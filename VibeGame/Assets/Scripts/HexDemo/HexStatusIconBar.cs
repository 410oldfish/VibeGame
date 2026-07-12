using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed class HexStatusIconBar : MonoBehaviour
    {
        private readonly List<StatusBadge> _badges = new();
        private static readonly Dictionary<string, Sprite> IconCache = new();
        private RectTransform _root;
        public RectTransform Root => _root;

        private sealed class StatusBadge
        {
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI countText;
            public TextMeshProUGUI tooltipHost;
        }

        public void EnsureBuilt(Transform parent)
        {
            if (_root != null)
                return;

            _root = new GameObject("StatusIconBar", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _root.SetParent(parent, false);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = new Vector2(0f, 40f);

            var layout = _root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public void Refresh(IReadOnlyList<BattleStatusEntry> entries)
        {
            if (_root == null)
                return;

            EnsureBadgeCount(entries?.Count ?? 0);
            for (int i = 0; i < _badges.Count; i++)
            {
                bool active = entries != null && i < entries.Count;
                _badges[i].root.SetActive(active);
                if (!active)
                    continue;

                var entry = entries[i];
                Sprite sprite = LoadStatusIcon(entry.iconId);
                _badges[i].icon.sprite = sprite;
                _badges[i].icon.preserveAspect = true;
                _badges[i].icon.color = entry.isBuff
                    ? new Color(0.35f, 0.95f, 0.62f, 1f)
                    : new Color(1f, 0.4f, 0.32f, 1f);
                _badges[i].countText.text = entry.isPermanent ? "MAX" : entry.stacks.ToString();
                var label = _badges[i].icon.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.gameObject.SetActive(sprite == null);
                    label.text = entry.shortLabel;
                }
                _badges[i].root.name = $"Status_{entry.iconId}_{entry.displayName}";
            }
        }

        private static Sprite LoadStatusIcon(string iconId)
        {
            if (string.IsNullOrWhiteSpace(iconId))
                return null;
            if (IconCache.TryGetValue(iconId, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>($"UI/StatusIcons/{iconId}");
            IconCache[iconId] = sprite;
            return sprite;
        }

        private void EnsureBadgeCount(int count)
        {
            while (_badges.Count < count)
                _badges.Add(CreateBadge(_badges.Count));
        }

        private StatusBadge CreateBadge(int index)
        {
            var badge = new StatusBadge();
            badge.root = new GameObject($"Status_{index}", typeof(RectTransform));
            badge.root.transform.SetParent(_root, false);
            var rootRect = badge.root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(36f, 36f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(badge.root.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            badge.icon = iconGO.GetComponent<Image>();

            var shortLabel = new GameObject("Short", typeof(RectTransform), typeof(TextMeshProUGUI));
            shortLabel.transform.SetParent(iconGO.transform, false);
            var shortRect = shortLabel.GetComponent<RectTransform>();
            shortRect.anchorMin = Vector2.zero;
            shortRect.anchorMax = Vector2.one;
            shortRect.offsetMin = Vector2.zero;
            shortRect.offsetMax = Vector2.zero;
            var shortText = shortLabel.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(shortText);
            shortText.fontSize = 14f;
            shortText.fontStyle = FontStyles.Bold;
            shortText.alignment = TextAlignmentOptions.Center;
            shortText.color = Color.white;

            var countGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGO.transform.SetParent(badge.root.transform, false);
            var countRect = countGO.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(3f, -2f);
            countRect.sizeDelta = new Vector2(30f, 16f);
            badge.countText = countGO.GetComponent<TextMeshProUGUI>();
            HexTMPFontProvider.ApplyTo(badge.countText);
            badge.countText.fontSize = 10f;
            badge.countText.fontStyle = FontStyles.Bold;
            badge.countText.alignment = TextAlignmentOptions.BottomRight;
            badge.countText.color = Color.white;

            return badge;
        }
    }
}
