using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    [CreateAssetMenu(menuName = "HexDemo/Debug/Battle Sandbox Scenario", fileName = "BattleSandboxScenario")]
    public sealed class HexBattleSandboxScenarioSO : ScriptableObject
    {
        [Serializable]
        public sealed class TerrainOverride
        {
            public Vector2Int coord;
            public HexTerrainBaseType baseTerrain = HexTerrainBaseType.Ground;
            public HexTerrainStructureType structureType = HexTerrainStructureType.None;
            public int structureHp = 0;
            public HexTerrainPickupType pickupType = HexTerrainPickupType.None;
            public int pickupAmount = 0;
        }

        [Serializable]
        public sealed class TerrainConfig
        {
            public int width = 11;
            public int height = 11;
            public float hexSize = 0.55f;
            public float tileY = 0f;
            public float tileDepth = 0.45f;
            public float heightStep = 0f;
            public bool generateFeatureTerrain = true;
            public float highGroundChance = 0.08f;
            public float ruinChance = 0.05f;
            public List<TerrainOverride> overrides = new();
        }

        [Serializable]
        public sealed class PlayerConfig
        {
            public HexCardProfession profession = HexCardProfession.Warrior;
            public string displayName = "Hero";
            public int maxHealth = 70;
            public int currentHealth = 70;
            public int maxEnergy = 3;
            public int drawPerTurn = 4;
            public int maxMovePoints = 2;
            public int attackRange = 1;
            public Vector2Int spawnCoord = new(3, 5);
            public List<string> deckCardIds = new();
        }

        [Serializable]
        public sealed class EnemyConfig
        {
            public string enemyDefinitionId = "goblin";
            public string displayNameOverride = string.Empty;
            public Vector2Int spawnCoord = new(7, 5);
            public int maxHealthOverride = -1;
            public int currentHealthOverride = -1;
            public List<string> deckCardIds = new();
        }

        public TerrainConfig terrain = new();
        public PlayerConfig player = new();
        public List<EnemyConfig> enemies = new();
    }
}
