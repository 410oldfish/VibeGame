using UnityEditor;
using UnityEngine;

namespace HexDemo.Editor
{
    [CustomPropertyDrawer(typeof(HexBattleSandboxScenarioSO.EnemyConfig))]
    public sealed class HexBattleSandboxEnemyConfigDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                label,
                true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            DrawProperty(ref y, position, property.FindPropertyRelative("enemyType"));
            DrawProperty(ref y, position, property.FindPropertyRelative("displayNameOverride"));
            DrawProperty(ref y, position, property.FindPropertyRelative("spawnCoord"));

            var enemyType = property.FindPropertyRelative("enemyType");
            if (enemyType != null && enemyType.intValue == (int)HexSandboxEnemyType.LivingWall)
                DrawProperty(ref y, position, property.FindPropertyRelative("livingWallPartnerSpawnCoord"));

            DrawProperty(ref y, position, property.FindPropertyRelative("maxHealthOverride"));
            DrawProperty(ref y, position, property.FindPropertyRelative("currentHealthOverride"));
            DrawProperty(ref y, position, property.FindPropertyRelative("deckCardIds"), true);
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += Height(property.FindPropertyRelative("enemyType"));
            height += Height(property.FindPropertyRelative("displayNameOverride"));
            height += Height(property.FindPropertyRelative("spawnCoord"));
            var enemyType = property.FindPropertyRelative("enemyType");
            if (enemyType != null && enemyType.intValue == (int)HexSandboxEnemyType.LivingWall)
                height += Height(property.FindPropertyRelative("livingWallPartnerSpawnCoord"));
            height += Height(property.FindPropertyRelative("maxHealthOverride"));
            height += Height(property.FindPropertyRelative("currentHealthOverride"));
            height += Height(property.FindPropertyRelative("deckCardIds"), true);
            return height;
        }

        private static void DrawProperty(ref float y, Rect total, SerializedProperty property, bool includeChildren = false)
        {
            if (property == null)
                return;
            float height = EditorGUI.GetPropertyHeight(property, includeChildren);
            EditorGUI.PropertyField(new Rect(total.x, y, total.width, height), property, includeChildren);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float Height(SerializedProperty property, bool includeChildren = false)
        {
            return property == null
                ? 0f
                : EditorGUI.GetPropertyHeight(property, includeChildren) + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
