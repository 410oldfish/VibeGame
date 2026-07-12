using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public static class HexBattleStatusDisplay
    {
        public static List<BattleStatusEntry> BuildMvpStatusEntries(HexBattleUnitState state)
        {
            var entries = new List<BattleStatusEntry>(48);
            if (state == null)
                return entries;

            TryAdd(entries, BattleHudStatusKind.Strength, state.strength, "力量", "攻击伤害增加", true, "力", "strength");
            TryAdd(entries, BattleHudStatusKind.Steady, state.toughness, "坚韧", "获得护甲时增加护甲值", true, "韧", "toughness");
            TryAdd(entries, BattleHudStatusKind.Other, state.agility, "敏捷", "每层使每回合一张随机牌临时减费", true, "敏", "agility");
            TryAdd(entries, BattleHudStatusKind.Other, state.wisdom, "智慧", "每回合额外抽牌", true, "智", "wisdom");
            TryAdd(entries, BattleHudStatusKind.Other, state.humility, "谦逊", "每回合额外保留手牌", true, "谦", "humility");
            TryAdd(entries, BattleHudStatusKind.Other, state.luck, "幸运", "使随机手牌费用变为0", true, "运", "luck");
            TryAdd(entries, BattleHudStatusKind.Other, state.vigor, "活力", "下一次造成伤害时增加伤害", true, "活", "vigor");
            TryAdd(entries, BattleHudStatusKind.Vampirism, state.vampirism, "吸血", "下次造成生命伤害时恢复等量生命", true, "吸", "vampirism");
            TryAdd(entries, BattleHudStatusKind.Regeneration, state.regeneration, "再生", "回合开始恢复生命，随后减少1层", true, "生", "regeneration");
            TryAdd(entries, BattleHudStatusKind.Other, state.holyShield, "圣盾", "完全抵挡下一次伤害", true, "圣", "holy_shield");
            TryAdd(entries, BattleHudStatusKind.Other, state.immunity, "免疫", "无法被施加负面效果", true, "免", "immunity");
            TryAdd(entries, BattleHudStatusKind.Other, state.invincible, "无敌", "无法受到伤害", true, "无", "invincible");
            TryAdd(entries, BattleHudStatusKind.Other, state.deflect, "闪避", "受到的伤害降低", true, "闪", "deflect");
            TryAdd(entries, BattleHudStatusKind.Block, state.block, "格挡", "受伤时减少等量伤害", true, "格", "block");
            TryAdd(entries, BattleHudStatusKind.Other, state.thorns, "荆棘", "受击时对攻击者造成伤害", true, "棘", "thorns");
            TryAdd(entries, BattleHudStatusKind.Other, state.phaseMovement, "飞行", "移动不受单位碰撞和部分地形限制", true, "飞", "flying");
            TryAdd(entries, BattleHudStatusKind.Other, state.momentum, "气势", "提高攻击伤害", true, "势", "momentum");

            TryAdd(entries, BattleHudStatusKind.Bleed, state.bleed, "流血", "出牌或回合结算时受到层数伤害", false, "血", "bleed");
            TryAdd(entries, BattleHudStatusKind.Poison, state.poison, "中毒", "回合开始受到层数伤害，随后减少1层", false, "毒", "poison");
            TryAdd(entries, BattleHudStatusKind.Vulnerable, state.vulnerable, "易伤", "受到伤害增加25%", false, "易", "vulnerable");
            TryAdd(entries, BattleHudStatusKind.Other, state.weak, "虚弱", "攻击造成的伤害降低25%", false, "弱", "weak");
            TryAdd(entries, BattleHudStatusKind.Stun, state.stun, "眩晕", "无法行动", false, "晕", "stun");
            TryAdd(entries, BattleHudStatusKind.Other, state.blind, "致盲", "攻击牌造成的伤害大幅降低", false, "盲", "blind");
            TryAdd(entries, BattleHudStatusKind.Other, state.nausea, "恶心", "受到的负面状态层数增加", false, "恶", "nausea");
            TryAdd(entries, BattleHudStatusKind.Other, state.curse, "诅咒", "受到诅咒效果影响", false, "咒", "curse");
            TryAdd(entries, BattleHudStatusKind.Other, state.allure, "诱惑", "回合开始向诱惑源移动", false, "诱", "allure");
            TryAdd(entries, BattleHudStatusKind.Other, Mathf.Max(state.taunt, state.tauntActiveThisTurn), "嘲讽", "只能以嘲讽源为攻击目标", false, "嘲", "taunt");
            TryAdd(entries, BattleHudStatusKind.Other, state.confusion, "混乱", "攻击目标可能发生变化", false, "乱", "confusion");
            TryAdd(entries, BattleHudStatusKind.Bind, state.bind, "束缚", "无法移动", false, "束", "bind");
            TryAdd(entries, BattleHudStatusKind.Burn, state.burn, "燃烧", "回合开始受到层数伤害并使层数减半", false, "燃", "burn");
            TryAdd(entries, BattleHudStatusKind.Other, state.entangle, "缠绕", "移动时受到伤害", false, "缠", "entangle");
            TryAdd(entries, BattleHudStatusKind.Other, state.armorBreak, "破甲", "护甲只能抵挡一半伤害", false, "破", "armor_break");
            TryAdd(entries, BattleHudStatusKind.Other, state.brittle, "熔化", "获得的护甲减少", false, "熔", "brittle");
            TryAdd(entries, BattleHudStatusKind.Other, state.disarm, "缴械", "无法打出攻击牌", false, "缴", "disarm");
            TryAdd(entries, BattleHudStatusKind.Other, state.cold, "寒冷", "随机手牌费用增加", false, "寒", "cold");
            TryAdd(entries, BattleHudStatusKind.Other, state.fatigue, "力竭", "下回合失去能量", false, "竭", "fatigue");
            TryAdd(entries, BattleHudStatusKind.Other, Mathf.Max(state.paralysis, state.paralysisActiveThisTurn), "麻痹", "本回合所有牌费用增加", false, "麻", "paralysis");
            TryAdd(entries, BattleHudStatusKind.Other, state.slow, "残废", "回合开始失去移动力", false, "残", "slow");
            TryAdd(entries, BattleHudStatusKind.Other, state.frozen, "冰冻", "无法行动且无法受到伤害", false, "冻", "frozen");

            AddPermanent(entries, state.consumableAttackBurnBonus > 0, "辣椒", $"本场战斗攻击附加{state.consumableAttackBurnBonus}燃烧", true, "椒", "chili");
            AddTimed(entries, state.consumableCoffeeTurns, "咖啡", $"每回合获得{state.consumableCoffeeAmount}活力", true, "咖", "vigor");
            AddTimed(entries, state.consumableEggTartTurns, "蛋挞", "每回合将一张虚无、消耗的前进加入手牌", true, "挞", "draw_passive");
            AddTimed(entries, state.flyingSecretTurns, "飞行秘术", "可消耗1能量进入飞行姿态", true, "秘", "flying");
            AddTimed(entries, state.stealSecretTurns, "窃取秘术", "可消耗1能量复制敌方手牌", true, "窃", "draw_passive");
            AddPermanent(entries, state.extraEnergyPerTurn > 0, "能量增幅", $"每回合额外获得{state.extraEnergyPerTurn}能量", true, "能", "energy_passive");
            AddPermanent(entries, state.extraMovePerTurn > 0, "移动增幅", $"每回合额外获得{state.extraMovePerTurn}移动力", true, "移", "momentum");
            AddPermanent(entries, state.drawOnExhaust, "消耗抽牌", "每当一张牌被消耗时抽1张牌", true, "抽", "draw_passive");
            AddPermanent(entries, state.retainArmorBetweenTurns, "保留护甲", "护甲不会在回合开始时清空", true, "甲", "block");
            AddPermanent(entries, state.gainStrengthOnSelfDamage, "受伤成长", "受到自伤时获得力量", true, "伤", "strength");
            AddPermanent(entries, state.gainMoveOnStrengthOrToughness, "力量转移", "获得力量或坚韧时同时获得移动力", true, "转", "momentum");
            AddPermanent(entries, state.warriorBloodPactActive, "鲜血契约", "卖血体系被动生效", true, "契", "vampirism");
            AddPermanent(entries, state.warriorInfernoHeart, "炼狱之心", "燃烧体系被动生效", true, "狱", "fire_passive");
            AddPermanent(entries, state.warriorFirstAttackKnockback, "首击击退", "每回合第一次攻击附带击退", true, "退", "momentum");
            AddPermanent(entries, state.axeAppliesArmorBreak, "战斧被动", "攻击附加破甲", true, "斧", "weapon_passive");
            AddPermanent(entries, state.hammerDoubleArmorDamage, "战锤被动", "对护甲伤害提高", true, "锤", "hammer_passive");
            AddPermanent(entries, state.swordAppliesBrittle, "长剑被动", "攻击附加熔化", true, "剑", "weapon_passive");
            return entries;
        }

        private static void TryAdd(
            List<BattleStatusEntry> entries,
            BattleHudStatusKind kind,
            int stacks,
            string displayName,
            string rule,
            bool isBuff,
            string shortLabel,
            string iconId)
        {
            if (stacks <= 0)
                return;

            entries.Add(new BattleStatusEntry
            {
                kind = kind,
                displayName = displayName,
                tooltip = $"{displayName} ×{stacks} — {rule}",
                stacks = stacks,
                isBuff = isBuff,
                shortLabel = shortLabel,
                iconId = iconId,
            });
        }

        private static void AddPermanent(List<BattleStatusEntry> entries, bool active, string displayName, string rule, bool isBuff, string shortLabel, string iconId)
        {
            if (!active)
                return;

            entries.Add(new BattleStatusEntry
            {
                kind = BattleHudStatusKind.Other,
                displayName = displayName,
                tooltip = $"{displayName} MAX — {rule}",
                stacks = 1,
                isBuff = isBuff,
                shortLabel = shortLabel,
                iconId = iconId,
                isPermanent = true,
            });
        }

        private static void AddTimed(List<BattleStatusEntry> entries, int turns, string displayName, string rule, bool isBuff, string shortLabel, string iconId)
        {
            if (turns <= 0)
                return;

            entries.Add(new BattleStatusEntry
            {
                kind = BattleHudStatusKind.Other,
                displayName = displayName,
                tooltip = $"{displayName} {turns}回合 — {rule}",
                stacks = turns,
                isBuff = isBuff,
                shortLabel = shortLabel,
                iconId = iconId,
            });
        }

        public static string GetIntentSlotLabel(HexEnemyIntentSlotKind kind)
        {
            return kind switch
            {
                HexEnemyIntentSlotKind.Move => "移动",
                HexEnemyIntentSlotKind.Attack => "攻击",
                HexEnemyIntentSlotKind.Free => "自由",
                _ => kind.ToString(),
            };
        }

        public static string GetIntentSlotShort(HexEnemyIntentSlotKind kind)
        {
            return kind switch
            {
                HexEnemyIntentSlotKind.Move => "移",
                HexEnemyIntentSlotKind.Attack => "攻",
                HexEnemyIntentSlotKind.Free => "自",
                _ => "?",
            };
        }
    }
}
