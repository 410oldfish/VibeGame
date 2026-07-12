using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed partial class HexBattleUI
    {
        private RectTransform _consumableBar;
        private RectTransform _consumableSlots;
        private TextMeshProUGUI _consumablePrompt;
        private RectTransform _consumableTooltip;
        private TextMeshProUGUI _consumableTooltipTitle;
        private TextMeshProUGUI _consumableTooltipBody;
        private readonly List<GameObject> _consumableSlotObjects = new();

        private void EnsureConsumableBar(Transform canvasRoot)
        {
            if (_consumableBar != null || canvasRoot == null)
                return;

            // Player status occupies the first ~100 px in the upper-left HUD. Keep the item bar
            // directly beneath it while retaining a true top-left anchor on every resolution.
            _consumableBar = CreatePanel(canvasRoot, "ConsumableBar", new Vector2(18f, -104f), new Vector2(554f, 140f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _consumableBar.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.9f);

            var title = CreateTMP(_consumableBar, "Title", new Vector2(12f, -8f), new Vector2(92f, 26f), 18, FontStyles.Bold);
            title.text = "消耗道具";

            _consumablePrompt = CreateTMP(_consumableBar, "Prompt", new Vector2(108f, -8f), new Vector2(430f, 26f), 16, FontStyles.Normal);
            _consumablePrompt.color = new Color(1f, 0.82f, 0.32f, 1f);

            _consumableSlots = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            _consumableSlots.SetParent(_consumableBar, false);
            _consumableSlots.anchorMin = new Vector2(0f, 1f);
            _consumableSlots.anchorMax = new Vector2(0f, 1f);
            _consumableSlots.pivot = new Vector2(0f, 1f);
            _consumableSlots.anchoredPosition = new Vector2(12f, -38f);
            _consumableSlots.sizeDelta = new Vector2(530f, 92f);
            var layout = _consumableSlots.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            EnsureConsumableTooltip(canvasRoot);
        }

        private void RefreshConsumableBar()
        {
            if (_consumableBar == null || _controller == null)
                return;

            HideConsumableTooltip();

            for (int i = 0; i < _consumableSlotObjects.Count; i++)
            {
                if (_consumableSlotObjects[i] != null)
                    Destroy(_consumableSlotObjects[i]);
            }
            _consumableSlotObjects.Clear();

            var items = _controller.GetConsumables();
            int slotCount = 4;
            for (int i = 0; i < slotCount; i++)
            {
                HexConsumableInstance item = i < items.Count ? items[i] : null;
                var slot = CreatePanel(_consumableSlots, $"Item_{i}", Vector2.zero, new Vector2(126f, 88f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                _consumableSlotObjects.Add(slot.gameObject);
                var image = slot.GetComponent<Image>();
                image.color = item?.Definition != null ? GetConsumableColor(item.Definition.category) : new Color(0.12f, 0.14f, 0.18f, 0.72f);

                var label = CreateTMP(slot, "Label", Vector2.zero, new Vector2(116f, 82f), 15, FontStyles.Bold);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = item?.Definition != null
                    ? $"{item.Definition.displayName}\n剩余 ×{item.remainingUses}\n<color=#D7E7FF>点击使用</color>"
                    : "空道具槽";

                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
                colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.7f);
                colors.colorMultiplier = 1f;
                button.colors = colors;
                // Match card interaction: the visual remains selectable for the whole player turn;
                // the controller is the authority that rejects input during resolution/busy frames.
                button.interactable = item?.Definition != null && _controller.CanSelectConsumables();
                if (item != null)
                {
                    string runtimeId = item.runtimeId;
                    button.onClick.AddListener(() => _controller.RequestUseConsumable(runtimeId));
                    AddConsumableHover(slot, item);
                    if (_controller.IsConsumableSelected(runtimeId))
                    {
                        var outline = slot.gameObject.AddComponent<Outline>();
                        outline.effectColor = new Color(1f, 0.82f, 0.25f, 1f);
                        outline.effectDistance = new Vector2(3f, -3f);
                        label.text = $"{item.Definition.displayName}\n剩余 ×{item.remainingUses}\n<color=#FFE05C>已选中</color>";
                    }
                }
            }

            if (_controller.GetLocalPlayerState()?.flyingSecretTurns > 0)
                AddSkillButton("飞行姿态", _controller.RequestUseFlyingSecretSkill);
            if (_controller.GetLocalPlayerState()?.stealSecretTurns > 0)
                AddSkillButton("窃取", _controller.RequestUseStealSecretSkill);

            if (_consumablePrompt != null)
            {
                string prompt = _controller.GetConsumableTargetPrompt();
                _consumablePrompt.text = string.IsNullOrWhiteSpace(prompt) ? "点击道具使用；目标型道具再点击战场目标" : prompt;
            }
        }

        private void AddSkillButton(string labelText, UnityEngine.Events.UnityAction action)
        {
            var slot = CreatePanel(_consumableSlots, $"Skill_{labelText}", Vector2.zero, new Vector2(94f, 88f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _consumableSlotObjects.Add(slot.gameObject);
            slot.GetComponent<Image>().color = new Color(0.38f, 0.24f, 0.64f, 0.95f);
            var label = CreateTMP(slot, "Label", Vector2.zero, new Vector2(90f, 82f), 15, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = $"{labelText}\n1能量\n点击使用";
            var button = slot.gameObject.AddComponent<Button>();
            button.interactable = _controller.CanSelectConsumables();
            button.onClick.AddListener(action);
        }

        private void EnsureConsumableTooltip(Transform canvasRoot)
        {
            if (_consumableTooltip != null || canvasRoot == null)
                return;

            _consumableTooltip = CreatePanel(canvasRoot, "ConsumableTooltip", new Vector2(586f, -104f), new Vector2(360f, 172f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var image = _consumableTooltip.GetComponent<Image>();
            image.color = new Color(0.025f, 0.035f, 0.055f, 0.97f);
            image.raycastTarget = false;

            var group = _consumableTooltip.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            _consumableTooltipTitle = CreateTMP(_consumableTooltip, "Title", new Vector2(16f, -12f), new Vector2(328f, 34f), 22, FontStyles.Bold);
            _consumableTooltipTitle.raycastTarget = false;
            _consumableTooltipBody = CreateTMP(_consumableTooltip, "Body", new Vector2(16f, -52f), new Vector2(328f, 104f), 17, FontStyles.Normal);
            _consumableTooltipBody.alignment = TextAlignmentOptions.TopLeft;
            _consumableTooltipBody.textWrappingMode = TextWrappingModes.Normal;
            _consumableTooltipBody.raycastTarget = false;
            _consumableTooltip.gameObject.SetActive(false);
        }

        private void AddConsumableHover(RectTransform slot, HexConsumableInstance item)
        {
            if (slot == null || item?.Definition == null)
                return;

            var trigger = slot.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowConsumableTooltip(item));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideConsumableTooltip());
            trigger.triggers.Add(exit);
        }

        private void ShowConsumableTooltip(HexConsumableInstance item)
        {
            var definition = item?.Definition;
            if (_consumableTooltip == null || definition == null)
                return;

            _consumableTooltipTitle.text = $"{definition.displayName}  ×{item.remainingUses}";
            _consumableTooltipTitle.color = GetConsumableColor(definition.category);
            _consumableTooltipBody.text = $"{GetConsumableCategoryName(definition.category)}\n{definition.description}";
            _consumableTooltip.gameObject.SetActive(true);
            _consumableTooltip.SetAsLastSibling();
        }

        private void HideConsumableTooltip()
        {
            if (_consumableTooltip != null)
                _consumableTooltip.gameObject.SetActive(false);
        }

        private static string GetConsumableCategoryName(HexConsumableCategory category)
        {
            return category switch
            {
                HexConsumableCategory.Engineering => "工程道具",
                HexConsumableCategory.Spell => "符咒",
                HexConsumableCategory.Potion => "药水",
                _ => "食物",
            };
        }

        private static Color GetConsumableColor(HexConsumableCategory category)
        {
            return category switch
            {
                HexConsumableCategory.Engineering => new Color(0.55f, 0.43f, 0.25f, 0.96f),
                HexConsumableCategory.Spell => new Color(0.42f, 0.28f, 0.68f, 0.96f),
                HexConsumableCategory.Potion => new Color(0.22f, 0.52f, 0.66f, 0.96f),
                _ => new Color(0.42f, 0.58f, 0.28f, 0.96f),
            };
        }
    }
}
