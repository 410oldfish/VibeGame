using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public enum HexConsumableCategory
    {
        Engineering,
        Spell,
        Potion,
        Food,
    }

    public enum HexConsumableTargetType
    {
        Self,
        Enemy,
        EmptyTile,
        Structure,
    }

    public enum HexConsumableEffectType
    {
        Strength,
        Toughness,
        Vampirism,
        Poison,
        Weak,
        Transform,
        Energy,
        Draw,
        Coffee,
        MaxHealth,
        Armor,
        AttackBurn,
        Wisdom,
        EggTart,
        Regeneration,
        FlyingSecret,
        StealSecret,
        Alchemy,
        StrengthRitual,
        EvilPact,
        BloodTrap,
        Scarecrow,
        GrapplingHook,
        RocketBoots,
        Tripwire,
        IronBall,
    }

    [Serializable]
    public sealed class HexConsumableDefinition
    {
        public string id;
        public string displayName;
        public HexConsumableCategory category;
        public HexConsumableTargetType targetType;
        public HexConsumableEffectType effectType;
        public int maxUses = 1;
        public int amount;
        public int duration;
        public int castRange;
        public int effectRadius;
        [TextArea] public string description;
    }

    [Serializable]
    public sealed class HexConsumableInstance
    {
        public string runtimeId;
        public string definitionId;
        public int remainingUses;

        public HexConsumableInstance() { }

        public HexConsumableInstance(HexConsumableDefinition definition)
        {
            runtimeId = Guid.NewGuid().ToString("N");
            definitionId = definition?.id;
            remainingUses = Mathf.Max(1, definition?.maxUses ?? 1);
        }

        public HexConsumableDefinition Definition => HexConsumableLibrary.Get(definitionId);
    }

    public static class HexConsumableLibrary
    {
        private static readonly Dictionary<string, HexConsumableDefinition> ById = BuildDefinitions();
        private static readonly List<HexConsumableDefinition> AllDefinitions = new(ById.Values);

        public static IReadOnlyList<HexConsumableDefinition> All => AllDefinitions;

        public static HexConsumableDefinition Get(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && ById.TryGetValue(id, out var definition) ? definition : null;
        }

        public static HexConsumableDefinition GetRandomDrop()
        {
            return AllDefinitions.Count > 0 ? AllDefinitions[UnityEngine.Random.Range(0, AllDefinitions.Count)] : null;
        }

        public static HexConsumableDefinition GetRandomDropExcluding(string excludedId)
        {
            if (AllDefinitions.Count == 0)
                return null;
            if (AllDefinitions.Count == 1 || string.IsNullOrWhiteSpace(excludedId))
                return GetRandomDrop();

            int start = UnityEngine.Random.Range(0, AllDefinitions.Count);
            for (int offset = 0; offset < AllDefinitions.Count; offset++)
            {
                var candidate = AllDefinitions[(start + offset) % AllDefinitions.Count];
                if (candidate != null && candidate.id != excludedId)
                    return candidate;
            }

            return GetRandomDrop();
        }

        public static int GetSlotCount(HexCardProfession profession)
        {
            // Only the warrior count is defined by the current GDD. Other professions use the same
            // safe default until their profession documents provide a different value.
            return 4;
        }

        private static Dictionary<string, HexConsumableDefinition> BuildDefinitions()
        {
            var result = new Dictionary<string, HexConsumableDefinition>();

            Add(result, "potion_strength", "力量药水", HexConsumableCategory.Potion, HexConsumableTargetType.Self, HexConsumableEffectType.Strength, 1, 3, 1, 0, 0, "本回合获得3力量。");
            Add(result, "potion_toughness", "坚韧药水", HexConsumableCategory.Potion, HexConsumableTargetType.Self, HexConsumableEffectType.Toughness, 1, 3, 1, 0, 0, "本回合获得3坚韧。");
            Add(result, "potion_vampirism", "吸血药水", HexConsumableCategory.Potion, HexConsumableTargetType.Self, HexConsumableEffectType.Vampirism, 1, 1, 0, 0, 0, "获得1层吸血，下一次造成生命伤害时触发。");
            Add(result, "potion_poison", "毒液药水", HexConsumableCategory.Potion, HexConsumableTargetType.Enemy, HexConsumableEffectType.Poison, 1, 5, 0, 4, 0, "给予敌人5中毒。");
            Add(result, "potion_weak", "虚弱药水", HexConsumableCategory.Potion, HexConsumableTargetType.Enemy, HexConsumableEffectType.Weak, 1, 2, 0, 4, 0, "给予敌人2虚弱。");
            Add(result, "potion_transform", "变形药水", HexConsumableCategory.Potion, HexConsumableTargetType.Enemy, HexConsumableEffectType.Transform, 1, 1, 0, 4, 0, "给予敌人1变形（本回合无法行动）。");

            Add(result, "food_bread", "面包", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Energy, 3, 1, 0, 0, 0, "回复1能量，可使用3次。");
            Add(result, "food_milk", "牛奶", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Draw, 2, 2, 0, 0, 0, "抽2张牌，可使用2次。");
            Add(result, "food_coffee", "咖啡", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Coffee, 2, 3, 3, 0, 0, "连续3回合在回合开始获得3活力，可使用2次。");
            Add(result, "food_dragon_fruit", "火龙果", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.MaxHealth, 1, 10, 0, 0, 0, "永久提高10生命上限并恢复10生命。");
            Add(result, "food_cheese", "奶酪块", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Armor, 2, 10, 0, 0, 0, "获得10护甲，可使用2次。");
            Add(result, "food_chili", "辣椒", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.AttackBurn, 1, 1, 0, 0, 0, "本场战斗攻击额外施加1燃烧。");
            Add(result, "food_citrus", "柑橘", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Wisdom, 1, 1, 3, 0, 0, "获得1智慧，持续3回合。");
            Add(result, "food_egg_tart", "蛋挞", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.EggTart, 1, 1, 5, 0, 0, "连续5回合将一张虚无、消耗的前进加入手牌。");
            Add(result, "food_honey", "蜂蜜", HexConsumableCategory.Food, HexConsumableTargetType.Self, HexConsumableEffectType.Regeneration, 1, 3, 0, 0, 0, "获得3再生。");

            Add(result, "spell_flying_secret", "飞行秘术", HexConsumableCategory.Spell, HexConsumableTargetType.Self, HexConsumableEffectType.FlyingSecret, 1, 1, 3, 0, 0, "3回合内可消耗1能量进入飞行姿态，并获得一张疾行。");
            Add(result, "spell_steal_secret", "窃取秘术", HexConsumableCategory.Spell, HexConsumableTargetType.Self, HexConsumableEffectType.StealSecret, 1, 1, 3, 0, 0, "3回合内可消耗1能量窃取敌方手牌的虚无、消耗复制。");
            Add(result, "spell_alchemy", "点金术", HexConsumableCategory.Spell, HexConsumableTargetType.Enemy, HexConsumableEffectType.Alchemy, 1, 0, 0, 4, 0, "立即击杀普通敌人，并获得等同其当前生命值的金币。");
            Add(result, "spell_strength_ritual", "力量仪式", HexConsumableCategory.Spell, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.StrengthRitual, 1, 3, 3, 4, 2, "范围2的地形附加3力量，持续3回合。");
            Add(result, "spell_evil_pact", "邪恶契约", HexConsumableCategory.Spell, HexConsumableTargetType.Self, HexConsumableEffectType.EvilPact, 1, 0, 0, 0, 0, "所有现有卡牌本场获得虚无、消耗且费用变为0。");

            Add(result, "engineering_blood_trap", "流血陷阱", HexConsumableCategory.Engineering, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.BloodTrap, 1, 5, 0, 3, 0, "单位踏入后获得束缚1与流血5。");
            Add(result, "engineering_scarecrow", "稻草人", HexConsumableCategory.Engineering, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.Scarecrow, 1, 10, 0, 3, 0, "放置10生命、与玩家同仇恨等级的稻草人。");
            Add(result, "engineering_grappling_hook", "钩爪", HexConsumableCategory.Engineering, HexConsumableTargetType.Structure, HexConsumableEffectType.GrapplingHook, 3, 0, 0, 5, 0, "移动到目标构筑物旁，可使用3次。");
            Add(result, "engineering_rocket_boots", "火箭靴", HexConsumableCategory.Engineering, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.RocketBoots, 1, 0, 0, -1, 0, "传送到任意空闲格。");
            Add(result, "engineering_tripwire", "绊锁", HexConsumableCategory.Engineering, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.Tripwire, 1, 2, 0, 5, 0, "依次选择两个端点，穿越连线的角色受到2伤害。");
            Add(result, "engineering_iron_ball", "铁球", HexConsumableCategory.Engineering, HexConsumableTargetType.EmptyTile, HexConsumableEffectType.IronBall, 1, 10, 0, 1, 0, "放置可攻击铁球；受击后沿受击方向滚动至多5格并造成10伤害。");

            return result;
        }

        private static void Add(Dictionary<string, HexConsumableDefinition> result, string id, string name,
            HexConsumableCategory category, HexConsumableTargetType targetType, HexConsumableEffectType effectType,
            int uses, int amount, int duration, int castRange, int radius, string description)
        {
            result[id] = new HexConsumableDefinition
            {
                id = id,
                displayName = name,
                category = category,
                targetType = targetType,
                effectType = effectType,
                maxUses = Mathf.Max(1, uses),
                amount = amount,
                duration = duration,
                castRange = castRange,
                effectRadius = radius,
                description = description,
            };
        }
    }
}
