using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexDemo
{
    internal static class HexTMPFontProvider
    {
        private static TMP_FontAsset s_runtimeFont;
        private static bool s_initialized;

        public static void EnsureInitialized()
        {
            if (s_initialized)
                return;

            s_initialized = true;
            s_runtimeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/HexChineseDynamic SDF");
            if (s_runtimeFont == null)
            {
                var font = Resources.Load<Font>("Fonts/simhei");
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Microsoft YaHei UI",
                        "Microsoft YaHei",
                        "SimHei",
                        "Noto Sans CJK SC",
                        "Arial Unicode MS",
                        "Segoe UI",
                        "Arial",
                    }, 32);
                }

                if (font != null)
                {
                    s_runtimeFont = TMP_FontAsset.CreateFontAsset(font);
                    if (s_runtimeFont != null)
                    {
                        s_runtimeFont.name = "HexRuntimeDynamicFont";
                        s_runtimeFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                        s_runtimeFont.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                    }
                }
            }

            if (s_runtimeFont == null)
                return;

            s_runtimeFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            var previousDefault = TMP_Settings.defaultFontAsset;
            s_runtimeFont.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (previousDefault != null &&
                previousDefault != s_runtimeFont &&
                !s_runtimeFont.fallbackFontAssetTable.Contains(previousDefault))
            {
                s_runtimeFont.fallbackFontAssetTable.Add(previousDefault);
            }

            TMP_Settings.defaultFontAsset = s_runtimeFont;
            TMP_Settings.fallbackFontAssets ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!TMP_Settings.fallbackFontAssets.Contains(s_runtimeFont))
                TMP_Settings.fallbackFontAssets.Insert(0, s_runtimeFont);
        }

        public static void ApplyTo(TMP_Text text)
        {
            if (text == null)
                return;

            EnsureInitialized();

            if (s_runtimeFont != null)
                text.font = s_runtimeFont;
            else if (TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
        }
    }

    public sealed class HexBattleUI : MonoBehaviour
    {
        private HexBattleController _controller;
        private readonly List<HexCardView> _cardViews = new();
        private RectTransform _handRoot;
        private TextMeshProUGUI _turnLabel;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _deckLabel;
        private TextMeshProUGUI _resourceLabel;
        private TextMeshProUGUI _weaponLabel;
        private Button _endTurnButton;
        private Button _swordSkillButton;
        private Button _axeSkillButton;
        private Button _hammerSkillButton;
        private Button _drawPileButton;
        private Button _discardPileButton;
        private TextMeshProUGUI _drawPileLabel;
        private TextMeshProUGUI _discardPileLabel;
        private RectTransform _pileModal;
        private TextMeshProUGUI _pileModalTitle;
        private RectTransform _pileModalContent;
        private Canvas _canvas;
        private const float CardWidth = 182f;
        private const float CardHeight = 240f;

        public Canvas Canvas => _canvas;

        public void Initialize(HexBattleController controller)
        {
            _controller = controller;
            EnsureEventSystem();
            BuildCanvas();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_canvas != null && _canvas.gameObject != gameObject)
                Destroy(_canvas.gameObject);
        }

        public void Refresh()
        {
            if (_controller == null)
                return;

            _turnLabel.text = _controller.GetTurnSummary();
            _statusLabel.text = _controller.GetStatusSummary();
            _deckLabel.text = _controller.GetDeckSummary();
            _resourceLabel.text = _controller.GetResourceSummary();
            _weaponLabel.text = _controller.GetWeaponSkillSummary();
            _endTurnButton.interactable = _controller.CanLocalPlayerEndTurn();
            _swordSkillButton.interactable = _controller.CanUseWeaponSkill(HexWeaponType.Sword);
            _axeSkillButton.interactable = _controller.CanUseWeaponSkill(HexWeaponType.Axe);
            _hammerSkillButton.interactable = _controller.CanUseWeaponSkill(HexWeaponType.Hammer);
            _drawPileLabel.text = $"Draw\n{_controller.GetLocalDrawPile().Count}";
            _discardPileLabel.text = $"Discard\n{_controller.GetLocalDiscardPile().Count}";
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
                rect.sizeDelta = new Vector2(CardWidth, CardHeight);

                var image = cardGO.GetComponent<Image>();
                image.color = Color.Lerp(hand[i].definition.color, Color.black, 0.12f);
                image.raycastTarget = true;

                CreateCardFace(cardGO.transform, hand[i], _controller.GetLocalCardCost(hand[i]));

                var view = cardGO.GetComponent<HexCardView>();
                view.Initialize(_controller, hand[i], _canvas);
                _cardViews.Add(view);
            }
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("BattleHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
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

            var resourcePanel = CreatePanel(canvasGO.transform, "ResourcePanel", new Vector2(20f, 20f), new Vector2(240f, 92f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _resourceLabel = CreateTMP(resourcePanel.transform, "Resource", new Vector2(16f, -16f), new Vector2(208f, 58f), 26, FontStyles.Bold);

            var weaponPanel = CreatePanel(canvasGO.transform, "WeaponPanel", new Vector2(276f, 20f), new Vector2(420f, 120f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _weaponLabel = CreateTMP(weaponPanel.transform, "WeaponSummary", new Vector2(16f, -14f), new Vector2(180f, 80f), 22, FontStyles.Bold);
            _swordSkillButton = CreateSmallButton(weaponPanel.transform, "Sword", new Vector2(210f, -14f), () => _controller.RequestWeaponSkill(HexWeaponType.Sword));
            _axeSkillButton = CreateSmallButton(weaponPanel.transform, "Axe", new Vector2(210f, -50f), () => _controller.RequestWeaponSkill(HexWeaponType.Axe));
            _hammerSkillButton = CreateSmallButton(weaponPanel.transform, "Hammer", new Vector2(210f, -86f), () => _controller.RequestWeaponSkill(HexWeaponType.Hammer));

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

            var drawPanel = CreatePanel(canvasGO.transform, "DrawPile", new Vector2(20f, 132f), new Vector2(170f, 112f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _drawPileButton = drawPanel.gameObject.AddComponent<Button>();
            _drawPileButton.onClick.AddListener(() => OpenPileView("Draw Pile", _controller.GetLocalDrawPile()));
            _drawPileLabel = CreateTMP(drawPanel.transform, "DrawLabel", new Vector2(0f, 0f), new Vector2(170f, 112f), 24, FontStyles.Bold);
            _drawPileLabel.alignment = TextAlignmentOptions.Center;

            var discardPanel = CreatePanel(canvasGO.transform, "DiscardPile", new Vector2(-20f, 132f), new Vector2(170f, 112f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _discardPileButton = discardPanel.gameObject.AddComponent<Button>();
            _discardPileButton.onClick.AddListener(() => OpenPileView("Discard Pile", _controller.GetLocalDiscardPile()));
            _discardPileLabel = CreateTMP(discardPanel.transform, "DiscardLabel", new Vector2(0f, 0f), new Vector2(170f, 112f), 24, FontStyles.Bold);
            _discardPileLabel.alignment = TextAlignmentOptions.Center;

            BuildPileModal(canvasGO.transform);
        }

        private static void CreateCardFace(Transform parent, HexCardInstance card, int displayedCost)
        {
            var rect = parent as RectTransform;
            float width = rect != null ? rect.sizeDelta.x : 182f;
            var title = CreateTMP(parent, "Title", new Vector2(16f, -14f), new Vector2(150f, 36f), 24, FontStyles.Bold);
            title.text = card.definition.displayName;

            var costBadge = CreatePanel(parent, "CostBadge", new Vector2(14f, -54f), new Vector2(48f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            costBadge.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var costText = CreateTMP(costBadge.transform, "Cost", new Vector2(0f, 0f), new Vector2(48f, 48f), 28, FontStyles.Bold);
            costText.alignment = TextAlignmentOptions.Center;
            costText.text = card.definition.energyCost < 0 ? $"X({displayedCost})" : displayedCost.ToString();

            var typeText = CreateTMP(parent, "Type", new Vector2(72f, -58f), new Vector2(90f, 26f), 18, FontStyles.Bold);
            typeText.text = card.definition.cardType.ToString();

            var body = CreateTMP(parent, "Body", new Vector2(16f, -112f), new Vector2(Mathf.Max(150f, width - 32f), 92f), 20, FontStyles.Normal);
            body.text = string.IsNullOrWhiteSpace(card.definition.description)
                ? card.definition.effectType == HexCardEffectType.Attack
                    ? $"Deal {card.definition.amount} damage"
                    : $"Gain {card.definition.amount} armor"
                : card.definition.description;

            if (card.definition.targetType != HexCardTargetType.Self && card.definition.range > 0)
            {
                var rangeBadge = CreatePanel(parent, "RangeBadge", new Vector2(14f, -14f), new Vector2(38f, 38f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
                rangeBadge.GetComponent<Image>().color = new Color(0.14f, 0.18f, 0.24f, 0.96f);
                var rangeText = CreateTMP(rangeBadge.transform, "RangeText", new Vector2(0f, 0f), new Vector2(38f, 38f), 24, FontStyles.Bold);
                rangeText.alignment = TextAlignmentOptions.Center;
                rangeText.text = card.definition.castRange.ToString();
            }
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
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private Button CreateSmallButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            var panel = CreatePanel(parent, $"{label}_Skill", anchoredPosition, new Vector2(190f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var text = CreateTMP(panel.transform, "Label", new Vector2(0f, 0f), new Vector2(190f, 30f), 18f, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemGO);
        }

        private void BuildPileModal(Transform parent)
        {
            _pileModal = CreatePanel(parent, "PileModal", Vector2.zero, new Vector2(1380f, 760f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _pileModal.gameObject.SetActive(false);

            _pileModalTitle = CreateTMP(_pileModal.transform, "Title", new Vector2(36f, -28f), new Vector2(1100f, 40f), 30, FontStyles.Bold);

            var scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot.transform.SetParent(_pileModal.transform, false);
            var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.5f, 1f);
            scrollRectTransform.anchorMax = new Vector2(0.5f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, -92f);
            scrollRectTransform.sizeDelta = new Vector2(1260f, 560f);
            scrollRoot.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.7f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            _pileModalContent = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _pileModalContent.SetParent(viewport.transform, false);
            _pileModalContent.anchorMin = new Vector2(0f, 1f);
            _pileModalContent.anchorMax = new Vector2(1f, 1f);
            _pileModalContent.pivot = new Vector2(0.5f, 1f);
            _pileModalContent.anchoredPosition = Vector2.zero;
            _pileModalContent.sizeDelta = Vector2.zero;

            var grid = _pileModalContent.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 238f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;

            var fitter = _pileModalContent.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollRoot.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = _pileModalContent;
            scrollRect.horizontal = false;

            var closePanel = CreatePanel(_pileModal.transform, "CloseButton", new Vector2(0f, 28f), new Vector2(180f, 64f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var closeButton = closePanel.gameObject.AddComponent<Button>();
            closeButton.onClick.AddListener(() => _pileModal.gameObject.SetActive(false));
            var closeText = CreateTMP(closePanel.transform, "CloseLabel", new Vector2(0f, 0f), new Vector2(180f, 64f), 24, FontStyles.Bold);
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.text = "Close";
        }

        private void OpenPileView(string title, IReadOnlyList<HexCardInstance> cards)
        {
            if (_pileModal == null)
                return;

            _pileModal.gameObject.SetActive(true);
            _pileModalTitle.text = title;
            ClearPileModalCards();
            if (cards == null || cards.Count == 0)
            {
                var emptyCard = CreatePileCardView(_pileModalContent, null);
                var emptyText = emptyCard.GetComponentInChildren<TextMeshProUGUI>();
                if (emptyText != null)
                {
                    emptyText.text = "(Empty)";
                    emptyText.alignment = TextAlignmentOptions.Center;
                }
                return;
            }

            for (int i = cards.Count - 1; i >= 0; i--)
                CreatePileCardView(_pileModalContent, cards[i]);
        }

        private void ClearPileModalCards()
        {
            if (_pileModalContent == null)
                return;

            for (int i = _pileModalContent.childCount - 1; i >= 0; i--)
                Destroy(_pileModalContent.GetChild(i).gameObject);
        }

        private GameObject CreatePileCardView(Transform parent, HexCardInstance card)
        {
            var holder = new GameObject($"{card?.runtimeId ?? "EmptyCard"}_Holder", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var holderRect = holder.GetComponent<RectTransform>();
            holderRect.sizeDelta = new Vector2(180f, 238f);
            var holderLayout = holder.GetComponent<LayoutElement>();
            holderLayout.preferredWidth = 180f;
            holderLayout.preferredHeight = 238f;
            holderLayout.minWidth = 180f;
            holderLayout.minHeight = 238f;

            var cardGO = new GameObject(card?.runtimeId ?? "EmptyCard", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(holder.transform, false);
            var rect = cardGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(180f, 238f);
            var image = cardGO.GetComponent<Image>();
            image.color = card != null ? Color.Lerp(card.definition.color, Color.black, 0.12f) : new Color(0.16f, 0.18f, 0.22f, 0.95f);

            if (card == null)
            {
                var empty = CreateTMP(cardGO.transform, "Empty", new Vector2(0f, 0f), new Vector2(180f, 238f), 28, FontStyles.Bold);
                empty.alignment = TextAlignmentOptions.Center;
                return holder;
            }

            CreateCardFace(cardGO.transform, card, card.definition.energyCost < 0 ? 0 : card.definition.energyCost);
            return holder;
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
            if (_controller == null)
                return;

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
            if (_controller == null)
                return;

            _rectTransform.position = eventData.position;
            _controller.UpdateDraggedCard(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller == null)
            {
                Object.Destroy(gameObject);
                return;
            }

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
