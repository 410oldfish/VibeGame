using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HexDemo.PlayModeTests
{
    public sealed class HexUiToolkitPlayModeTests
    {
        [Test]
        public void UiToolkitResources_AreLoadable()
        {
            Assert.That(Resources.Load<VisualTreeAsset>("UI Toolkit/Screens/BattleRoot"), Is.Not.Null);
            Assert.That(Resources.Load<VisualTreeAsset>("UI Toolkit/Screens/AdventureRoot"), Is.Not.Null);
            Assert.That(Resources.Load<VisualTreeAsset>("UI Toolkit/Screens/RestRoot"), Is.Not.Null);
            Assert.That(Resources.Load<VisualTreeAsset>("UI Toolkit/Templates/Card"), Is.Not.Null);
            Assert.That(Resources.Load<StyleSheet>("UI Toolkit/Styles/HexTheme"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RuntimeDocuments_SharePanelSettingsAndReferenceResolution()
        {
            var first = new GameObject("FirstDocument");
            var second = new GameObject("SecondDocument");
            var attach = RequireType("HexDemo.HexUiToolkitRuntime").GetMethod("AttachDocument", BindingFlags.Static | BindingFlags.NonPublic);
            var firstDocument = (UIDocument)attach.Invoke(null, new object[] { first, "BattleRoot", 1 });
            var secondDocument = (UIDocument)attach.Invoke(null, new object[] { second, "AdventureRoot", 2 });
            yield return null;

            Assert.That(firstDocument.panelSettings, Is.SameAs(secondDocument.panelSettings));
            Assert.That(firstDocument.panelSettings.referenceResolution, Is.EqualTo(new Vector2Int(1920, 1080)));
            Assert.That(firstDocument.panelSettings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));

            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
        }

        [UnityTest]
        public IEnumerator BattleView_EmptySnapshotProducesExpectedContract()
        {
            var controllerObject = new GameObject("Controller");
            var viewObject = new GameObject("View");
            var controller = controllerObject.AddComponent(RequireType("HexDemo.HexBattleController"));
            var view = viewObject.AddComponent(RequireType("HexDemo.HexBattleToolkitUI"));
            view.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public).Invoke(view, new object[] { controller });
            yield return null;

            var root = viewObject.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<VisualElement>("hand"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("hand").childCount, Is.EqualTo(0));
            Assert.That(root.Q<Button>("end-turn").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("draw").text, Does.Contain("0"));
            Assert.That((bool)view.GetType().GetMethod("IsBlockingWorldClick", BindingFlags.Instance | BindingFlags.Public).Invoke(view, null), Is.False);

            UnityEngine.Object.Destroy(viewObject);
            UnityEngine.Object.Destroy(controllerObject);
        }

        [UnityTest]
        public IEnumerator AdventureView_RendersProfessionAndModalContracts()
        {
            var host = new GameObject("AdventureView");
            var view = host.AddComponent(RequireType("HexDemo.HexAdventureToolkitView"));
            const BindingFlags instanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            view.GetType().GetMethod("Initialize", instanceMethods).Invoke(view, null);
            view.GetType().GetMethod("ShowProfessionSelection", instanceMethods).Invoke(view, new object[] { "离线测试", null });
            yield return null;

            var root = host.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Query<Button>().ToList().Count, Is.EqualTo(3));
            var overlayProperty = view.GetType().GetProperty("IsOverlayOpen", instanceMethods);
            Assert.That((bool)overlayProperty.GetValue(view), Is.True);
            view.GetType().GetMethod("ClearOverlay", instanceMethods).Invoke(view, null);
            Assert.That((bool)overlayProperty.GetValue(view), Is.False);

            UnityEngine.Object.Destroy(host);
        }

        private static Type RequireType(string fullName)
        {
            var type = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Missing runtime type {fullName}");
            return type;
        }
    }
}
