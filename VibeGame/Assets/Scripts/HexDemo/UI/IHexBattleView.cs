using UnityEngine;

namespace HexDemo
{
    internal interface IBattleHudView
    {
        GameObject Host { get; }
        void Initialize(HexBattleController controller);
        void Refresh();
        bool IsEnemyIntentPopupOpen();
        void OpenEnemyHandPopup(HexBattleUnit enemy, Vector2 screenPosition);
        bool IsBlockingWorldClick();
        void OpenTerrainDetailPopup(HexTile tile, Vector2 screenPosition);
        void CloseTerrainDetailPopup();
        void CloseEnemyHandPopup();
        void CloseTopModal();
        void ShowFloatingCombatText(HexBattleUnit unit, HexFloatingFeedbackKind kind, int amount);
        void ShowPlayedCard(HexBattleUnit source, HexCardInstance card);
    }
}
