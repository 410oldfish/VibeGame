using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexEnemyIntentRow : MonoBehaviour
    {
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _resourceLabel;
        private TextMeshProUGUI _orderHintLabel;
        private HexStatusIconBar _statusBar;
        private RectTransform _slotRoot;
        private readonly List<SlotView> _slots = new();

        private sealed class SlotView
        {
            public GameObject root;
            public Image kindBar;
            public TextMeshProUGUI kindLabel;
            public TextMeshProUGUI cardLabel;
        }

        public void EnsureBuilt(Transform parent, int index)
        {
            if (_headerLabel != null)
                return;

            var row = GetComponent<RectTransform>();
            if (row == null)
                row = gameObject.AddComponent<RectTransform>();
            transform.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(0f, 108f);

            var bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);

            _headerLabel = CreateText(transform, "Header", new Vector2(12f, -8f), new Vector2(520f, 24f), 20, FontStyles.Bold);
            _resourceLabel = CreateText(transform, "Resources", new Vector2(12f, -32f), new Vector2(520f, 20f), 16, FontStyles.Normal);
            _orderHintLabel = CreateText(transform, "OrderHint", new Vector2(12f, -86f), new Vector2(520f, 18f), 15, FontStyles.Italic);
            _orderHintLabel.color = new Color(0.75f, 0.8f, 0.9f, 1f);

            _statusBar = gameObject.AddComponent<HexStatusIconBar>();
            _statusBar.EnsureBuilt(transform);
            var statusRect = _statusBar.Root;
            statusRect.anchoredPosition = new Vector2(12f, -54f);
            statusRect.sizeDelta = new Vector2(500f, 28f);

            _slotRoot = new GameObject("IntentSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _slotRoot.SetParent(transform, false);
            _slotRoot.anchorMin = new Vector2(0f, 1f);
            _slotRoot.anchorMax = new Vector2(1f, 1f);
            _slotRoot.pivot = new Vector2(0f, 1f);
            _slotRoot.anchoredPosition = new Vector2(280f, -8f);
            _slotRoot.sizeDelta = new Vector2(-292f, 72f);

            var layout = _slotRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            EnsureSlotCount(4);
            name = $"EnemyIntentRow_{index}";
        }

        public void Refresh(BattleUnitHudView view)
        {
            if (view == null || _headerLabel == null)
                return;

            _headerLabel.text = view.displayName;
            _resourceLabel.text = $"生命 {view.currentHealth}/{view.maxHealth}  护甲 {view.armor}";
            _orderHintLabel.text = string.IsNullOrWhiteSpace(view.intentOrderHint) ? string.Empty : view.intentOrderHint;
            _statusBar.Refresh(view.statuses);

            EnsureSlotCount(Mathf.Max(4, view.intentSlots?.Count ?? 0));
            for (int i = 0; i < _slots.Count; i++)
            {
                bool active = view.intentSlots != null && i < view.intentSlots.Count;
                _slots[i].root.SetActive(active);
                if (!active)
                    continue;

                var slot = view.intentSlots[i];
                _slots[i].kindLabel.text = slot.isEmpty ? "空" : HexBattleStatusDisplay.GetIntentSlotShort(slot.slotKind);
                _slots[i].cardLabel.text = slot.isEmpty ? "—" : slot.cardName;
                _slots[i].kindBar.color = slot.isEmpty
                    ? new Color(0.35f, 0.38f, 0.42f, 0.9f)
                    : GetSlotColor(slot.slotKind);
            }
        }

        private static Color GetSlotColor(HexEnemyIntentSlotKind kind)
        {
            return kind switch
            {
                HexEnemyIntentSlotKind.Move => new Color(0.29f, 0.62f, 0.91f, 0.95f),
                HexEnemyIntentSlotKind.Attack => new Color(0.91f, 0.35f, 0.29f, 0.95f),
                HexEnemyIntentSlotKind.Free => new Color(0.6f, 0.48f, 0.91f, 0.95f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.95f),
            };
        }

        private void EnsureSlotCount(int count)
        {
            while (_slots.Count < count)
            {
                int index = _slots.Count;
                var slot = new SlotView();
                slot.root = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image));
                slot.root.transform.SetParent(_slotRoot, false);
                var rect = slot.root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(96f, 72f);
                slot.root.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

                slot.kindBar = CreatePanel(slot.root.transform, "KindBar", new Vector2(0f, 0f), new Vector2(96f, 22f));
                slot.kindLabel = CreateText(slot.kindBar.transform, "Kind", Vector2.zero, new Vector2(96f, 22f), 14, FontStyles.Bold);
                slot.kindLabel.alignment = TextAlignmentOptions.Center;

                slot.cardLabel = CreateText(slot.root.transform, "Card", new Vector2(4f, -28f), new Vector2(88f, 40f), 14, FontStyles.Normal);
                slot.cardLabel.alignment = TextAlignmentOptions.Top;
                _slots.Add(slot);
            }
        }

        private static Image CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(text);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }
    }
}
