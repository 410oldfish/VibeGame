using System;
using System.Collections.Generic;

namespace HexDemo
{
    /// <summary>统一词缀触发时机。所有词缀规则只允许挂在这些节点上。</summary>
    public enum HexCardTriggerTiming
    {
        /// <summary>打出前校验（可否打出）。</summary>
        PlayAttempt,
        /// <summary>打出结算完成（决定去向：弃牌堆/消耗堆）。</summary>
        PlayResolved,
        /// <summary>攻击命中确认后。</summary>
        HitConfirmed,
        /// <summary>其他卡牌被打出后（对仍在手牌中的卡触发）。</summary>
        OtherCardPlayed,
        /// <summary>回合结束弃牌阶段。</summary>
        TurnEnd,
    }

    /// <summary>统一词缀 flag 标识。定义 tag、描述关键词与运行时临时 flag 共用同一套 id。</summary>
    public static class HexCardFlagIds
    {
        public const string FirstPlayOnly = "首发";
        public const string Void = "虚无";
        public const string Exhaust = "消耗";
        public const string NoCostOnHit = "命中无耗";
        public const string RemoveFromGame = "移出游戏";
    }

    /// <summary>单次触发的上下文与结果。规则通过写结果字段影响战斗流程。</summary>
    public sealed class HexCardTriggerContext
    {
        public HexCardTriggerTiming timing;
        public HexBattleUnit unit;
        public HexCardInstance card;
        /// <summary>OtherCardPlayed 时机下：本次实际被打出的卡。</summary>
        public HexCardInstance playedCard;
        /// <summary>本次打出实际支付的能量。</summary>
        public int energySpent;

        public bool blockPlay;
        public bool sendToExhaust;
        public int energyRefunded;
    }

    public sealed class HexCardTriggerRule
    {
        public string flagId;
        public HexCardTriggerTiming timing;
        public Func<HexCardTriggerContext, bool> condition;
        public Action<HexCardTriggerContext> effect;
    }

    /// <summary>
    /// 四词缀统一触发表：首发 / 虚无 / 消耗 / 命中无耗（含"移出游戏"兼容 tag）。
    /// 新词缀在此追加规则，不要在战斗流程里写散落的 if。
    /// </summary>
    public static class CardTriggerTable
    {
        public static readonly IReadOnlyList<HexCardTriggerRule> Rules = new List<HexCardTriggerRule>
        {
            // 首发：不是本回合第一张牌则禁止打出
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.FirstPlayOnly,
                timing = HexCardTriggerTiming.PlayAttempt,
                condition = ctx => ctx.unit?.State != null && ctx.unit.State.cardsPlayedThisTurn > 0,
                effect = ctx => ctx.blockPlay = true,
            },
            // 首发：打出了其他牌后，仍留在手牌的首发牌移入消耗堆
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.FirstPlayOnly,
                timing = HexCardTriggerTiming.OtherCardPlayed,
                condition = ctx => ctx.card != null && ctx.card != ctx.playedCard,
                effect = ctx => ctx.sendToExhaust = true,
            },
            // 消耗：打出结算后进消耗堆
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.Exhaust,
                timing = HexCardTriggerTiming.PlayResolved,
                effect = ctx => ctx.sendToExhaust = true,
            },
            // 虚无：回合结束仍在手牌则进消耗堆
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.Void,
                timing = HexCardTriggerTiming.TurnEnd,
                effect = ctx => ctx.sendToExhaust = true,
            },
            // 移出游戏（兼容 tag）：回合结束行为与虚无一致
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.RemoveFromGame,
                timing = HexCardTriggerTiming.TurnEnd,
                effect = ctx => ctx.sendToExhaust = true,
            },
            // 命中无耗：命中确认后按实际支付能量返还
            new HexCardTriggerRule
            {
                flagId = HexCardFlagIds.NoCostOnHit,
                timing = HexCardTriggerTiming.HitConfirmed,
                condition = ctx => ctx.unit?.State != null && ctx.energySpent > 0,
                effect = ctx =>
                {
                    ctx.unit.State.energy += ctx.energySpent;
                    ctx.energyRefunded = ctx.energySpent;
                },
            },
        };
    }
}
