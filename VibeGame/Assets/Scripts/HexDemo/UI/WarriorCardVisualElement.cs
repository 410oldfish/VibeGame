using UnityEngine;
using UnityEngine.UIElements;

namespace HexDemo
{
    internal sealed class WarriorCardVisualElement : VisualElement
    {
        private const string CardBaseResource = "UI Toolkit/CardArt/WarriorCardBase";

        private readonly Label _costLabel;
        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;

        internal WarriorCardVisualElement(string elementName = "warrior-card-preview")
        {
            name = elementName;
            AddToClassList("warrior-card");
            pickingMode = PickingMode.Ignore;

            var cardBase = Resources.Load<Texture2D>(CardBaseResource);
            if (cardBase != null)
                style.backgroundImage = new StyleBackground(cardBase);

            _costLabel = CreateLabel("card-cost", "warrior-card__cost");
            _titleLabel = CreateLabel("card-title", "warrior-card__title");
            _descriptionLabel = CreateLabel("card-description", "warrior-card__description");
        }

        internal void SetPreviewWidth(float width)
        {
            SetCardSize(Mathf.Clamp(width, 240f, 720f));
        }

        internal void SetCardSize(float width)
        {
            width = Mathf.Clamp(width, 120f, 720f);
            style.width = width;
            style.height = width / HexCardUiLayoutSettings.CardAspectRatio;
        }

        internal void SetContent(string cost, string title, string description)
        {
            _costLabel.text = string.IsNullOrWhiteSpace(cost) ? "-" : cost;
            _titleLabel.text = string.IsNullOrWhiteSpace(title) ? "未命名卡牌" : title;
            _descriptionLabel.text = string.IsNullOrWhiteSpace(description) ? "在这里输入卡牌描述。" : description;
        }

        internal void ApplyLayout(HexCardUiLayoutSettings settings, float fontScale = 1f)
        {
            if (settings == null)
                return;

            ApplyRegion(_costLabel, settings.cost, fontScale);
            ApplyRegion(_titleLabel, settings.title, fontScale);
            ApplyRegion(_descriptionLabel, settings.description, fontScale);
        }

        internal void SetGuidesVisible(bool visible)
        {
            EnableInClassList("warrior-card--guides", visible);
        }

        private Label CreateLabel(string elementName, string className)
        {
            var label = new Label { name = elementName, pickingMode = PickingMode.Ignore };
            label.AddToClassList("warrior-card__region");
            label.AddToClassList(className);
            Add(label);
            return label;
        }

        private static void ApplyRegion(VisualElement element, HexCardUiRegionLayout region, float fontScale)
        {
            if (element == null || region == null)
                return;

            Rect rect = ClampRect(region.normalizedRect);
            element.style.left = Length.Percent(rect.x * 100f);
            element.style.top = Length.Percent(rect.y * 100f);
            element.style.width = Length.Percent(rect.width * 100f);
            element.style.height = Length.Percent(rect.height * 100f);
            element.style.fontSize = Mathf.Max(8f, region.fontSize * Mathf.Max(0.1f, fontScale));
        }

        private static Rect ClampRect(Rect rect)
        {
            rect.width = Mathf.Clamp(rect.width, 0.02f, 1f);
            rect.height = Mathf.Clamp(rect.height, 0.02f, 1f);
            rect.x = Mathf.Clamp(rect.x, 0f, 1f - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0f, 1f - rect.height);
            return rect;
        }
    }
}
