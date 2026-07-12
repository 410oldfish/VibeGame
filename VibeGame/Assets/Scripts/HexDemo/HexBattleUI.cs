using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexDemo
{
    public enum HexFloatingFeedbackKind
    {
        Armor,
        ArmorDamage,
        HealthDamage,
        Blocked,
    }

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
        private const string BattleHudCanvasPrefabPath = "Assets/Prefabs/UI/Battle/BattleHudCanvas.prefab";
        private const string BattlePanelDir = "Assets/Prefabs/UI/Battle/Panels/";
        private const string CardViewItemPrefabPath = "Assets/Prefabs/UI/Battle/CardViewItem.prefab";
        private const string ResourcesBattleDir = "UI/Battle/";
        private const string ResourcesBattlePanelsDir = "UI/Battle/Panels/";

        private static readonly string[] BattlePanelNames =
        {
            "HUD",
            "ResourcePanel",
            "HandPanel",
            "ActionPanel",
            "DrawPile",
            "DiscardPile",
            "ExhaustPile",
            "PlayLog",
            "PileModal",
            "EnemyHandOverlay",
            "PlayLogModal",
        };
        private HexBattleController _controller;
        private readonly List<HexCardView> _cardViews = new();
        private RectTransform _handRoot;
        private TextMeshProUGUI _turnLabel;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _playerStripLabel;
        private HexStatusIconBar _playerStatusBar;
        private RectTransform _enemyIntentPanel;
        private readonly List<HexEnemyIntentRow> _enemyIntentRows = new();
        private TextMeshProUGUI _deckLabel;
        private TextMeshProUGUI _resourceLabel;
        private Button _endTurnButton;
        private Button _drawPileButton;
        private Button _discardPileButton;
        private Button _exhaustPileButton;
        private Button _playLogButton;
        private TextMeshProUGUI _drawPileLabel;
        private TextMeshProUGUI _discardPileLabel;
        private TextMeshProUGUI _exhaustPileLabel;
        private RectTransform _pileModal;
        private TextMeshProUGUI _pileModalTitle;
        private RectTransform _pileModalContent;
        private RectTransform _enemyHandOverlay;
        private RectTransform _enemyHandPopup;
        private TextMeshProUGUI _enemyHandTitle;
        private RectTransform _enemyHandContent;
        private RectTransform _terrainDetailOverlay;
        private RectTransform _terrainDetailPopup;
        private TextMeshProUGUI _terrainDetailTitle;
        private TextMeshProUGUI _terrainDetailBody;
        private RectTransform _playLogModal;
        private RectTransform _playLogContent;
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

            var snapshot = _controller.GetBattleHudSnapshot();
            _turnLabel.text = snapshot.phaseLabel;
            if (_playerStripLabel != null)
            {
                _playerStripLabel.text =
                    $"生命 {snapshot.player.currentHealth}/{snapshot.player.maxHealth}  护甲 {snapshot.player.armor}  能量 {snapshot.player.energy}/{snapshot.player.maxEnergy}  力量 {snapshot.player.power}";
            }

            if (_playerStatusBar != null)
                _playerStatusBar.Refresh(snapshot.player.statuses);

            if (_statusLabel != null)
            {
                _statusLabel.text = string.Empty;
                _statusLabel.gameObject.SetActive(_playerStripLabel == null);
            }

            RefreshEnemyIntentRows(snapshot);
            _deckLabel.text = _controller.GetDeckSummary();
            _resourceLabel.text = _controller.GetResourceSummary();
            _endTurnButton.interactable = snapshot.canEndTurn;
            _drawPileLabel.text = $"抽牌\n{snapshot.piles.draw}";
            _discardPileLabel.text = $"弃牌\n{snapshot.piles.discard}";
            if (_exhaustPileLabel != null)
                _exhaustPileLabel.text = $"消耗\n{snapshot.piles.exhaust}";
            RebuildHand();
        }

        private void RefreshEnemyIntentRows(BattleHudSnapshot snapshot)
        {
            if (_enemyIntentPanel == null)
                return;

            if (_enemyIntentRows.Count == 0)
            {
                for (int i = 0; i < _enemyIntentPanel.childCount; i++)
                {
                    var row = _enemyIntentPanel.GetChild(i).GetComponent<HexEnemyIntentRow>();
                    if (row != null)
                        _enemyIntentRows.Add(row);
                }
            }

            float y = 0f;
            for (int i = 0; i < _enemyIntentRows.Count; i++)
            {
                bool active = i < snapshot.enemies.Count;
                _enemyIntentRows[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                var rowRect = _enemyIntentRows[i].GetComponent<RectTransform>();
                rowRect.anchoredPosition = new Vector2(0f, y);
                y -= 112f;
                _enemyIntentRows[i].Refresh(snapshot.enemies[i]);
            }

            _enemyIntentPanel.sizeDelta = new Vector2(_enemyIntentPanel.sizeDelta.x, Mathf.Max(112f, -y));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseTopModal();
        }

        private void RebuildHand()
        {
            for (int i = _cardViews.Count - 1; i >= 0; i--)
                Destroy(_cardViews[i].gameObject);

            _cardViews.Clear();

            var hand = _controller.GetLocalHand();
            for (int i = 0; i < hand.Count; i++)
            {
                var cardGO = CreateCardRoot($"Card_{i}");
                cardGO.transform.SetParent(_handRoot, false);

                var image = cardGO.GetComponent<Image>();
                image.color = Color.Lerp(hand[i].definition.color, Color.black, 0.12f);
                image.raycastTarget = true;

                CreateCardFace(cardGO.transform, hand[i], _controller.GetLocalCardCost(hand[i]));

                var view = cardGO.GetComponent<HexCardView>();
                view.Initialize(_controller, hand[i], _canvas);
                _cardViews.Add(view);
            }
        }

        private GameObject CreateCardRoot(string name)
        {
            var prefab = LoadBattlePrefab("CardViewItem");
#if UNITY_EDITOR
            if (prefab == null)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardViewItemPrefabPath);
#endif
            if (prefab == null)
            {
                Debug.LogError("Missing CardViewItem prefab. Battle UI is prefab-only and cannot fallback to procedural card root.");
                return new GameObject(name);
            }

            var card = Instantiate(prefab);
            card.name = name;
            if (card.GetComponent<RectTransform>() == null ||
                card.GetComponent<Image>() == null ||
                card.GetComponent<CanvasGroup>() == null ||
                card.GetComponent<HexCardView>() == null)
            {
                Debug.LogError("CardViewItem prefab is missing required components (RectTransform/Image/CanvasGroup/HexCardView).");
            }
            else
            {
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardWidth, CardHeight);
            }

            return card;
        }

        private void BuildCanvas()
        {
            if (!TryBuildCanvasFromPrefab())
                Debug.LogError("Battle UI is prefab-only. Failed to load BattleHudCanvas and required panels from prefabs.");
        }

        private void EnsureStructuredHud(Transform canvasRoot)
        {
            var hud = canvasRoot.Find("HUD");
            if (hud != null)
            {
                if (_playerStripLabel == null)
                    _playerStripLabel = hud.Find("PlayerStrip")?.GetComponent<TextMeshProUGUI>();

                if (_playerStatusBar == null)
                {
                    _playerStatusBar = hud.GetComponent<HexStatusIconBar>();
                    if (_playerStatusBar != null)
                    {
                        _playerStatusBar.EnsureBuilt(hud);
                        var statusRect = _playerStatusBar.Root;
                        statusRect.anchoredPosition = new Vector2(18f, -78f);
                        statusRect.sizeDelta = new Vector2(420f, 28f);
                    }
                }
            }

            if (_enemyIntentPanel == null)
            {
                _enemyIntentPanel = FindByPath<RectTransform>(canvasRoot, "EnemyIntentPanel");
            }

            if (_exhaustPileButton == null)
            {
                var exhaust = canvasRoot.Find("ExhaustPile");
                if (exhaust != null)
                {
                    _exhaustPileButton = exhaust.GetComponent<Button>() ?? exhaust.gameObject.AddComponent<Button>();
                    _exhaustPileLabel = exhaust.Find("ExhaustLabel")?.GetComponent<TextMeshProUGUI>();
                }
            }

            EnsureTerrainDetailPopup(canvasRoot);
        }

        private static GameObject LoadBattlePrefab(string assetName)
        {
            var fromResources = Resources.Load<GameObject>(ResourcesBattlePanelsDir + assetName);
            if (fromResources != null)
                return fromResources;

            fromResources = Resources.Load<GameObject>(ResourcesBattleDir + assetName);
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            if (assetName == "BattleHudCanvas")
                return AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudCanvasPrefabPath);

            return AssetDatabase.LoadAssetAtPath<GameObject>(BattlePanelDir + assetName + ".prefab");
