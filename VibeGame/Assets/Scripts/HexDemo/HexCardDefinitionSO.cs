using UnityEngine;

namespace HexDemo
{
    /// <summary>
    /// Data-only ScriptableObject mirror of <see cref="HexCardDefinition"/>.
    /// Effect logic still lives in the controller's id switch; this only carries data.
    /// </summary>
    [CreateAssetMenu(fileName = "Card", menuName = "HexDemo/Card Definition", order = 0)]
    public sealed class HexCardDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public HexCardType cardType;
        public HexCardProfession profession;
        public HexCardEffectType effectType;
        public HexCardTargetType targetType;
        public int energyCost;
        public int amount;
        public int range;
        public int castRange;
        public int effectRadius;
        public int priority;
        public string rarity;
        [TextArea]
        public string description;
        public Color color = Color.white;
        public bool isUnplayable;
        public bool upgraded;
        public string[] tags;

        public HexCardDefinition ToDefinition()
        {
            return new HexCardDefinition
            {
                id = id,
                displayName = displayName,
                cardType = cardType,
                profession = profession,
                effectType = effectType,
                targetType = targetType,
                energyCost = energyCost,
                amount = amount,
                range = range,
                castRange = castRange,
                effectRadius = effectRadius,
                priority = priority,
                rarity = rarity,
                description = description,
                color = color,
                isUnplayable = isUnplayable,
                upgraded = upgraded,
                tags = tags,
            };
        }

        public void CopyFrom(HexCardDefinition definition)
        {
            if (definition == null)
                return;

            id = definition.id;
            displayName = definition.displayName;
            cardType = definition.cardType;
            profession = definition.profession;
            effectType = definition.effectType;
            targetType = definition.targetType;
            energyCost = definition.energyCost;
            amount = definition.amount;
            range = definition.range;
            castRange = definition.castRange;
            effectRadius = definition.effectRadius;
            priority = definition.priority;
            rarity = definition.rarity;
            description = definition.description;
            color = definition.color;
            isUnplayable = definition.isUnplayable;
            upgraded = definition.upgraded;
            tags = definition.tags;
        }
    }
}
