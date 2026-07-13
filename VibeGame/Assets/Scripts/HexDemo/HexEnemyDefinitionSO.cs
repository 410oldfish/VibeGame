using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    [CreateAssetMenu(fileName = "Enemy", menuName = "HexDemo/Enemy Definition", order = 1)]
    public sealed class HexEnemyDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public HexEnemyEncounterType encounterType;
        public HexEnemyIntentPattern intentPattern;
        public int attackMinRange = 1;
        public int attackMaxRange = 1;
        public int emptyDrawPileStrengthGain;
        public int maxSummons;
        public int summonHealth;
        [Range(0f, 1f)] public float phaseTwoHealthRatio;
        public List<HexEnemyIntentSlotKind> intentSlots = new();
        public List<string> deckCardIds = new();
        public List<string> phaseTwoDeckCardIds = new();
        public string bottomCardId;

        public HexEnemyDefinition ToDefinition()
        {
            var definition = new HexEnemyDefinition
            {
                id = id,
                displayName = displayName,
                encounterType = encounterType,
                intentPattern = intentPattern,
                attackMinRange = Mathf.Max(1, attackMinRange),
                attackMaxRange = Mathf.Max(Mathf.Max(1, attackMinRange), attackMaxRange),
                emptyDrawPileStrengthGain = Mathf.Max(0, emptyDrawPileStrengthGain),
                maxSummons = Mathf.Max(0, maxSummons),
                summonHealth = Mathf.Max(0, summonHealth),
                phaseTwoHealthRatio = Mathf.Clamp01(phaseTwoHealthRatio),
                intentSlots = new List<HexEnemyIntentSlotKind>(intentSlots ?? new List<HexEnemyIntentSlotKind>()),
                deckDefinitions = ResolveCards(deckCardIds),
                phaseTwoDeckDefinitions = ResolveCards(phaseTwoDeckCardIds),
                bottomCard = HexCardLibrary.GetCardById(bottomCardId),
            };

            return definition;
        }

        private static List<HexCardDefinition> ResolveCards(List<string> ids)
        {
            var result = new List<HexCardDefinition>();
            if (ids == null)
                return result;

            for (int i = 0; i < ids.Count; i++)
            {
                var card = HexCardLibrary.GetCardById(ids[i]);
                if (card != null)
                    result.Add(card);
            }

            return result;
        }
    }

}