#else
            return null;
#endif
        }

        private bool TryBuildCanvasFromPrefab()
        {
            var canvasPrefab = LoadBattlePrefab("BattleHudCanvas");
            if (canvasPrefab == null)
                return false;

            var canvasGO = Instantiate(canvasPrefab, transform, false);
            canvasGO.name = "BattleHUD_Canvas";
            _canvas = canvasGO.GetComponent<Canvas>();
            if (_canvas == null)
            {
                Destroy(canvasGO);
                _canvas = null;
                return false;
            }

            for (int i = 0; i < BattlePanelNames.Length; i++)
            {
                if (InstantiateBattlePanel(BattlePanelNames[i], canvasGO.transform) == null)
                {
                    Destroy(canvasGO);
                    _canvas = null;
                    return false;
                }
            }

            _turnLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "HUD/Turn");
            _statusLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "HUD/Status");
            _deckLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "HUD/Deck");
            _resourceLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "ResourcePanel/Resource");
            _endTurnButton = FindByPath<Button>(canvasGO.transform, "ActionPanel");
            _drawPileButton = FindByPath<Button>(canvasGO.transform, "DrawPile");
            _discardPileButton = FindByPath<Button>(canvasGO.transform, "DiscardPile");
            _exhaustPileButton = FindByPath<Button>(canvasGO.transform, "ExhaustPile");
            _playLogButton = FindByPath<Button>(canvasGO.transform, "PlayLog");
            _drawPileLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "DrawPile/DrawLabel");
            _discardPileLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "DiscardPile/DiscardLabel");
            _exhaustPileLabel = FindByPath<TextMeshProUGUI>(canvasGO.transform, "ExhaustPile/ExhaustLabel");
            _handRoot = FindByPath<RectTransform>(canvasGO.transform, "HandPanel/HandRoot");

            _pileModal = FindByPath<RectTransform>(canvasGO.transform, "PileModal");
            _pileModalTitle = FindByPath<TextMeshProUGUI>(canvasGO.transform, "PileModal/Title");
            _pileModalContent = FindByPath<RectTransform>(canvasGO.transform, "PileModal/ScrollRoot/Viewport/Content");
            _enemyHandOverlay = FindByPath<RectTransform>(canvasGO.transform, "EnemyHandOverlay");
            _enemyHandPopup = FindByPath<RectTransform>(canvasGO.transform, "EnemyHandOverlay/EnemyHandPopup");
            _enemyHandTitle = FindByPath<TextMeshProUGUI>(canvasGO.transform, "EnemyHandOverlay/EnemyHandPopup/Title");
            _enemyHandContent = FindByPath<RectTransform>(canvasGO.transform, "EnemyHandOverlay/EnemyHandPopup/Content");
            _playLogModal = FindByPath<RectTransform>(canvasGO.transform, "PlayLogModal");
            _playLogContent = FindByPath<RectTransform>(canvasGO.transform, "PlayLogModal/ScrollRoot/Viewport/Content");

            if (!ValidateCoreReferences())
            {
                Destroy(canvasGO);
                _canvas = null;
                return false;
            }

            EnsureStructuredHud(canvasGO.transform);
            if (_enemyIntentPanel == null)
                Debug.LogWarning("EnemyIntentPanel not found in battle prefabs. Enemy intent row UI will be skipped.");
            if (_playerStatusBar == null)
                Debug.LogWarning("HexStatusIconBar component is missing on HUD prefab. Player status icons will be skipped.");
            if (_exhaustPileButton == null || _exhaustPileLabel == null)
                Debug.LogWarning("ExhaustPile prefab block is missing. Exhaust pile button will be skipped.");
            WireButtonActions();
            return true;
        }

        private GameObject InstantiateBattlePanel(string panelName, Transform parent)
        {
            var prefab = LoadBattlePrefab(panelName);
            if (prefab == null)
                return null;

            var go = Instantiate(prefab, parent, false);
            go.name = panelName;
            return go;
        }

        private static T FindByPath<T>(Transform root, string path) where T : Component
        {
            var t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        private bool ValidateCoreReferences()
        {
            return _turnLabel != null &&
                   _deckLabel != null &&
                   _resourceLabel != null &&
                   _endTurnButton != null &&
                   _drawPileButton != null &&
                   _discardPileButton != null &&
                   _playLogButton != null &&
                   _drawPileLabel != null &&
                   _discardPileLabel != null &&
                   _handRoot != null &&
                   _pileModal != null &&
                   _pileModalTitle != null &&
                   _pileModalContent != null &&
                   _enemyHandOverlay != null &&
                   _enemyHandPopup != null &&
                   _enemyHandTitle != null &&
                   _enemyHandContent != null &&
                   _playLogModal != null &&
                   _playLogContent != null;
        }

        private void WireButtonActions()
        {
            _endTurnButton.onClick.RemoveAllListeners();
            _endTurnButton.onClick.AddListener(_controller.RequestEndTurn);

            _drawPileButton.onClick.RemoveAllListeners();
            _drawPileButton.onClick.AddListener(() => OpenPileView("抽牌堆", _controller.GetLocalDrawPile()));
            _discardPileButton.onClick.RemoveAllListeners();
            _discardPileButton.onClick.AddListener(() => OpenPileView("弃牌堆", _controller.GetLocalDiscardPile()));
            if (_exhaustPileButton != null)
            {
                _exhaustPileButton.onClick.RemoveAllListeners();
                _exhaustPileButton.onClick.AddListener(() => OpenPileView("消耗堆", _controller.GetLocalExhaustPile()));
            }
            _playLogButton.onClick.RemoveAllListeners();
            _playLogButton.onClick.AddListener(OpenPlayLogView);

            var enemyOverlayButton = _enemyHandOverlay.GetComponent<Button>();
            if (enemyOverlayButton != null)
            {
                enemyOverlayButton.onClick.RemoveAllListeners();
                enemyOverlayButton.onClick.AddListener(CloseEnemyHandPopup);
            }
            _enemyHandOverlay.gameObject.SetActive(false);

            var pileClose = FindByPath<Button>(_pileModal, "CloseButton");
            if (pileClose != null)
            {
                pileClose.onClick.RemoveAllListeners();
                pileClose.onClick.AddListener(() => _pileModal.gameObject.SetActive(false));
            }

            var playLogClose = FindByPath<Button>(_playLogModal, "CloseButton");
            if (playLogClose != null)
            {
                playLogClose.onClick.RemoveAllListeners();
                playLogClose.onClick.AddListener(() => _playLogModal.gameObject.SetActive(false));
            }

            _pileModal.gameObject.SetActive(false);
            _playLogModal.gameObject.SetActive(false);
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

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemGO);
        }

        public bool IsEnemyIntentPopupOpen()
        {
            return _enemyHandOverlay != null && _enemyHandOverlay.gameObject.activeSelf;
        }

        public void OpenEnemyHandPopup(HexBattleUnit enemy, Vector2 screenPosition)
        {
            if (_enemyHandOverlay == null || _enemyHandPopup == null || _controller == null || enemy == null)
                return;

            _enemyHandOverlay.gameObject.SetActive(true);
            _enemyHandTitle.text = $"{GetUnitDisplayName(enemy)} 手牌";
            ClearChildren(_enemyHandContent);

            var cards = _controller.GetEnemyHand(enemy);
            if (cards == null || cards.Count == 0)
            {
                var empty = CreateTMP(_enemyHandContent, "Empty", Vector2.zero, new Vector2(540f, 220f), 26, FontStyles.Bold);
                empty.alignment = TextAlignmentOptions.Center;
                empty.text = "(Empty)";
            }
            else
            {
                for (int i = 0; i < cards.Count; i++)
                    CreatePileCardView(_enemyHandContent, cards[i]);
            }

            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPoint))
            {
                Vector2 size = _enemyHandPopup.sizeDelta;
                Rect rect = canvasRect.rect;
                localPoint.x = Mathf.Clamp(localPoint.x + 18f, rect.xMin + 16f, rect.xMax - size.x - 16f);
                localPoint.y = Mathf.Clamp(localPoint.y - 18f, rect.yMin + size.y + 16f, rect.yMax - 16f);
                _enemyHandPopup.anchoredPosition = localPoint;
            }
        }

        public bool IsBlockingWorldClick()
        {
            return (_enemyHandOverlay != null && _enemyHandOverlay.gameObject.activeSelf) ||
                   (_terrainDetailOverlay != null && _terrainDetailOverlay.gameObject.activeSelf) ||
                   (_pileModal != null && _pileModal.gameObject.activeSelf) ||
                   (_playLogModal != null && _playLogModal.gameObject.activeSelf);
        }

        public void OpenTerrainDetailPopup(HexTile tile, Vector2 screenPosition)
        {
            if (_canvas == null || tile == null)
                return;

            EnsureTerrainDetailPopup(_canvas.transform);
            if (_terrainDetailOverlay == null || _terrainDetailPopup == null)
                return;

            _terrainDetailOverlay.gameObject.SetActive(true);
            _terrainDetailTitle.text = BuildTerrainDetailTitle(tile);
            _terrainDetailBody.text = BuildTerrainDetailBody(tile);

            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPoint))
            {
                Vector2 size = _terrainDetailPopup.sizeDelta;
                Rect rect = canvasRect.rect;
                localPoint.x = Mathf.Clamp(localPoint.x + 18f, rect.xMin + 16f, rect.xMax - size.x - 16f);
                localPoint.y = Mathf.Clamp(localPoint.y - 18f, rect.yMin + size.y + 16f, rect.yMax - 16f);
                _terrainDetailPopup.anchoredPosition = localPoint;
            }
        }

        public void CloseTerrainDetailPopup()
        {
            if (_terrainDetailOverlay != null)
                _terrainDetailOverlay.gameObject.SetActive(false);
        }

        public void CloseEnemyHandPopup()
        {
            if (_enemyHandOverlay != null)
                _enemyHandOverlay.gameObject.SetActive(false);
        }

        public void CloseTopModal()
        {
            if (_terrainDetailOverlay != null && _terrainDetailOverlay.gameObject.activeSelf)
            {
                CloseTerrainDetailPopup();
                return;
            }

            if (_enemyHandOverlay != null && _enemyHandOverlay.gameObject.activeSelf)
            {
                CloseEnemyHandPopup();
                return;
            }

            if (_pileModal != null && _pileModal.gameObject.activeSelf)
            {
                _pileModal.gameObject.SetActive(false);
                return;
            }

            if (_playLogModal != null && _playLogModal.gameObject.activeSelf)
                _playLogModal.gameObject.SetActive(false);
        }

        public void ShowFloatingCombatText(HexBattleUnit unit, HexFloatingFeedbackKind kind, int amount)
        {
            if (_canvas == null || unit == null)
                return;

            StartCoroutine(AnimateFloatingCombatText(unit, kind, amount));
        }

        public void ShowPlayedCard(HexBattleUnit source, HexCardInstance card)
        {
            if (_canvas == null || source == null || card?.definition == null)
                return;

            StartCoroutine(AnimatePlayedCard(source, card));
        }

        private IEnumerator AnimateFloatingCombatText(HexBattleUnit unit, HexFloatingFeedbackKind kind, int amount)
        {
            var root = new GameObject("FloatingCombatText", typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(_canvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(176f, 48f);
            rect.anchoredPosition = WorldToCanvasPosition(unit.GetTargetPoint() + Vector3.up * 0.35f);

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 8f;

            Color iconColor;
            string iconText;
            string valueText;
            switch (kind)
            {
                case HexFloatingFeedbackKind.Armor:
                    iconText = "盾";
                    valueText = $"+{amount}";
                    iconColor = new Color(0.3f, 0.62f, 1f, 0.95f);
                    break;
                case HexFloatingFeedbackKind.ArmorDamage:
                    iconText = "盾";
                    valueText = $"-{amount}";
                    iconColor = new Color(0.34f, 0.55f, 0.82f, 0.95f);
                    break;
                case HexFloatingFeedbackKind.Blocked:
                    iconText = "盾";
                    valueText = "0";
                    iconColor = new Color(0.7f, 0.74f, 0.82f, 0.95f);
                    break;
                default:
                    iconText = "HP";
                    valueText = $"-{amount}";
                    iconColor = new Color(0.88f, 0.22f, 0.18f, 0.95f);
                    break;
            }

            var iconPanel = CreatePanel(root.transform, "Icon", Vector2.zero, new Vector2(42f, 42f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            iconPanel.GetComponent<Image>().color = iconColor;
            var iconLabel = CreateTMP(iconPanel.transform, "IconLabel", Vector2.zero, new Vector2(42f, 42f), 18, FontStyles.Bold);
            iconLabel.alignment = TextAlignmentOptions.Center;
            iconLabel.text = iconText;

            var value = CreateTMP(root.transform, "Value", Vector2.zero, new Vector2(96f, 44f), 34, FontStyles.Bold);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            value.color = kind == HexFloatingFeedbackKind.HealthDamage
                ? new Color(1f, 0.34f, 0.28f, 1f)
                : Color.white;
            value.text = valueText;

            yield return AnimateCanvasGroup(root.GetComponent<CanvasGroup>(), rect, Vector2.up * 74f, 0.85f);
        }

        private IEnumerator AnimatePlayedCard(HexBattleUnit source, HexCardInstance card)
        {
            var cardGO = new GameObject("PlayedCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            cardGO.transform.SetParent(_canvas.transform, false);
            var rect = cardGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rect.localScale = Vector3.one * 0.62f;
            rect.anchoredPosition = WorldToCanvasPosition(source.GetTargetPoint()) + new Vector2(92f, 4f);

            var image = cardGO.GetComponent<Image>();
            image.color = Color.Lerp(card.definition.color, Color.black, 0.12f);
            image.raycastTarget = false;
            CreateCardFace(cardGO.transform, card, card.definition.energyCost < 0 ? 0 : card.definition.energyCost);

            yield return AnimateCanvasGroup(cardGO.GetComponent<CanvasGroup>(), rect, Vector2.up * 92f, 1.05f);
        }

        private IEnumerator AnimateCanvasGroup(CanvasGroup group, RectTransform rect, Vector2 offset, float duration)
        {
            Vector2 start = rect.anchoredPosition;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(start, start + offset, t);
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            Destroy(rect.gameObject);
        }

        private Vector2 WorldToCanvasPosition(Vector3 worldPoint)
        {
            Camera camera = _controller != null && _controller.rayCamera != null ? _controller.rayCamera : Camera.main;
            Vector2 screenPoint = camera != null ? RectTransformUtility.WorldToScreenPoint(camera, worldPoint) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var localPoint))
                return localPoint;

            return Vector2.zero;
        }

        private void BuildEnemyHandPopup(Transform parent)
        {
            _enemyHandOverlay = new GameObject("EnemyHandOverlay", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            _enemyHandOverlay.SetParent(parent, false);
            _enemyHandOverlay.anchorMin = Vector2.zero;
            _enemyHandOverlay.anchorMax = Vector2.one;
            _enemyHandOverlay.offsetMin = Vector2.zero;
            _enemyHandOverlay.offsetMax = Vector2.zero;
            _enemyHandOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            _enemyHandOverlay.GetComponent<Button>().onClick.AddListener(CloseEnemyHandPopup);
            _enemyHandOverlay.gameObject.SetActive(false);

            _enemyHandPopup = CreatePanel(_enemyHandOverlay, "EnemyHandPopup", Vector2.zero, new Vector2(660f, 330f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            _enemyHandPopup.gameObject.AddComponent<Button>();
            _enemyHandTitle = CreateTMP(_enemyHandPopup.transform, "Title", new Vector2(22f, -16f), new Vector2(600f, 34f), 26, FontStyles.Bold);

            _enemyHandContent = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _enemyHandContent.SetParent(_enemyHandPopup.transform, false);
            _enemyHandContent.anchorMin = new Vector2(0f, 1f);
            _enemyHandContent.anchorMax = new Vector2(0f, 1f);
            _enemyHandContent.pivot = new Vector2(0f, 1f);
            _enemyHandContent.anchoredPosition = new Vector2(22f, -66f);
            _enemyHandContent.sizeDelta = new Vector2(616f, 244f);

            var layout = _enemyHandContent.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
        }

        private void EnsureTerrainDetailPopup(Transform parent)
        {
            if (_terrainDetailOverlay != null || parent == null)
                return;

            _terrainDetailOverlay = new GameObject("TerrainDetailOverlay", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            _terrainDetailOverlay.SetParent(parent, false);
            _terrainDetailOverlay.anchorMin = Vector2.zero;
            _terrainDetailOverlay.anchorMax = Vector2.one;
            _terrainDetailOverlay.offsetMin = Vector2.zero;
            _terrainDetailOverlay.offsetMax = Vector2.zero;
            _terrainDetailOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            _terrainDetailOverlay.GetComponent<Button>().onClick.AddListener(CloseTerrainDetailPopup);
            _terrainDetailOverlay.gameObject.SetActive(false);

            _terrainDetailPopup = CreatePanel(_terrainDetailOverlay, "TerrainDetailPopup", Vector2.zero, new Vector2(420f, 320f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            _terrainDetailPopup.gameObject.AddComponent<Button>();
            _terrainDetailTitle = CreateTMP(_terrainDetailPopup.transform, "Title", new Vector2(18f, -14f), new Vector2(380f, 34f), 24, FontStyles.Bold);
            _terrainDetailBody = CreateTMP(_terrainDetailPopup.transform, "Body", new Vector2(18f, -56f), new Vector2(380f, 240f), 18, FontStyles.Normal);
            _terrainDetailBody.alignment = TextAlignmentOptions.TopLeft;
            _terrainDetailBody.textWrappingMode = TextWrappingModes.Normal;
        }

        private static string BuildTerrainDetailTitle(HexTile tile)
        {
            if (tile == null)
                return "地形";

            var def = HexPropLibrary.Get(tile.propId);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;

            if (tile.HasBarrier)
                return "障碍";
            if (tile.HasRuin)
                return "残骸";
            if (tile.zone == HexTerrainZoneType.Pit)
                return "深坑";
            return "地形";
        }

        private static string BuildTerrainDetailBody(HexTile tile)
        {
            if (tile == null)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"坐标 ({tile.coord.q},{tile.coord.r})");
            sb.AppendLine($"Zone：{DescribeZone(tile.zone)}");

            if (tile.HasBarrier)
                sb.AppendLine("构筑物：障碍 Barrier");
            else if (tile.HasRuin)
                sb.AppendLine("构筑物：残骸 Ruin");
            else
                sb.AppendLine("构筑物：无");

            sb.AppendLine($"通行：{(tile.BlocksMovement ? "不可进入" : "可进入")}");
            sb.AppendLine($"视线：{(tile.BlocksLineOfSight ? "阻挡 LOS" : "不挡 LOS")}");

            if (tile.HasRuin)
                sb.AppendLine($"HP：{tile.structureHp}/{Mathf.Max(tile.structureMaxHp, tile.structureHp)}");

            var def = HexPropLibrary.Get(tile.propId);
            if (def != null)
            {
                sb.AppendLine($"propId：{def.propId}");
                sb.AppendLine($"破坏：{DescribeDestroyBy(def.destroyBy)}");
                if (!string.IsNullOrWhiteSpace(def.description))
                    sb.AppendLine(def.description);

                if (def.onRemoveEffects != null && def.onRemoveEffects.Count > 0)
                {
                    sb.AppendLine("移除效果：");
                    for (int i = 0; i < def.onRemoveEffects.Count; i++)
                    {
                        var effect = def.onRemoveEffects[i];
                        if (effect == null)
                            continue;
                        sb.AppendLine($"- {effect.type}: {effect.summary}");
                    }
                }

                if (def.fuseTurns.HasValue)
                    sb.AppendLine($"引信：{def.fuseTurns.Value} 回合（armed={(tile.Model != null && tile.Model.fuseArmed)})");
                if (def.adjacentAura != null && !string.IsNullOrWhiteSpace(def.adjacentAura.summary))
                    sb.AppendLine($"光环：{def.adjacentAura.summary}");
                if (def.postBattleReward)
                    sb.AppendLine("战后奖励：是");
            }
            else if (tile.zone == HexTerrainZoneType.Pit)
            {
                sb.AppendLine("深坑为地面属性 Zone，不是构筑物。击退落入可造成高额伤害（Content）。");
            }

            return sb.ToString().TrimEnd();
        }

        private static string DescribeZone(HexTerrainZoneType zone)
        {
            return zone switch
            {
                HexTerrainZoneType.Pit => "深坑 Pit",
                _ => "普通 Normal",
            };
        }

        private static string DescribeDestroyBy(HexPropDestroyBy destroyBy)
        {
            return destroyBy switch
            {
                HexPropDestroyBy.NormalAttack => "普通攻击可破坏",
                HexPropDestroyBy.Both => "普通攻击或特殊行动",
                _ => "仅特殊破障",
            };
        }

        private void BuildPlayLogModal(Transform parent)
        {
            _playLogModal = CreatePanel(parent, "PlayLogModal", Vector2.zero, new Vector2(920f, 640f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _playLogModal.gameObject.SetActive(false);
            var title = CreateTMP(_playLogModal.transform, "Title", new Vector2(32f, -24f), new Vector2(720f, 40f), 30, FontStyles.Bold);
            title.text = "出牌回放";

            var scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollRoot.transform.SetParent(_playLogModal.transform, false);
            var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.5f, 1f);
            scrollRectTransform.anchorMax = new Vector2(0.5f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, -82f);
            scrollRectTransform.sizeDelta = new Vector2(840f, 450f);
            scrollRoot.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.72f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            _playLogContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _playLogContent.SetParent(viewport.transform, false);
            _playLogContent.anchorMin = new Vector2(0f, 1f);
            _playLogContent.anchorMax = new Vector2(1f, 1f);
            _playLogContent.pivot = new Vector2(0.5f, 1f);
            _playLogContent.anchoredPosition = Vector2.zero;
            _playLogContent.sizeDelta = Vector2.zero;

            var layout = _playLogContent.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = _playLogContent.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollRoot.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = _playLogContent;
            scrollRect.horizontal = false;

            var closePanel = CreatePanel(_playLogModal.transform, "CloseButton", new Vector2(0f, 28f), new Vector2(180f, 64f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var closeButton = closePanel.gameObject.AddComponent<Button>();
            closeButton.onClick.AddListener(() => _playLogModal.gameObject.SetActive(false));
            var closeText = CreateTMP(closePanel.transform, "CloseLabel", new Vector2(0f, 0f), new Vector2(180f, 64f), 24, FontStyles.Bold);
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.text = "关闭";
        }

        private void OpenPlayLogView()
        {
            if (_playLogModal == null || _controller == null)
                return;

            _playLogModal.gameObject.SetActive(true);
            ClearChildren(_playLogContent);
            var records = _controller.GetPlayLog();
            if (records == null || records.Count == 0)
            {
                CreatePlayLogLine("尚无出牌记录。");
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                CreatePlayLogLine($"{i + 1}. {record.turnOwner}  {record.sourceName} -> {record.targetName}  {record.cardName}");
            }
        }

        private void CreatePlayLogLine(string text)
        {
            var line = CreateTMP(_playLogContent, "Record", Vector2.zero, new Vector2(790f, 42f), 22, FontStyles.Normal);
            line.alignment = TextAlignmentOptions.MidlineLeft;
            line.text = text;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static string GetUnitDisplayName(HexBattleUnit unit)
        {
            if (unit?.State == null)
                return "Unknown";

            if (!string.IsNullOrWhiteSpace(unit.State.displayName))
                return unit.State.displayName;

            return unit.State.faction == HexBattleFaction.Player ? "Player" : "Enemy";
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
            closeText.text = "关闭";
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
}
