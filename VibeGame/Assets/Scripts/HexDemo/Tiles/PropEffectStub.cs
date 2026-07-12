using UnityEngine;

namespace HexDemo
{
    public static class PropEffectStub
    {
        public static void ResolveOnRemove(HexTile tile, HexPropDefinition definition)
        {
            if (tile == null)
                return;

            if (definition == null)
            {
                tile.RollDefaultRuinPickup();
                return;
            }

            if (definition.postBattleReward)
            {
                Debug.Log($"[PropStub] {definition.propId} postBattleReward armed at {tile.coord.q},{tile.coord.r} (deferred).");
                return;
            }

            var effects = definition.onRemoveEffects;
            if (effects == null || effects.Count == 0)
            {
                Debug.Log($"[PropStub] {definition.propId} removed with no onRemove effects.");
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                    continue;

                switch (effect.type)
                {
                    case HexPropOnRemoveType.FieldPickup:
                        ApplyFieldPickupBridge(tile, effect);
                        break;
                    case HexPropOnRemoveType.None:
                        break;
                    default:
                        Debug.Log($"[PropStub] {definition.propId} onRemove={effect.type} payload={effect.payloadId} amount={effect.amount} summary={effect.summary}");
                        break;
                }
            }
        }

        private static void ApplyFieldPickupBridge(HexTile tile, HexPropOnRemoveEffect effect)
        {
            if (tile == null || effect == null)
                return;

            string payload = effect.payloadId ?? string.Empty;
            if (payload.Contains("heal") || payload == "healing_orb")
            {
                tile.SetPickup(HexTerrainPickupType.Heal, Mathf.Max(1, effect.amount > 0 ? effect.amount : 15));
                return;
            }

            if (payload.Contains("weapon") || payload == "worn_weapon" || payload.Contains("axe"))
            {
                tile.SetPickup(HexTerrainPickupType.TemporaryCard, 1);
                return;
            }

            if (payload.Contains("strength"))
            {
                tile.SetPickup(HexTerrainPickupType.TemporaryStrength, Mathf.Max(1, effect.amount > 0 ? effect.amount : 2));
                return;
            }

            // Default bridge for unknown field_pickup payloads.
            tile.SetPickup(HexTerrainPickupType.TemporaryCard, 1);
            Debug.Log($"[PropStub] Bridged field_pickup '{payload}' to TemporaryCard.");
        }
    }
}
