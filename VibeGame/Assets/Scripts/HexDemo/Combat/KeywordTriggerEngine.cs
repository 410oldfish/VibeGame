using System;
using System.Collections.Generic;

namespace HexDemo
{
    /// <summary>
    /// 词缀统一执行入口。战斗流程只调用本类的入口方法，
    /// 具体词缀行为全部由 <see cref="CardTriggerTable"/> 的规则驱动。
    /// </summary>
    public static class KeywordTriggerEngine
    {
        /// <summary>统一 flag 查询：定义 tag + 描述关键词 + 运行时临时 flag 合并判定。</summary>
        public static bool HasFlag(HexCardInstance card, string flagId)
        {
            if (card == null || string.IsNullOrWhiteSpace(flagId))
                return false;

            if (card.HasRuntimeFlag(flagId))
                return true;

            if (string.Equals(flagId, HexCardFlagIds.Exhaust, StringComparison.OrdinalIgnoreCase) &&
                (card.exhaustWhenPlayed || HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Exhaust)))
                return true;

            if (string.Equals(flagId, HexCardFlagIds.Void, StringComparison.OrdinalIgnoreCase) &&
                HexCardLibrary.HasKeyword(card.definition, HexCardKeywordType.Void))
                return true;

            return HasDefinitionTag(card.definition, flagId);
        }

        /// <summary>核心分发：对给定上下文执行触发表中匹配时机与词缀的所有规则。</summary>
        public static HexCardTriggerContext Dispatch(HexCardTriggerContext context)
        {
            if (context?.card == null)
                return context;

            var rules = CardTriggerTable.Rules;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || rule.timing != context.timing || rule.effect == null)
                    continue;
                if (!HasFlag(context.card, rule.flagId))
                    continue;
                if (rule.condition != null && !rule.condition(context))
                    continue;

                rule.effect(context);
            }

            return context;
        }

        /// <summary>PlayAttempt：校验卡牌当前是否允许打出。</summary>
        public static bool CanPlay(HexBattleUnit unit, HexCardInstance card)
        {
            var context = Dispatch(new HexCardTriggerContext
            {
                timing = HexCardTriggerTiming.PlayAttempt,
                unit = unit,
                card = card,
            });
            return context == null || !context.blockPlay;
        }

        /// <summary>PlayResolved：判断打出结算后是否进入消耗堆。</summary>
        public static bool ShouldExhaustOnPlay(HexBattleUnit unit, HexCardInstance card)
        {
            var context = Dispatch(new HexCardTriggerContext
            {
                timing = HexCardTriggerTiming.PlayResolved,
                unit = unit,
                card = card,
            });
            return context != null && context.sendToExhaust;
        }

        /// <summary>OtherCardPlayed：某张牌打出后，对仍在手牌的其他牌触发，返回需要移入消耗堆的牌。</summary>
        public static List<HexCardInstance> CollectHandCardsToExhaustAfterPlay(HexBattleUnit unit, HexCardInstance playedCard)
        {
            var result = new List<HexCardInstance>();
            if (unit?.Deck == null)
                return result;

            var handSnapshot = new List<HexCardInstance>(unit.Deck.Hand);
            for (int i = 0; i < handSnapshot.Count; i++)
            {
                var card = handSnapshot[i];
                if (card == null)
                    continue;

                var context = Dispatch(new HexCardTriggerContext
                {
                    timing = HexCardTriggerTiming.OtherCardPlayed,
                    unit = unit,
                    card = card,
                    playedCard = playedCard,
                });
                if (context != null && context.sendToExhaust)
                    result.Add(card);
            }

            return result;
        }

        /// <summary>HitConfirmed：命中确认后触发（命中无耗返还能量），返回实际返还量。</summary>
        public static int OnHitConfirmed(HexBattleUnit unit, HexCardInstance card, int energySpent)
        {
            var context = Dispatch(new HexCardTriggerContext
            {
                timing = HexCardTriggerTiming.HitConfirmed,
                unit = unit,
                card = card,
                energySpent = energySpent,
            });
            return context?.energyRefunded ?? 0;
        }

        /// <summary>TurnEnd：判断回合结束时仍在手牌的卡是否移入消耗堆。</summary>
        public static bool ShouldExhaustAtTurnEnd(HexBattleUnit unit, HexCardInstance card)
        {
            var context = Dispatch(new HexCardTriggerContext
            {
                timing = HexCardTriggerTiming.TurnEnd,
                unit = unit,
                card = card,
            });
            return context != null && context.sendToExhaust;
        }

        private static bool HasDefinitionTag(HexCardDefinition definition, string tag)
        {
            if (definition?.tags == null)
                return false;

            for (int i = 0; i < definition.tags.Length; i++)
            {
                if (string.Equals(definition.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
