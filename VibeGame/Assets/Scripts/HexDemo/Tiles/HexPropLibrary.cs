using System.Collections.Generic;

namespace HexDemo
{
    public static class HexPropLibrary
    {
        private static readonly Dictionary<string, HexPropDefinition> ById = new();
        private static bool _initialized;

        public const string DefaultBarrierPropId = "stone_pillar";
        public const string DefaultRuinPropId = "wood_crate";

        public static HexPropDefinition Get(string propId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(propId))
                return null;
            return ById.TryGetValue(propId, out var def) ? def : null;
        }

        public static IReadOnlyCollection<HexPropDefinition> All
        {
            get
            {
                EnsureInitialized();
                return ById.Values;
            }
        }

        public static HexPropDefinition GetOrDefault(HexTerrainStructureType structureType)
        {
            return structureType switch
            {
                HexTerrainStructureType.Barrier => Get(DefaultBarrierPropId),
                HexTerrainStructureType.Ruin => Get(DefaultRuinPropId),
                _ => null,
            };
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;
            RegisterAll();
        }

        private static void RegisterAll()
        {
            Register(MakeBarrier("stone_pillar", "石台", "MVP",
                "实心石台。阻挡视线；仅特殊破障可移除。",
                HexPropDestroyBy.SpecialOnly));

            Register(MakeRuin("wood_crate", "木箱", 4, "MVP",
                "可破坏残骸。攻击可穿透并扣 HP；归零后掉落可视区拾取物。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                FieldPickup("worn_weapon", "破旧武器拾取（可投掷）", 1)));

            Register(MakeBarrier("life_tree_bough", "生命树·枝桠", "Ch1+",
                "生命树枝桠。仅特殊行动破坏；移除后可掉落治愈球。",
                HexPropDestroyBy.SpecialOnly,
                FieldPickup("healing_orb", "治愈球（+20% 最大生命）", 1)));

            Register(MakeRuin("iron_brazier", "火盆", 6, "Ch1+",
                "火盆残骸。归零后生成着火场地（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                Overlay("ignition_field", "着火场地 2 回合", 2)));

            Register(MakeRuin("bone_pile", "骸骨堆", 1, "Ch1+",
                "骸骨堆。归零召唤骷髅（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, Aura("亡灵邻接回血（stub）", "undead", 1), false,
                SpawnUnit("skeleton", "召唤骷髅", 1)));

            Register(MakeRuin("treasure_chest", "宝箱", 12, "Ch1+",
                "宝箱残骸。HP 归零不发战场拾取，改为战后奖励。",
                HexPropDestroyBy.NormalAttack,
                null, null, true,
                Effect(HexPropOnRemoveType.PostBattleReward, "chest_reward", "战后金币/卡牌奖励", 1)));

            Register(MakeRuin("mimic_chest", "伪装箱", 4, "Ch1+",
                "伪装成宝箱的残骸。归零揭示宝箱怪（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                EnemyTrigger("mimic", "揭示宝箱怪", 1)));

            Register(MakeRuin("barricade_planks", "拒马木栅", 5, "Ch1+",
                "拒马。击退撞上可额外受伤（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                Overlay("wood_spikes", "木刺覆盖（stub）", 1)));

            Register(MakeRuin("ale_barrel", "火药桶", 3, "Ch2+",
                "火药桶。受击装引发信，延迟范围伤害（stub）。",
                HexPropDestroyBy.NormalAttack,
                1, null, false,
                Effect(HexPropOnRemoveType.AreaDamage, "barrel_blast", "邻格2 范围伤 40（stub）", 40)));

            Register(MakeBarrier("shrine_fragment", "圣坛残片", "Ch2+",
                "圣坛残片。破障后生成圣域覆盖（stub）。",
                HexPropDestroyBy.SpecialOnly,
                Overlay("sanctuary", "圣域 2 回合", 2)));

            Register(MakeRuin("thorn_bramble", "荆棘丛", 3, "Ch1+",
                "荆棘丛。可被藤蔓寄生（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false));

            Register(MakeRuin("cult_brazier", "邪火祭盆", 5, "Ch2+",
                "邪火祭盆。归零使敌方全体 +1 力量（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                EnemyTrigger("cult_power", "敌方全体 +1 力量", 1)));

            Register(MakeRuin("webbed_corpse", "蜘蛛巢", 4, "Ch2+",
                "蜘蛛巢。移除后施加束缚（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                Effect(HexPropOnRemoveType.ApplyStatus, "bind", "束缚 1 回合", 1)));

            Register(MakeRuin("holy_font_basin", "圣水盆", 4, "Ch2+",
                "圣水盆。归零生成圣水地覆盖（stub）。",
                HexPropDestroyBy.NormalAttack,
                null, null, false,
                Overlay("holy_ground", "圣水地 3 回合", 3)));
        }

        private static void Register(HexPropDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.propId))
                return;
            ById[definition.propId] = definition;
        }

        private static HexPropDefinition MakeBarrier(
            string id,
            string name,
            string mvp,
            string description,
            HexPropDestroyBy destroyBy,
            params HexPropOnRemoveEffect[] effects)
        {
            return new HexPropDefinition
            {
                propId = id,
                displayName = name,
                structureType = HexTerrainStructureType.Barrier,
                ruinHp = 0,
                blocksLos = true,
                destroyBy = destroyBy,
                onRemoveEffects = ToList(effects),
                description = description,
                mvpStatus = mvp,
            };
        }

        private static HexPropDefinition MakeRuin(
            string id,
            string name,
            int hp,
            string mvp,
            string description,
            HexPropDestroyBy destroyBy,
            int? fuseTurns,
            HexPropAdjacentAura aura,
            bool postBattleReward,
            params HexPropOnRemoveEffect[] effects)
        {
            return new HexPropDefinition
            {
                propId = id,
                displayName = name,
                structureType = HexTerrainStructureType.Ruin,
                ruinHp = hp,
                blocksLos = false,
                destroyBy = destroyBy,
                onRemoveEffects = ToList(effects),
                fuseTurns = fuseTurns,
                adjacentAura = aura,
                postBattleReward = postBattleReward,
                description = description,
                mvpStatus = mvp,
            };
        }

        private static List<HexPropOnRemoveEffect> ToList(HexPropOnRemoveEffect[] effects)
        {
            var list = new List<HexPropOnRemoveEffect>();
            if (effects == null)
                return list;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    list.Add(effects[i]);
            }
            return list;
        }

        private static HexPropOnRemoveEffect FieldPickup(string id, string summary, int amount) =>
            Effect(HexPropOnRemoveType.FieldPickup, id, summary, amount);

        private static HexPropOnRemoveEffect Overlay(string id, string summary, int amount) =>
            Effect(HexPropOnRemoveType.TempOverlay, id, summary, amount);

        private static HexPropOnRemoveEffect SpawnUnit(string id, string summary, int amount) =>
            Effect(HexPropOnRemoveType.SpawnUnit, id, summary, amount);

        private static HexPropOnRemoveEffect EnemyTrigger(string id, string summary, int amount) =>
            Effect(HexPropOnRemoveType.EnemyTrigger, id, summary, amount);

        private static HexPropOnRemoveEffect Effect(HexPropOnRemoveType type, string id, string summary, int amount)
        {
            return new HexPropOnRemoveEffect
            {
                type = type,
                payloadId = id,
                summary = summary,
                amount = amount,
            };
        }

        private static HexPropAdjacentAura Aura(string summary, string enemyTag, int radius)
        {
            return new HexPropAdjacentAura
            {
                summary = summary,
                enemyTag = enemyTag,
                radius = radius,
            };
        }
    }
}
