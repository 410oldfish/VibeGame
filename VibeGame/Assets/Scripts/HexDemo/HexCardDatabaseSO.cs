using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    /// <summary>
    /// Aggregates all card definitions by profession/usage.
    /// Placed at Assets/Resources/HexCardDatabase.asset so <see cref="HexCardLibrary"/>
    /// can load it via Resources.Load at runtime. When absent, the library falls back
    /// to its hardcoded pools.
    /// </summary>
    [CreateAssetMenu(fileName = "HexCardDatabase", menuName = "HexDemo/Card Database", order = 1)]
    public sealed class HexCardDatabaseSO : ScriptableObject
    {
        public List<HexCardDefinitionSO> warriorCards = new();
        public List<HexCardDefinitionSO> paladinCards = new();
        public List<HexCardDefinitionSO> druidCards = new();
        public List<HexCardDefinitionSO> commonCards = new();
        public List<HexCardDefinitionSO> enemyCards = new();

        public List<HexCardDefinition> BuildDefinitions(List<HexCardDefinitionSO> source)
        {
            var result = new List<HexCardDefinition>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(source[i].ToDefinition());
            }

            return result;
        }

        public List<HexCardDefinition> BuildWarrior() => BuildDefinitions(warriorCards);
        public List<HexCardDefinition> BuildPaladin() => BuildDefinitions(paladinCards);
        public List<HexCardDefinition> BuildDruid() => BuildDefinitions(druidCards);
        public List<HexCardDefinition> BuildCommon() => BuildDefinitions(commonCards);
        public List<HexCardDefinition> BuildEnemy() => BuildDefinitions(enemyCards);
    }
}
