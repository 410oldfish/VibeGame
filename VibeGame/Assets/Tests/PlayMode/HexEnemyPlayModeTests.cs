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
    public sealed class HexEnemyPlayModeTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void EnemyDatabase_ContainsElevenIndependentDefinitions()
        {
            var database = Resources.Load("HexEnemyDatabase");
            Assert.That(database, Is.Not.Null);

            var enemies = GetField(database, "enemies") as IList;
            Assert.That(enemies, Is.Not.Null);
            Assert.That(enemies.Count, Is.EqualTo(11));

            var ids = enemies.Cast<object>().Select(enemy => (string)GetField(enemy, "id")).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(11));
        }

        [UnityTest]
        public IEnumerator DefaultSandbox_InitializesGoblinAndSpearGoblin()
        {
            SceneManager.LoadScene("BattleSandbox", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var ids = GetEnemyStates(SceneManager.GetActiveScene())
                .Select(state => (string)GetField(state, "enemyDefinitionId"))
                .OrderBy(id => id)
                .ToArray();

            Assert.That(ids, Is.EqualTo(new[] { "goblin", "spear_goblin" }));
        }

        [UnityTest]
        public IEnumerator CaptainSandbox_UsesSummonContract()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_GoblinCaptain");
            yield return null;
            yield return null;

            var states = GetEnemyStates(scene);
            Assert.That(states, Has.Count.EqualTo(1));
            Assert.That(GetField(states[0], "enemyDefinitionId"), Is.EqualTo("goblin_captain"));

            object definition = InvokeStatic("HexDemo.HexCardLibrary", "GetEnemyDefinition", "goblin_captain");
            Assert.That(GetField(definition, "maxSummons"), Is.EqualTo(2));
            Assert.That(GetField(definition, "summonHealth"), Is.EqualTo(15));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ChieftainPhaseTwoSandbox_ReplacesApproachWithQuake()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_TribalChieftain_Phase2");
            yield return null;
            yield return null;

            var states = GetEnemyStates(scene);
            Assert.That(states, Has.Count.EqualTo(1));
            object state = states[0];
            Assert.That(GetField(state, "enemyDefinitionId"), Is.EqualTo("tribal_chieftain"));
            Assert.That(GetField(state, "enemyPhaseTwoApplied"), Is.True);
            Assert.That(GetField(state, "currentHealth"), Is.EqualTo(50));
            Assert.That(GetField(state, "maxHealth"), Is.EqualTo(100));

            object definition = InvokeStatic("HexDemo.HexCardLibrary", "GetEnemyDefinition", "tribal_chieftain");
            var phaseTwo = GetField(definition, "phaseTwoDeckDefinitions") as IEnumerable;
            var ids = phaseTwo.Cast<object>().Select(card => (string)GetField(card, "id")).ToArray();
            Assert.That(ids.Count(id => id == "enemy_chieftain_quake"), Is.EqualTo(2));
            Assert.That(ids, Does.Not.Contain("enemy_goblin_approach"));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ChieftainCharge_DamagesTargetAndQueuesStunOnlyOnObstacle()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_TribalChieftain");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            Component enemy = units.Single(component => GetField(GetState(component), "faction").ToString() == "Enemy");
            Component player = units.Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            object enemyState = GetState(enemy);
            object playerState = GetState(player);
            Type coordType = RequireType("HexDemo.HexAxialCoord");

            SetField(enemyState, "coord", Activator.CreateInstance(coordType, 0, 0));
            SetField(playerState, "coord", Activator.CreateInstance(coordType, 1, 0));
            SetField(playerState, "armor", 0);
            int healthBeforeCollision = (int)GetField(playerState, "currentHealth");
            yield return RunCharge(controllerType, controller, enemy, player);
            Assert.That(GetField(playerState, "currentHealth"), Is.EqualTo(healthBeforeCollision - 6));
            Assert.That(GetField(enemyState, "stun"), Is.EqualTo(0));

            SetField(enemyState, "coord", Activator.CreateInstance(coordType, 999, 999));
            SetField(playerState, "coord", Activator.CreateInstance(coordType, 1001, 999));
            int healthBeforeObstacle = (int)GetField(playerState, "currentHealth");
            yield return RunCharge(controllerType, controller, enemy, player);
            Assert.That(GetField(playerState, "currentHealth"), Is.EqualTo(healthBeforeObstacle));
            Assert.That(GetField(enemyState, "stun"), Is.EqualTo(1));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallSandbox_SpawnsPairedThreeCellWallsAndBlocksOccupancy()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            var walls = GetUnits(scene)
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => (string)GetField(GetState(component), "id"))
                .ToList();
            Assert.That(walls, Has.Count.EqualTo(2));

            object firstState = GetState(walls[0]);
            object secondState = GetState(walls[1]);
            object firstWallState = GetField(firstState, "livingWall");
            object secondWallState = GetField(secondState, "livingWall");
            Assert.That(firstWallState, Is.Not.Null);
            Assert.That(secondWallState, Is.Not.Null);
            Assert.That(GetField(firstWallState, "pairedWallId"), Is.EqualTo(GetField(secondState, "id")));
            Assert.That(GetField(secondWallState, "pairedWallId"), Is.EqualTo(GetField(firstState, "id")));

            IList firstOccupied = GetOccupiedCoords(walls[0]);
            IList secondOccupied = GetOccupiedCoords(walls[1]);
            Assert.That(firstOccupied, Has.Count.EqualTo(3));
            Assert.That(secondOccupied, Has.Count.EqualTo(3));
            AssertHorizontalLivingWallFootprint(walls[0], 3);
            AssertHorizontalLivingWallFootprint(walls[1], 3);

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            MethodInfo occupiedMethod = controllerType.GetMethod("IsOccupied", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(occupiedMethod, Is.Not.Null);
            Assert.That(occupiedMethod.Invoke(controller, new[] { firstOccupied[1], null }), Is.True);

            Type segmentType = RequireType("HexDemo.HexLivingWallSegmentView");
            int segmentCount = UnityEngine.Object.FindObjectsByType(segmentType)
                .Cast<Component>()
                .Count(component => component.gameObject.scene == scene);
            Assert.That(segmentCount, Is.EqualTo(6));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallMovement_BlocksWallCellsAndConnectedSegments()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            Component player = GetUnits(scene)
                .Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            object playerState = GetState(player);
            Type coordType = RequireType("HexDemo.HexAxialCoord");
            FieldInfo professionField = playerState.GetType().GetField("profession", InstanceFields);
            FieldInfo formField = playerState.GetType().GetField("druidForm", InstanceFields);
            Assert.That(professionField, Is.Not.Null);
            Assert.That(formField, Is.Not.Null);
            professionField.SetValue(playerState, Enum.ToObject(professionField.FieldType, 3));
            formField.SetValue(playerState, Enum.ToObject(formField.FieldType, 2));
            SetField(playerState, "currentMovePoints", 10);

            MethodInfo buildPath = controllerType.GetMethod("BuildMovementPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildPath, Is.Not.Null);

            SetField(playerState, "coord", Activator.CreateInstance(coordType, 1, 5));
            object throughCell = buildPath.Invoke(controller, new[]
            {
                player,
                Activator.CreateInstance(coordType, 3, 5),
            });
            Assert.That(throughCell, Is.Null);

            SetField(playerState, "coord", Activator.CreateInstance(coordType, 1, 6));
            object throughConnection = buildPath.Invoke(controller, new[]
            {
                player,
                Activator.CreateInstance(coordType, 3, 5),
            });
            Assert.That(throughConnection, Is.Null);

            SetField(playerState, "coord", Activator.CreateInstance(coordType, 0, 5));
            var legalPath = buildPath.Invoke(controller, new[]
            {
                player,
                Activator.CreateInstance(coordType, 1, 5),
            }) as IList;
            Assert.That(legalPath, Has.Count.EqualTo(2));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallAdvance_PushesPlayerOnlyInAdvanceDirection()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            Component player = units.Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            var walls = units
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => GetCoord(GetField(GetState(component), "coord"), "q"))
                .ToArray();
            Type coordType = RequireType("HexDemo.HexAxialCoord");
            object playerState = GetState(player);
            object movingWallState = GetState(walls[0]);
            object pairedWallState = GetState(walls[1]);
            SetField(movingWallState, "coord", Activator.CreateInstance(coordType, 4, 5));
            SetField(pairedWallState, "coord", Activator.CreateInstance(coordType, 7, 5));
            SetField(playerState, "coord", Activator.CreateInstance(coordType, 5, 5));
            SetField(playerState, "armor", 0);
            SetField(playerState, "toughness", 0);
            int healthBefore = (int)GetField(playerState, "currentHealth");

            yield return RunLivingWallAdvance(controllerType, controller, walls[0]);

            object movingWallCoord = GetField(movingWallState, "coord");
            object playerCoord = GetField(playerState, "coord");
            Assert.That(GetCoord(movingWallCoord, "q"), Is.EqualTo(5));
            Assert.That(GetCoord(playerCoord, "q"), Is.EqualTo(6));
            Assert.That(GetCoord(playerCoord, "r"), Is.EqualTo(5));
            Assert.That(GetOccupiedCoords(walls[0]).Cast<object>().Any(coord => coord.Equals(playerCoord)), Is.False);
            Assert.That(GetField(playerState, "currentHealth"), Is.EqualTo(healthBefore));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallAdvance_WhenForwardPushFails_DealsSqueezeAndRollsBackWall()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            Component player = units.Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            var walls = units
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => GetCoord(GetField(GetState(component), "coord"), "q"))
                .ToArray();
            Type coordType = RequireType("HexDemo.HexAxialCoord");
            object playerState = GetState(player);
            object movingWallState = GetState(walls[0]);
            object pairedWallState = GetState(walls[1]);
            SetField(movingWallState, "coord", Activator.CreateInstance(coordType, 4, 5));
            SetField(pairedWallState, "coord", Activator.CreateInstance(coordType, 7, 5));
            SetField(playerState, "coord", Activator.CreateInstance(coordType, 5, 5));
            SetField(playerState, "armor", 0);
            SetField(playerState, "toughness", 1);
            SetField(playerState, "currentHealth", 100);

            yield return RunLivingWallAdvance(controllerType, controller, walls[0]);

            object movingWallCoord = GetField(movingWallState, "coord");
            object playerCoord = GetField(playerState, "coord");
            Assert.That(GetCoord(movingWallCoord, "q"), Is.EqualTo(4));
            Assert.That(GetCoord(movingWallCoord, "r"), Is.EqualTo(5));
            Assert.That(GetField(playerState, "currentHealth"), Is.EqualTo(50));
            Assert.That(GetOccupiedCoords(walls[0]).Cast<object>().Any(coord => coord.Equals(playerCoord)), Is.False);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallBreak_DamagesMainByCeilTwentyPercentAndKillsOffspring()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            Component wall = GetUnits(scene).First(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall");
            object state = GetState(wall);
            MethodInfo breakMethod = controllerType.GetMethod("ApplyLivingWallBreak", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(breakMethod, Is.Not.Null);

            Assert.That(breakMethod.Invoke(controller, new object[] { wall }), Is.True);
            Assert.That(GetField(state, "currentHealth"), Is.EqualTo(27));

            object livingWallState = GetField(state, "livingWall");
            SetField(livingWallState, "isOffspring", true);
            SetField(state, "maxHealth", 50);
            SetField(state, "currentHealth", 50);
            Assert.That(breakMethod.Invoke(controller, new object[] { wall }), Is.True);
            Assert.That(GetField(state, "currentHealth"), Is.EqualTo(0));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallAdvance_KeepsSegmentVisualsMetadataAndOccupancyAligned()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            Component player = units.Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            Component wall = units
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => GetCoord(GetField(GetState(component), "coord"), "q"))
                .First();

            yield return RunLivingWallAdvance(controllerType, controller, wall);

            object core = GetField(GetState(wall), "coord");
            Assert.That(GetCoord(core, "q"), Is.EqualTo(3));
            Assert.That(GetCoord(core, "r"), Is.EqualTo(5));
            Assert.That(Quaternion.Angle(wall.transform.rotation, Quaternion.identity), Is.LessThan(0.01f));
            AssertLivingWallSegmentsAligned(scene, wall, GetField(controller, "grid"));

            MethodInfo occupiedMethod = controllerType.GetMethod("IsOccupied", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pathMethod = controllerType.GetMethod("BuildMovementPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(occupiedMethod, Is.Not.Null);
            Assert.That(pathMethod, Is.Not.Null);
            IList occupied = GetOccupiedCoords(wall);
            foreach (object coord in occupied)
                Assert.That(occupiedMethod.Invoke(controller, new[] { coord, null }), Is.True);
            Assert.That(pathMethod.Invoke(controller, new[] { player, occupied[1] }), Is.Null);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallReform_PairedWallsTeleportToPlayerCenteredOpposites()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            Component player = units.Single(component => GetField(GetState(component), "faction").ToString() == "Player");
            var walls = units
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => (string)GetField(GetState(component), "id"))
                .ToArray();
            object firstWallState = GetField(GetState(walls[0]), "livingWall");
            object secondWallState = GetField(GetState(walls[1]), "livingWall");
            SetField(firstWallState, "reformPending", true);
            SetField(secondWallState, "reformPending", true);

            yield return RunLivingWallTurnStarts(controllerType, controller);

            object playerCoord = GetField(GetState(player), "coord");
            object firstCoord = GetField(GetState(walls[0]), "coord");
            object secondCoord = GetField(GetState(walls[1]), "coord");
            Assert.That(GetCoord(firstCoord, "q") + GetCoord(secondCoord, "q"), Is.EqualTo(GetCoord(playerCoord, "q") * 2));
            Assert.That(GetCoord(firstCoord, "r") + GetCoord(secondCoord, "r"), Is.EqualTo(GetCoord(playerCoord, "r") * 2));
            Assert.That(InvokeStatic("HexDemo.HexAxialCoord", "Distance", playerCoord, firstCoord), Is.EqualTo(3));
            Assert.That(InvokeStatic("HexDemo.HexAxialCoord", "Distance", playerCoord, secondCoord), Is.EqualTo(3));
            Assert.That(GetOccupiedCoords(walls[0]), Has.Count.EqualTo(4));
            Assert.That(GetOccupiedCoords(walls[1]), Has.Count.EqualTo(4));
            AssertHorizontalLivingWallFootprint(walls[0], 4);
            AssertHorizontalLivingWallFootprint(walls[1], 4);
            Assert.That(GetField(firstWallState, "reformPending"), Is.False);
            Assert.That(GetField(secondWallState, "reformPending"), Is.False);
            object grid = GetField(controller, "grid");
            AssertLivingWallSegmentsAligned(scene, walls[0], grid);
            AssertLivingWallSegmentsAligned(scene, walls[1], grid);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallReform_SingleWallUsesOnlyPairedWallOppositePoint()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var units = GetUnits(scene);
            var walls = units
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => (string)GetField(GetState(component), "id"))
                .ToArray();
            Type coordType = RequireType("HexDemo.HexAxialCoord");
            object firstState = GetState(walls[0]);
            object secondState = GetState(walls[1]);
            object firstWallState = GetField(firstState, "livingWall");
            SetField(firstState, "coord", Activator.CreateInstance(coordType, 4, 1));
            SetField(firstWallState, "reformPending", true);

            yield return RunLivingWallTurnStarts(controllerType, controller);

            object firstCoord = GetField(firstState, "coord");
            object secondCoord = GetField(secondState, "coord");
            Assert.That(GetCoord(firstCoord, "q"), Is.EqualTo(2));
            Assert.That(GetCoord(firstCoord, "r"), Is.EqualTo(5));
            Assert.That(GetCoord(secondCoord, "q"), Is.EqualTo(8));
            Assert.That(GetCoord(secondCoord, "r"), Is.EqualTo(5));
            Assert.That(GetOccupiedCoords(walls[0]), Has.Count.EqualTo(4));
            Assert.That(GetOccupiedCoords(walls[1]), Has.Count.EqualTo(3));
            AssertHorizontalLivingWallFootprint(walls[0], 4);
            AssertHorizontalLivingWallFootprint(walls[1], 3);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LivingWallReform_SingleWallDoesNotSearchWhenOppositePointIsBlocked()
        {
            Scene scene = BuildScenario("Debug/BattleSandbox_LivingWall");
            yield return null;
            yield return null;

            Type controllerType = RequireType("HexDemo.HexBattleController");
            var controller = UnityEngine.Object.FindObjectsByType(controllerType)
                .Cast<MonoBehaviour>()
                .Single(component => component.gameObject.scene == scene);
            var walls = GetUnits(scene)
                .Where(component => (string)GetField(GetState(component), "enemyDefinitionId") == "living_wall")
                .OrderBy(component => (string)GetField(GetState(component), "id"))
                .ToArray();
            Type coordType = RequireType("HexDemo.HexAxialCoord");
            object firstState = GetState(walls[0]);
            object secondState = GetState(walls[1]);
            object firstWallState = GetField(firstState, "livingWall");
            object secondWallState = GetField(secondState, "livingWall");
            SetField(firstState, "coord", Activator.CreateInstance(coordType, 4, 1));
            SetField(secondState, "coord", Activator.CreateInstance(coordType, 8, 5));
            var secondOffsets = GetField(secondWallState, "footprintOffsets") as IList;
            Assert.That(secondOffsets, Is.Not.Null);
            secondOffsets.Add(Activator.CreateInstance(coordType, -6, 0));
            SetField(firstWallState, "reformPending", true);

            yield return RunLivingWallTurnStarts(controllerType, controller);

            object firstCoord = GetField(firstState, "coord");
            Assert.That(GetCoord(firstCoord, "q"), Is.EqualTo(4));
            Assert.That(GetCoord(firstCoord, "r"), Is.EqualTo(1));
            Assert.That(GetField(firstWallState, "reformPending"), Is.False);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static Scene BuildScenario(string resourcePath)
        {
            Scene scene = SceneManager.CreateScene($"EnemyPlayMode_{Guid.NewGuid():N}");
            SceneManager.SetActiveScene(scene);

            Type bootstrapType = RequireType("HexDemo.HexBattleSandboxBootstrap");
            var host = new GameObject("BattleSandboxTestBootstrap");
            var bootstrap = host.AddComponent(bootstrapType);
            SetField(bootstrap, "autoStartOnPlay", false);
            SetField(bootstrap, "scenario", Resources.Load(resourcePath));
            bootstrapType.GetMethod("BuildSandboxBattle", BindingFlags.Instance | BindingFlags.Public)?.Invoke(bootstrap, null);
            return scene;
        }

        private static List<object> GetEnemyStates(Scene scene)
        {
            return GetUnits(scene)
                .Select(GetState)
                .Where(state => state != null && GetField(state, "faction").ToString() == "Enemy")
                .ToList();
        }

        private static List<Component> GetUnits(Scene scene)
        {
            Type unitType = RequireType("HexDemo.HexBattleUnit");
            return UnityEngine.Object.FindObjectsByType(unitType)
                .Cast<Component>()
                .Where(component => component.gameObject.scene == scene)
                .ToList();
        }

        private static object GetState(Component unit)
        {
            PropertyInfo stateProperty = unit.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(stateProperty, Is.Not.Null);
            return stateProperty.GetValue(unit);
        }

        private static IList GetOccupiedCoords(Component unit)
        {
            PropertyInfo property = unit.GetType().GetProperty("OccupiedCoords", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(unit) as IList;
        }

        private static void AssertHorizontalLivingWallFootprint(Component wall, int expectedSize)
        {
            object core = GetField(GetState(wall), "coord");
            int coreQ = GetCoord(core, "q");
            int[] rows = GetOccupiedCoords(wall)
                .Cast<object>()
                .Select(coord =>
                {
                    Assert.That(GetCoord(coord, "q"), Is.EqualTo(coreQ));
                    return GetCoord(coord, "r");
                })
                .OrderBy(row => row)
                .ToArray();

            Assert.That(rows, Has.Length.EqualTo(expectedSize));
            for (int i = 1; i < rows.Length; i++)
                Assert.That(rows[i], Is.EqualTo(rows[i - 1] + 1));
        }

        private static IEnumerator RunCharge(Type controllerType, MonoBehaviour controller, Component enemy, Component player)
        {
            MethodInfo method = controllerType.GetMethod("ResolveEnemyChargeRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var routine = method.Invoke(controller, new object[] { enemy, player, 1, 6, true }) as IEnumerator;
            Assert.That(routine, Is.Not.Null);
            yield return controller.StartCoroutine(routine);
        }

        private static IEnumerator RunLivingWallTurnStarts(Type controllerType, MonoBehaviour controller)
        {
            MethodInfo method = controllerType.GetMethod("ResolveLivingWallTurnStarts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var routine = method.Invoke(controller, null) as IEnumerator;
            Assert.That(routine, Is.Not.Null);
            yield return controller.StartCoroutine(routine);
        }

        private static IEnumerator RunLivingWallAdvance(Type controllerType, MonoBehaviour controller, Component wall)
        {
            MethodInfo method = controllerType.GetMethod("ResolveLivingWallAdvance", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var routine = method.Invoke(controller, new object[] { wall }) as IEnumerator;
            Assert.That(routine, Is.Not.Null);
            yield return controller.StartCoroutine(routine);
        }

        private static void AssertLivingWallSegmentsAligned(Scene scene, Component wall, object grid)
        {
            IList occupied = GetOccupiedCoords(wall);
            Type segmentType = RequireType("HexDemo.HexLivingWallSegmentView");
            var segments = UnityEngine.Object.FindObjectsByType(segmentType)
                .Cast<Component>()
                .Where(component => component.gameObject.scene == scene)
                .Where(component => ReferenceEquals(
                    component.GetType().GetProperty("OwnerUnit", BindingFlags.Instance | BindingFlags.Public)?.GetValue(component),
                    wall))
                .ToArray();
            Assert.That(segments, Has.Length.EqualTo(occupied.Count));

            var storedCoords = new HashSet<string>();
            foreach (Component segment in segments)
            {
                object stored = segment.GetType().GetProperty("Coord", BindingFlags.Instance | BindingFlags.Public)?.GetValue(segment);
                Assert.That(stored, Is.Not.Null);
                object visual = InvokeStatic("HexDemo.HexBattlePathing", "WorldToAxial", grid, segment.transform.position);
                Assert.That(visual, Is.EqualTo(stored), segment.name);
                Assert.That(occupied.Cast<object>().Any(coord => coord.Equals(stored)), Is.True, segment.name);
                storedCoords.Add($"{GetCoord(stored, "q")},{GetCoord(stored, "r")}");
            }
            Assert.That(storedCoords, Has.Count.EqualTo(occupied.Count));
        }

        private static int GetCoord(object coord, string fieldName) => (int)GetField(coord, fieldName);

        private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
        {
            Type type = RequireType(typeName);
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, arguments);
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static object GetField(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null, fieldName);
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
