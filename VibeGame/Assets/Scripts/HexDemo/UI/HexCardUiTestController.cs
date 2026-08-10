using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HexDemo
{
    [DisallowMultipleComponent]
    public sealed class HexCardUiTestController : MonoBehaviour
    {
        private const string LayoutResource = "UI Toolkit/CardArt/WarriorCardLayout";
        private static readonly string[] RegionNames = { "费用", "名称", "描述" };

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _cardMount;
        private VisualElement _controls;
        private WarriorCardVisualElement _cardView;
        private HexCardUiLayoutSettings _sourceLayout;
        private HexCardUiLayoutSettings _workingLayout;
        private List<HexCardDefinition> _cards;

        private DropdownField _cardDropdown;
        private DropdownField _regionDropdown;
        private TextField _costField;
        private TextField _titleField;
        private TextField _descriptionField;
        private Slider _xSlider;
        private Slider _ySlider;
        private Slider _widthSlider;
        private Slider _heightSlider;
        private Slider _fontSlider;
        private Label _layoutReadout;
        private Label _saveStatus;
        private bool _syncingControls;

        private void OnEnable()
        {
            _document = HexUiToolkitRuntime.AttachDocument(gameObject, "CardPreviewRoot", 500);
            _root = _document.rootVisualElement;
            HexUiToolkitRuntime.PrepareRoot(_root);

            var previewStyle = Resources.Load<StyleSheet>("UI Toolkit/Styles/CardPreview");
            if (previewStyle != null && !_root.styleSheets.Contains(previewStyle))
                _root.styleSheets.Add(previewStyle);

            _cardMount = _root.Q<VisualElement>("card-mount");
            _controls = _root.Q<VisualElement>("controls");
            if (_cardMount == null || _controls == null)
                return;

            _sourceLayout = Resources.Load<HexCardUiLayoutSettings>(LayoutResource);
            if (_sourceLayout == null)
            {
                _sourceLayout = ScriptableObject.CreateInstance<HexCardUiLayoutSettings>();
                _sourceLayout.name = "Fallback Warrior Card Layout";
            }

            _workingLayout = _sourceLayout.CreateRuntimeCopy();
            _cards = HexCardLibrary.GetWarriorPool()
                .Where(card => card != null)
                .OrderBy(card => card.displayName)
                .ToList();

            if (_cards.Count == 0)
                _cards = HexCardLibrary.GetWarriorDesignCardsRaw().Where(card => card != null).ToList();

            _cardView = new WarriorCardVisualElement();
            _cardView.SetPreviewWidth(420f);
            _cardView.ApplyLayout(_workingLayout);
            _cardView.SetGuidesVisible(true);
            _cardMount.Add(_cardView);

            BuildControls();
            SelectCard(0);
            SelectRegion(0);
        }

        private void OnDisable()
        {
            if (_workingLayout != null)
                Destroy(_workingLayout);

            if (_sourceLayout != null && _sourceLayout.name == "Fallback Warrior Card Layout")
                Destroy(_sourceLayout);

            _workingLayout = null;
            _sourceLayout = null;
            _cards = null;
            _cardView = null;
            _root = null;
            _cardMount = null;
            _controls = null;
        }

        private void BuildControls()
        {
            _controls.Clear();

            AddHeading("卡牌内容");
            _cardDropdown = new DropdownField("战士卡牌", _cards.Select(GetCardLabel).ToList(), 0);
            _cardDropdown.name = "card-selector";
            _cardDropdown.RegisterValueChangedCallback(_ => SelectCard(_cardDropdown.index));
            _controls.Add(_cardDropdown);

            _costField = new TextField("费用");
            _costField.name = "content-cost";
            _costField.RegisterValueChangedCallback(_ => RefreshCardContent());
            _controls.Add(_costField);

            _titleField = new TextField("名称");
            _titleField.name = "content-title";
            _titleField.RegisterValueChangedCallback(_ => RefreshCardContent());
            _controls.Add(_titleField);

            _descriptionField = new TextField("描述") { multiline = true };
            _descriptionField.name = "content-description";
            _descriptionField.AddToClassList("card-test-description-field");
            _descriptionField.RegisterValueChangedCallback(_ => RefreshCardContent());
            _controls.Add(_descriptionField);

            AddHeading("预览");
            var previewWidth = CreateSlider("卡牌宽度", 280f, 620f, 420f);
            previewWidth.RegisterValueChangedCallback(evt => _cardView.SetPreviewWidth(evt.newValue));

            var guides = new Toggle("显示区域参考框") { value = true };
            guides.RegisterValueChangedCallback(evt => _cardView.SetGuidesVisible(evt.newValue));
            _controls.Add(guides);

            AddHeading("区域位置（百分比）");
            _regionDropdown = new DropdownField("当前区域", RegionNames.ToList(), 0);
            _regionDropdown.name = "region-selector";
            _regionDropdown.RegisterValueChangedCallback(_ => SelectRegion(_regionDropdown.index));
            _controls.Add(_regionDropdown);

            _xSlider = CreateLayoutSlider("X", 0f, 100f);
            _xSlider.name = "layout-x";
            _ySlider = CreateLayoutSlider("Y", 0f, 100f);
            _ySlider.name = "layout-y";
            _widthSlider = CreateLayoutSlider("宽度", 2f, 100f);
            _widthSlider.name = "layout-width";
            _heightSlider = CreateLayoutSlider("高度", 2f, 100f);
            _heightSlider.name = "layout-height";
            _fontSlider = CreateLayoutSlider("字号", 12f, 72f);
            _fontSlider.name = "layout-font-size";

            _layoutReadout = new Label();
            _layoutReadout.AddToClassList("card-test-readout");
            _controls.Add(_layoutReadout);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("card-test-button-row");

            var resetButton = new Button(ResetWorkingLayout) { text = "恢复默认布局" };
            resetButton.AddToClassList("hex-button");
            buttonRow.Add(resetButton);

            var reloadButton = new Button(ReloadLayoutAsset) { text = "从资产重载" };
            reloadButton.AddToClassList("hex-button");
            buttonRow.Add(reloadButton);

            var saveButton = new Button(SaveLayoutAsset) { text = "保存布局资产" };
            saveButton.AddToClassList("hex-button");
#if !UNITY_EDITOR
            saveButton.SetEnabled(false);
#endif
            buttonRow.Add(saveButton);
            _controls.Add(buttonRow);

            _saveStatus = new Label("布局资产：Assets/Resources/UI Toolkit/CardArt/WarriorCardLayout.asset");
            _saveStatus.AddToClassList("hex-muted");
            _saveStatus.AddToClassList("card-test-save-status");
            _controls.Add(_saveStatus);
        }

        private void AddHeading(string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("card-test-section-title");
            _controls.Add(heading);
        }

        private Slider CreateSlider(string label, float low, float high, float value)
        {
            var slider = new Slider(label, low, high) { value = value, showInputField = true };
            _controls.Add(slider);
            return slider;
        }

        private Slider CreateLayoutSlider(string label, float low, float high)
        {
            var slider = CreateSlider(label, low, high, low);
            slider.RegisterValueChangedCallback(_ => ApplySlidersToLayout());
            return slider;
        }

        private void SelectCard(int index)
        {
            if (_cards == null || _cards.Count == 0)
            {
                _cardView.SetContent("1", "示例卡牌", "当前没有找到战士卡牌数据。你仍可编辑右侧文本测试布局。");
                return;
            }

            index = Mathf.Clamp(index, 0, _cards.Count - 1);
            HexCardDefinition card = _cards[index];
            _syncingControls = true;
            _costField.SetValueWithoutNotify(card.energyCost < 0 ? "X" : card.energyCost.ToString());
            _titleField.SetValueWithoutNotify(card.displayName);
            _descriptionField.SetValueWithoutNotify(card.description);
            _syncingControls = false;
            RefreshCardContent();
        }

        private void SelectRegion(int index)
        {
            HexCardUiRegionLayout region = GetRegion(index);
            if (region == null)
                return;

            Rect rect = region.normalizedRect;
            _syncingControls = true;
            _xSlider.SetValueWithoutNotify(rect.x * 100f);
            _ySlider.SetValueWithoutNotify(rect.y * 100f);
            _widthSlider.SetValueWithoutNotify(rect.width * 100f);
            _heightSlider.SetValueWithoutNotify(rect.height * 100f);
            _fontSlider.SetValueWithoutNotify(region.fontSize);
            _syncingControls = false;
            UpdateReadout(region);
        }

        private void ApplySlidersToLayout()
        {
            if (_syncingControls || _workingLayout == null)
                return;

            HexCardUiRegionLayout region = GetRegion(_regionDropdown.index);
            if (region == null)
                return;

            float width = Mathf.Clamp(_widthSlider.value, 2f, 100f) / 100f;
            float height = Mathf.Clamp(_heightSlider.value, 2f, 100f) / 100f;
            float x = Mathf.Clamp(_xSlider.value / 100f, 0f, 1f - width);
            float y = Mathf.Clamp(_ySlider.value / 100f, 0f, 1f - height);
            region.normalizedRect = new Rect(x, y, width, height);
            region.fontSize = _fontSlider.value;
            _cardView.ApplyLayout(_workingLayout);
            UpdateReadout(region);
        }

        private HexCardUiRegionLayout GetRegion(int index)
        {
            if (_workingLayout == null)
                return null;

            return index switch
            {
                0 => _workingLayout.cost,
                1 => _workingLayout.title,
                _ => _workingLayout.description,
            };
        }

        private void RefreshCardContent()
        {
            if (_syncingControls || _cardView == null)
                return;

            _cardView.SetContent(_costField.value, _titleField.value, _descriptionField.value);
        }

        private void ResetWorkingLayout()
        {
            _workingLayout.ResetToDefaults();
            _cardView.ApplyLayout(_workingLayout);
            SelectRegion(_regionDropdown.index);
            SetSaveStatus("已恢复默认值，尚未写入资产。", false);
        }

        private void ReloadLayoutAsset()
        {
            ReplaceWorkingLayout(_sourceLayout.CreateRuntimeCopy());
            SetSaveStatus("已从布局资产重新载入。", false);
        }

        private void ReplaceWorkingLayout(HexCardUiLayoutSettings replacement)
        {
            if (_workingLayout != null)
                Destroy(_workingLayout);

            _workingLayout = replacement;
            _cardView.ApplyLayout(_workingLayout);
            SelectRegion(_regionDropdown.index);
        }

        private void SaveLayoutAsset()
        {
#if UNITY_EDITOR
            if (_sourceLayout == null || _workingLayout == null || !AssetDatabase.Contains(_sourceLayout))
            {
                SetSaveStatus("未找到可写入的布局资产。", true);
                return;
            }

            Undo.RecordObject(_sourceLayout, "Save Warrior Card UI Layout");
            _sourceLayout.referenceSize = _workingLayout.referenceSize;
            _sourceLayout.cost = _workingLayout.cost.Clone();
            _sourceLayout.title = _workingLayout.title.Clone();
            _sourceLayout.description = _workingLayout.description.Clone();
            EditorUtility.SetDirty(_sourceLayout);
            AssetDatabase.SaveAssets();
            SetSaveStatus("布局已保存到 WarriorCardLayout.asset。", false);
#else
            SetSaveStatus("Player 中不能写入 Unity 布局资产。", true);
#endif
        }

        private void UpdateReadout(HexCardUiRegionLayout region)
        {
            if (_layoutReadout == null || region == null)
                return;

            Rect rect = region.normalizedRect;
            int referenceWidth = _workingLayout.referenceSize.x;
            int referenceHeight = _workingLayout.referenceSize.y;
            _layoutReadout.text =
                $"归一化 Rect({rect.x:F3}, {rect.y:F3}, {rect.width:F3}, {rect.height:F3})\n" +
                $"参考像素 x={rect.x * referenceWidth:F0}, y={rect.y * referenceHeight:F0}, " +
                $"w={rect.width * referenceWidth:F0}, h={rect.height * referenceHeight:F0}, 字号={region.fontSize:F0}";
        }

        private void SetSaveStatus(string text, bool error)
        {
            if (_saveStatus == null)
                return;

            _saveStatus.text = text;
            _saveStatus.EnableInClassList("card-test-save-status--error", error);
        }

        private static string GetCardLabel(HexCardDefinition card)
        {
            string name = string.IsNullOrWhiteSpace(card.displayName) ? card.id : card.displayName;
            return $"{name}  [{card.energyCost}]";
        }
    }
}
