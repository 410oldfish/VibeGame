using UnityEngine;

namespace HexDemo
{
    public static class HexDamageResolver
    {
        public static HexAttackModifierSnapshot CaptureAttackModifiers(HexBattleUnit source)
        {
            if (source?.State == null)
                return default;

            return new HexAttackModifierSnapshot(
                source.State.warriorNextAttackDamageBonus,
                source.State.warriorFocusEffectDoubleThisCard,
                source.State.weak > 0,
                source.State.warriorDamageMultiplierThisTurn,
                source.State.vigor,
                source.State.momentum > 0);
        }

        public static void ConsumeAttackModifiers(HexBattleUnit source, HexAttackModifierSnapshot snapshot)
        {
            if (source?.State == null)
                return;

            if (snapshot.ConsumesNextAttackBonus)
            {
                source.State.warriorNextAttackDamageBonus = source.State.warriorNextAttackDamageBonusQueued;
                source.State.warriorNextAttackDamageBonusQueued = 0;
                if (snapshot.doubleNextAttackBonus)
                    source.State.warriorFocusEffectDoubleThisCard = false;
            }

            if (snapshot.ConsumesVigor)
                source.State.vigor = 0;
            if (snapshot.ConsumesMomentum)
                source.State.momentum = Mathf.Max(0, source.State.momentum - 1);
        }

        public static int PreviewModifiedDamage(
            HexAttackModifierSnapshot snapshot,
            HexBattleUnit target,
            int baseDamage)
        {
            int result = Mathf.Max(0, baseDamage);
            int nextAttackBonus = snapshot.nextAttackBonus;
            if (snapshot.doubleNextAttackBonus)
                nextAttackBonus *= 2;
            result += nextAttackBonus;

            if (snapshot.weak)
                result = Mathf.FloorToInt(result * 0.75f);
            result *= Mathf.Max(1, snapshot.damageMultiplier);
            result += snapshot.vigor;

            if (target?.State != null && target.State.vulnerable > 0)
                result = Mathf.CeilToInt(result * 1.25f);
            if (snapshot.momentum)
                result = Mathf.CeilToInt(result * 1.5f);

            return Mathf.Max(0, result);
        }

        public static void CompleteAttackBatch(HexBattleUnit source, int totalHealthLost)
        {
            if (source?.State == null || totalHealthLost <= 0)
                return;

            source.State.damageDealtThisTurn += totalHealthLost;
            if (source.State.vampirism > 0 && source.IsAlive)
            {
                source.Heal(totalHealthLost);
                source.State.vampirism = Mathf.Max(0, source.State.vampirism - 1);
            }
        }

        public static HexDamageResult Resolve(HexDamageRequest request)
        {
            HexBattleUnit target = request.target;
            if (target?.State == null || request.requestedDamage <= 0 || !target.IsAlive)
                return HexDamageResult.None(request.requestedDamage);

            int amount = request.requestedDamage;
            if (request.IsAttack)
            {
                if (request.attackModifierSnapshot.HasValue)
                {
                    amount = PreviewModifiedDamage(
                        request.attackModifierSnapshot.Value,
                        target,
                        amount);
                }

                HexBattleUnit source = request.source;
                bool axeArmorBreak = source != null && source.State.axeAppliesArmorBreak &&
                    (source.State.weapon == HexWeaponType.Axe || source.State.allWeaponsEquipped);
                bool swordBrittle = source != null && source.State.swordAppliesBrittle &&
                    (source.State.weapon == HexWeaponType.Sword || source.State.allWeaponsEquipped);
                bool hammerArmorCrush = source != null && source.State.hammerDoubleArmorDamage &&
                    (source.State.weapon == HexWeaponType.Hammer || source.State.allWeaponsEquipped);

                if (axeArmorBreak)
                    target.State.armorBreak += 1;
                if (swordBrittle)
                    target.State.brittle += 1;
                if (hammerArmorCrush && target.State.armor > 0)
                    target.State.armor = Mathf.Max(0, target.State.armor - Mathf.Min(target.State.armor, amount));

                if (target.State.deflect > 0)
                {
                    amount = Mathf.CeilToInt(amount * 0.75f);
                    target.State.deflect = Mathf.Max(0, target.State.deflect - 1);
                }
                if (target.State.block > 0)
                    amount = Mathf.Max(0, amount - target.State.block);
                amount = Mathf.CeilToInt(amount * request.targetDamageMultiplier);
                if (amount <= 0)
                    return HexDamageResult.None(request.requestedDamage);
            }

            return target.ApplyResolvedDamage(request.requestedDamage, amount);
        }
    }
}
