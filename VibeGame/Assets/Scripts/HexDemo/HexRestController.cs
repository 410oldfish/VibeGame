using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexRestController : MonoBehaviour
    {
        public Camera rayCamera;
        public GameObject campfireObject;
        public int healAmount = 10;

        private HexBattleUnit _playerUnit;
        private Canvas _canvas;
        private TextMeshProUGUI _statusLabel;
        private bool _usedCampfire;
        private HexGrid _grid;

        public System.Action<bool, HexBattleUnit> RestFinished;

        public void Initialize(HexBattleUnit playerUnit, Camera battleCamera)
        {
            _playerUnit = playerUnit;
            rayCamera = battleCamera != null ? battleCamera : Camera.main;
            _grid = Object.FindFirstObjectByType<HexGrid>();
            BuildCanvas();
            Refresh();
        }

        private void Update()
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
            leaveButton.onClick.AddListener(() => RestFinished?.Invoke(true, _playerUnit));
            var leaveText = CreateText(leavePanel.transform, "LeaveLabel", Vector2.zero, new Vector2(220f, 84f), 28f);
            leaveText.alignment = TextAlignmentOptions.Center;
            leaveText.text = "Leave Rest";
        }

        private void Refresh()
        {
            if (_statusLabel == null || _playerUnit == null)
                return;

            _statusLabel.text = _usedCampfire
                ? $"Campfire used\nHP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}"
                : $"Campfire heals {healAmount}\nHP {_playerUnit.State.currentHealth}/{_playerUnit.State.maxHealth}";
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
    }
}
