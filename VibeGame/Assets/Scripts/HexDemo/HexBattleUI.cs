using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexBattleUI : MonoBehaviour
    {
        private HexBattleController _controller;
        private readonly List<HexCardView> _cardViews = new();
        private RectTransform _handRoot;
        private TextMeshProUGUI _turnLabel;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _deckLabel;
        private Button _endTurnButton;
        private Canvas _canvas;

        public Canvas Canvas => _canvas;

        public void Initialize(HexBattleController controller)
        {
            _controller = controller;
            EnsureEventSystem();
            BuildCanvas();
            Refresh();
        }

        public void Refresh()
        {
            if (_controller == null)
                return;

            _turnLabel.text = _controller.GetTurnSummary();
            _statusLabel.text = _controller.GetStatusSummary();
            _deckLabel.text = _controller.GetDeckSummary();
            _endTurnButton.interactable = _controller.CanLocalPlayerEndTurn();
            RebuildHand();
        }

        private void RebuildHand()
        {
            for (int i = _cardViews.Count - 1; i >= 0; i--)
                Destroy(_cardViews[i].gameObject);

            _cardViews.Clear();

            var hand = _controller.GetLocalHand();
            for (int i = 0; i < hand.Count; i++)
            {
                var cardGO = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HexCardView));
                cardGO.transform.SetParent(_handRoot, false);

                var rect = cardGO.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(182f, 240f);

                var image = cardGO.GetComponent<Image>();
                image.color = Color.Lerp(hand[i].definition.color, Color.black, 0.12f);
                image.raycastTarget = true;

                CreateCardFace(cardGO.transform, hand[i]);

                var view = cardGO.GetComponent<HexCardView>();
                view.Initialize(_controller, hand[i], _canvas);
                _cardViews.Add(view);
            }
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("BattleHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hudPanel = CreatePanel(canvasGO.transform, "HUD", new Vector2(20f, -20f), new Vector2(460f, 200f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _turnLabel = CreateTMP(hudPanel.transform, "Turn", new Vector2(18f, -16f), new Vector2(420f, 34f), 30, FontStyles.Bold);
            _statusLabel = CreateTMP(hudPanel.transform, "Status", new Vector2(18f, -58f), new Vector2(420f, 86f), 22, FontStyles.Normal);
            _deckLabel = CreateTMP(hudPanel.transform, "Deck", new Vector2(18f, -146f), new Vector2(420f, 34f), 20, FontStyles.Normal);

            var handPanel = CreatePanel(canvasGO.transform, "HandPanel", new Vector2(0f, 0f), new Vector2(1180f, 292f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            handPanel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
            _handRoot = new GameObject("HandRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _handRoot.SetParent(handPanel.transform, false);
            _handRoot.anchorMin = new Vector2(0.5f, 0f);
            _handRoot.anchorMax = new Vector2(0.5f, 0f);
            _handRoot.pivot = new Vector2(0.5f, 0f);
            _handRoot.anchoredPosition = new Vector2(0f, 18f);
            _handRoot.sizeDelta = new Vector2(1120f, 248f);

            var layout = _handRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var buttonPanel = CreatePanel(canvasGO.transform, "ActionPanel", new Vector2(-20f, 20f), new Vector2(220f, 88f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _endTurnButton = buttonPanel.gameObject.AddComponent<Button>();
            _endTurnButton.onClick.AddListener(_controller.RequestEndTurn);
            var buttonText = CreateTMP(buttonPanel.transform, "ButtonLabel", new Vector2(0f, 0f), new Vector2(220f, 88f), 28, FontStyles.Bold);
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.text = "End Turn";
        }

        private static void CreateCardFace(Transform parent, HexCardInstance card)
        {
            var title = CreateTMP(parent, "Title", new Vector2(16f, -14f), new Vector2(150f, 36f), 24, FontStyles.Bold);
            title.text = card.definition.displayName;

            var costBadge = CreatePanel(parent, "CostBadge", new Vector2(14f, -54f), new Vector2(48f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            costBadge.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var costText = CreateTMP(costBadge.transform, "Cost", new Vector2(0f, 0f), new Vector2(48f, 48f), 28, FontStyles.Bold);
            costText.alignment = TextAlignmentOptions.Center;
            costText.text = card.definition.energyCost.ToString();

            var typeText = CreateTMP(parent, "Type", new Vector2(72f, -58f), new Vector2(90f, 26f), 18, FontStyles.Bold);
            typeText.text = card.definition.effectType == HexCardEffectType.Attack ? "Attack" : "Skill";

            var body = CreateTMP(parent, "Body", new Vector2(16f, -112f), new Vector2(150f, 92f), 20, FontStyles.Normal);
            body.text = card.definition.effectType == HexCardEffectType.Attack
                ? $"Deal {card.definition.amount} damage\nRange {card.definition.range}"
                : $"Gain {card.definition.amount} armor";
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
            go.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            return rect;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
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
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemGO);
        }
    }

    public sealed class HexCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private HexBattleController _controller;
        private HexCardInstance _card;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Transform _originalParent;
        private Vector2 _originalAnchoredPosition;
        private Canvas _rootCanvas;
        private Image _image;

        public void Initialize(HexBattleController controller, HexCardInstance card, Canvas rootCanvas)
        {
            _controller = controller;
            _card = card;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rootCanvas = rootCanvas;
            _image = GetComponent<Image>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = _rectTransform.parent;
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            if (_rootCanvas != null)
                _rectTransform.SetParent(_rootCanvas.transform, true);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;
            if (_image != null)
                _image.raycastTarget = false;
            _controller.BeginCardDrag(_card);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTransform.position = eventData.position;
            _controller.UpdateDraggedCard(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            bool played = _controller.EndCardDrag(eventData.position);
            if (!played)
            {
                _rectTransform.SetParent(_originalParent, false);
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
                _canvasGroup.alpha = 1f;
                if (_image != null)
                    _image.raycastTarget = true;
            }
            else
            {
                Object.Destroy(gameObject);
            }
        }
    }
}
