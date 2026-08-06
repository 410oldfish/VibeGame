using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexDemo
{
    internal sealed class HexRestToolkitView : MonoBehaviour
    {
        private Label _status;
        private Button _leave;
        private Action _leaveAction;

        internal void Initialize(Action leaveAction)
        {
            _leaveAction = leaveAction;
            var document = HexUiToolkitRuntime.AttachDocument(gameObject, "RestRoot", 120);
            var root = document.rootVisualElement;
            HexUiToolkitRuntime.PrepareRoot(root);
            var panel = root.Q<VisualElement>("rest-panel");
            if (panel == null)
            {
                panel = new VisualElement();
                panel.AddToClassList("hex-panel");
                root.Add(panel);
            }
            panel.Clear();
            panel.Add(new Label("休息处") { name = "title" });
            panel.Q<Label>("title").AddToClassList("hex-title");
            _status = new Label { name = "status" };
            _status.AddToClassList("hex-subtitle");
            panel.Add(_status);
            _leave = new Button { text = "离开休息处" };
            _leave.AddToClassList("hex-button");
            panel.Add(_leave);
            _leave.clicked += OnLeave;
        }

        private void OnDestroy()
        {
            if (_leave != null)
                _leave.clicked -= OnLeave;
        }

        internal void Refresh(bool used, int healAmount, int currentHealth, int maxHealth)
        {
            if (_status != null)
                _status.text = used
                    ? $"篝火已使用\n生命 {currentHealth}/{maxHealth}"
                    : $"点击场景中的篝火恢复 {healAmount} 点生命\n生命 {currentHealth}/{maxHealth}";
        }

        private void OnLeave() => _leaveAction?.Invoke();
    }
}
