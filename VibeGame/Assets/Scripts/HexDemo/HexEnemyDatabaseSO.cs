using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    [CreateAssetMenu(fileName = "HexEnemyDatabase", menuName = "HexDemo/Enemy Database", order = 2)]
    public sealed class HexEnemyDatabaseSO : ScriptableObject
    {
        public List<HexEnemyDefinitionSO> enemies = new();

        public bool TryBuild(string id, out HexEnemyDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id) || enemies == null)
                return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                var candidate = enemies[i];
                if (candidate == null || !string.Equals(candidate.id, id, StringComparison.OrdinalIgnoreCase))
                    continue;

                definition = candidate.ToDefinition();
                return definition != null;
            }

            return false;
        }
    }
}
