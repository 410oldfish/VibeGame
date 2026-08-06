using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HexDemo
{
    public enum HexSandboxEnemyType
    {
        [InspectorName("哥布林")] Goblin = 0,
        [InspectorName("投矛哥布林")] SpearGoblin = 1,
        [InspectorName("哥布林队长")] GoblinCaptain = 2,
        [InspectorName("部落酋长")] TribalChieftain = 3,
        [InspectorName("骷髅")] Skeleton = 4,
        [InspectorName("寄生藤蔓")] ParasiticVine = 5,
        [InspectorName("活墙壁")] LivingWall = 6,
        [InspectorName("石像鬼")] Gargoyle = 7,
        [InspectorName("地狱犬")] Hellhound = 8,
        [InspectorName("宝箱怪")] Mimic = 9,
        [InspectorName("夺心魔")] MindFlayer = 10,
        [InspectorName("兽人战士")] OrcWarrior = 11,
    }

    public static class HexSandboxEnemyTypeExtensions
    {
        public static string ToDefinitionId(this HexSandboxEnemyType enemyType) => enemyType switch
        {
            HexSandboxEnemyType.Goblin => "goblin",
            HexSandboxEnemyType.SpearGoblin => "spear_goblin",
            HexSandboxEnemyType.GoblinCaptain => "goblin_captain",
            HexSandboxEnemyType.TribalChieftain => "tribal_chieftain",
            HexSandboxEnemyType.Skeleton => "skeleton",
            HexSandboxEnemyType.ParasiticVine => "parasitic_vine",
            HexSandboxEnemyType.LivingWall => "living_wall",
            HexSandboxEnemyType.Gargoyle => "gargoyle",
            HexSandboxEnemyType.Hellhound => "hellhound",
            HexSandboxEnemyType.Mimic => "mimic",
            HexSandboxEnemyType.MindFlayer => "mind_flayer",
            HexSandboxEnemyType.OrcWarrior => "orc_warrior",
            _ => throw new ArgumentOutOfRangeException(nameof(enemyType), enemyType, null),
        };

        public static bool TryFromDefinitionId(string id, out HexSandboxEnemyType enemyType)
        {
            enemyType = HexSandboxEnemyType.Goblin;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            switch (id.Trim().ToLowerInvariant())
            {
                case "goblin": enemyType = HexSandboxEnemyType.Goblin; return true;
                case "spear_goblin": enemyType = HexSandboxEnemyType.SpearGoblin; return true;
                case "goblin_captain": enemyType = HexSandboxEnemyType.GoblinCaptain; return true;
                case "tribal_chieftain": enemyType = HexSandboxEnemyType.TribalChieftain; return true;
                case "skeleton": enemyType = HexSandboxEnemyType.Skeleton; return true;
                case "parasitic_vine": enemyType = HexSandboxEnemyType.ParasiticVine; return true;
                case "living_wall": enemyType = HexSandboxEnemyType.LivingWall; return true;
                case "gargoyle": enemyType = HexSandboxEnemyType.Gargoyle; return true;
                case "hellhound": enemyType = HexSandboxEnemyType.Hellhound; return true;
                case "mimic": enemyType = HexSandboxEnemyType.Mimic; return true;
                case "mind_flayer": enemyType = HexSandboxEnemyType.MindFlayer; return true;
                case "orc_warrior": enemyType = HexSandboxEnemyType.OrcWarrior; return true;
                default: return false;
            }
        }
    }

    [CreateAssetMenu(menuName = "HexDemo/Debug/Battle Sandbox Scenario", fileName = "BattleSandboxScenario")]
    public sealed class HexBattleSandboxScenarioSO : ScriptableObject
    {
        public bool useFixedRandomSeed = true;
        public int randomSeed = 1337;
        public bool quickBottomCard;

        [Serializable]
        public sealed class TerrainOverride
        {
            public Vector2Int coord;
            public HexTerrainZoneType zone = HexTerrainZoneType.Normal;
            [Tooltip("Obsolete alias field. Prefer zone.")]
            public HexTerrainBaseType baseTerrain = HexTerrainBaseType.Ground;
            public HexTerrainStructureType structureType = HexTerrainStructureType.None;
            public string propId = string.Empty;
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
        public sealed class EnemyConfig : ISerializationCallbackReceiver
        {
            public HexSandboxEnemyType enemyType = HexSandboxEnemyType.Goblin;
            [FormerlySerializedAs("enemyDefinitionId"), SerializeField, HideInInspector]
            private string legacyEnemyDefinitionId = string.Empty;
            public string displayNameOverride = string.Empty;
            public Vector2Int spawnCoord = new(7, 5);
            [Tooltip("仅活墙壁使用：第二面主墙的本体格。")]
            public Vector2Int livingWallPartnerSpawnCoord = new(3, 5);
            public int maxHealthOverride = -1;
            public int currentHealthOverride = -1;
            public List<string> deckCardIds = new();

            public string DefinitionId => string.IsNullOrWhiteSpace(legacyEnemyDefinitionId)
                ? enemyType.ToDefinitionId()
                : legacyEnemyDefinitionId;

            public bool MigrateLegacyId()
            {
                if (string.IsNullOrWhiteSpace(legacyEnemyDefinitionId) ||
                    !HexSandboxEnemyTypeExtensions.TryFromDefinitionId(legacyEnemyDefinitionId, out var migrated))
                    return false;

                enemyType = migrated;
                legacyEnemyDefinitionId = string.Empty;
                return true;
            }

            public void OnBeforeSerialize()
            {
            }

            public void OnAfterDeserialize()
            {
                MigrateLegacyId();
            }
        }

        public TerrainConfig terrain = new();
        public PlayerConfig player = new();
        public List<EnemyConfig> enemies = new();

        private void OnValidate()
        {
            if (enemies == null)
                return;

            for (int i = 0; i < enemies.Count; i++)
                enemies[i]?.MigrateLegacyId();
        }
    }
}
