using TMPro;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexRestController : MonoBehaviour
    {
        public Camera rayCamera;
        public GameObject campfireObject;
        [Range(0f, 1f)]
        public float healPercent = 0.25f;

        private HexBattleUnit _playerUnit;
        private Canvas _canvas;
        private TextMeshProUGUI _statusLabel;
        private bool _usedCampfire;
        private HexGrid _grid;
        private bool _updateRegistered;
        private HexRestToolkitView _toolkitView;

        public System.Action<bool, HexBattleUnit> RestFinished;

        public void Initialize(HexBattleUnit playerUnit, Camera battleCamera)
        {
            _playerUnit = playerUnit;
            rayCamera = battleCamera != null ? battleCamera : Camera.main;
            _grid = Object.FindFirstObjectByType<HexGrid>();
            BuildToolkitView();
            RegisterUpdate();
            Refresh();
        }

        private void OnDestroy()
        {
            UnregisterUpdate();
        }

        private void Tick()
        {
            if (campfireObject == null || rayCamera == null || _usedCampfire)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform.IsChildOf(campfireObject.transform))
                    UseCampfire();
            }
        }

        private void UseCampfire()
        {
            if (_usedCampfire || _playerUnit == null)
                return;

            _usedCampfire = true;
            int healAmount = GetHealAmount();
            _playerUnit.State.currentHealth = Mathf.Min(_playerUnit.State.maxHealth, _playerUnit.State.currentHealth + healAmount);
            if (_grid != null)
                _playerUnit.SnapTo(_grid, 0.03f);
            Refresh();
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("RestHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreatePanel(canvasGO.transform, "RestPanel", new Vector2(20f, -20f), new Vector2(420f, 180f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _statusLabel = CreateText(panel.transform, "Status", new Vector2(18f, -18f), new Vector2(360f, 96f), 26f);

            var leavePanel = CreatePanel(canvasGO.transform, "LeaveButton", new Vector2(-20f, 20f), new Vector2(220f, 84f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var leaveButton = leavePanel.gameObject.AddComponent<Button>();
            leaveButton.onClick.AddListener(() =>
            {
                GameEvent.Send(HexGameEvents.RestFinished, true, _playerUnit);
                RestFinished?.Invoke(true, _playerUnit);
            });
            var leaveText = CreateText(leavePanel.transform, "LeaveLabel", Vector2.zero, new Vector2(220f, 84f), 28f);
            leaveText.alignment = TextAlignmentOptions.Center;
            leaveText.text = "Leave Rest";
        }

        private void BuildToolkitView()
        {
            var host = new GameObject("RestToolkitUI");
            host.transform.SetParent(transform, false);
            _toolkitView = host.AddComponent<HexRestToolkitView>();
            _toolkitView.Initialize(() =>
            {
                GameEvent.Send(HexGameEvents.RestFinished, true, _playerUnit);
                RestFinished?.Invoke(true, _playerUnit);
            });
        }

        private void Refresh()
        {
            if (_toolkitView != null && _playerUnit != null)
                _toolkitView.Refresh(_usedCampfire, GetHealAmount(), _playerUnit.State.currentHealth, _playerUnit.State.maxHealth);
            if (_statusLabel == null || _playerUnit == null)
                return;

            _statusLabel.text = _usedCampfire
                ? $"Campfire used\nHP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}"
                : $"Click the campfire to heal {GetHealAmount()} HP ({Mathf.RoundToInt(healPercent * 100f)}% Max HP)\nHP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}";
        }

        private int GetHealAmount()
        {
            if (_playerUnit?.State == null)
                return 0;

            return Mathf.CeilToInt(_playerUnit.State.maxHealth * Mathf.Clamp01(healPercent));
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.9f);
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private void RegisterUpdate()
        {
            if (_updateRegistered)
                return;

            HexGameModule.Update.AddUpdateListener(Tick);
            _updateRegistered = true;
        }

        private void UnregisterUpdate()
        {
            if (!_updateRegistered)
                return;

            HexGameModule.Update.RemoveUpdateListener(Tick);
            _updateRegistered = false;
        }
    }
}
