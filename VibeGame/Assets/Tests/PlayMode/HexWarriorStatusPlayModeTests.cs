using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HexDemo.PlayModeTests
{
    public sealed class HexWarriorStatusPlayModeTests
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator Windstep_StacksEffectiveAmountPerActiveMoveAndExpiresAtTurnEnd()
        {
            Scene scene = BuildScenario();
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            Component controller = FindComponentInScene(scene, controllerType);
            Component player = GetUnits(scene).Single(unit => GetField(GetState(unit), "faction").ToString() == "Player");
            object state = GetState(player);
            object coord = GetField(state, "coord");

            object firstCard = CreateWindstepCard(0);
            object secondCard = CreateWindstepCard(1);
            yield return RunWarriorCard(controller, player, firstCard, coord);
            yield return RunWarriorCard(controller, player, secondCard, coord);

            Assert.That(GetField(state, "warriorWindstepStrengthPerMoveThisTurn"), Is.EqualTo(5));

            object path = BuildTwoCoordPath(coord);
            InvokePostMovement(controller, player, path, "Active");
            InvokePostMovement(controller, player, path, "Active");

            Assert.That(GetField(state, "strength"), Is.EqualTo(10));
            Assert.That(GetField(state, "temporaryStrengthUntilEndOfTurn"), Is.EqualTo(10));

            InvokePostMovement(controller, player, path, "Forced");
            Assert.That(GetField(state, "strength"), Is.EqualTo(10));

            player.GetType().GetMethod("EndTurn", AllInstance)?.Invoke(player, null);
            Assert.That(GetField(state, "strength"), Is.Zero);
            Assert.That(GetField(state, "temporaryStrengthUntilEndOfTurn"), Is.Zero);
            Assert.That(GetField(state, "warriorWindstepStrengthPerMoveThisTurn"), Is.Zero);

            InvokePostMovement(controller, player, path, "Active");
            Assert.That(GetField(state, "strength"), Is.Zero);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ForcedMovement_SkipsWarriorPassivesButStillResolvesPathTrap()
        {
            Scene scene = BuildScenario();
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            Component controller = FindComponentInScene(scene, controllerType);
            Component player = GetUnits(scene).Single(unit => GetField(GetState(unit), "faction").ToString() == "Player");
            object state = GetState(player);
            SetField(state, "warriorWindstepStrengthPerMoveThisTurn", 2);
            SetField(state, "warriorSkirmishArmorOnMove", true);

            object path = BuildTwoCoordPath(GetField(state, "coord"));
            object trapCoord = ((IList)path)[1];
            object traps = GetField(controller, "_bloodTrapCoords");
            traps.GetType().GetMethod("Add")?.Invoke(traps, new[] { trapCoord });

            InvokePostMovement(controller, player, path, "Forced");

            Assert.That(GetField(state, "strength"), Is.Zero);
            Assert.That(GetField(state, "armor"), Is.Zero);
            Assert.That(GetField(state, "warriorMoveEventThisTurn"), Is.False);
            Assert.That(GetField(state, "bind"), Is.EqualTo(1));
            Assert.That(GetField(state, "bleed"), Is.EqualTo(5));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static Scene BuildScenario()
        {
            Scene scene = SceneManager.CreateScene($"WarriorStatusPlayMode_{Guid.NewGuid():N}");
            SceneManager.SetActiveScene(scene);

            Type bootstrapType = RequireType("HexDemo.HexBattleSandboxBootstrap");
            var host = new GameObject("BattleSandboxTestBootstrap");
            Component bootstrap = host.AddComponent(bootstrapType);
            SetField(bootstrap, "autoStartOnPlay", false);
            SetField(bootstrap, "scenario", Resources.Load("Debug/BattleSandbox_Default"));
            bootstrapType.GetMethod("BuildSandboxBattle", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bootstrap, null);
            return scene;
        }

        private static object CreateWindstepCard(int battleAmountModifier)
        {
            UnityEngine.Object asset = Resources.Load("Cards/Warrior/warrior_windstep_ready");
            Assert.That(asset, Is.Not.Null);
            object definition = asset.GetType().GetMethod("ToDefinition", AllInstance)?.Invoke(asset, null);
            Type cardType = RequireType("HexDemo.HexCardInstance");
            object card = Activator.CreateInstance(cardType, definition);
            SetField(card, "battleAmountModifier", battleAmountModifier);
            return card;
        }

        private static IEnumerator RunWarriorCard(Component controller, Component player, object card, object coord)
        {
            MethodInfo method = controller.GetType().GetMethod("ResolveWarriorDesignCardRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var routine = method.Invoke(controller, new[] { (object)player, player, card, 0, coord }) as IEnumerator;
            Assert.That(routine, Is.Not.Null);
            yield return ((MonoBehaviour)controller).StartCoroutine(routine);
        }

        private static void InvokePostMovement(Component controller, Component player, object path, string causeName)
        {
            Type causeType = RequireType("HexDemo.HexMovementCause");
            object cause = Enum.Parse(causeType, causeName);
            MethodInfo method = controller.GetType().GetMethod("HandlePostMovement", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new[] { (object)player, path, cause, null, 1 });
        }

        private static object BuildTwoCoordPath(object start)
        {
            Type coordType = start.GetType();
            object end = coordType.GetMethod("Neighbor", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new[] { start, (object)0 });
            Type listType = typeof(List<>).MakeGenericType(coordType);
            var path = (IList)Activator.CreateInstance(listType);
            path.Add(start);
            path.Add(end);
            return path;
        }

        private static List<Component> GetUnits(Scene scene)
        {
            Type unitType = RequireType("HexDemo.HexBattleUnit");
            return UnityEngine.Object.FindObjectsByType(unitType)
                .Cast<Component>()
                .Where(component => component.gameObject.scene == scene)
                .ToList();
        }

        private static Component FindComponentInScene(Scene scene, Type type)
        {
            return UnityEngine.Object.FindObjectsByType(type)
                .Cast<Component>()
                .Single(component => component.gameObject.scene == scene);
        }

        private static object GetState(Component unit)
        {
            return unit.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.Public)?.GetValue(unit);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, AllInstance);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().FullName}.{name}");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, AllInstance);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().FullName}.{name}");
            field.SetValue(target, value);
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
