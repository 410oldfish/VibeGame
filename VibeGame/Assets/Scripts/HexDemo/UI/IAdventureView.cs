using System;
using System.Collections.Generic;

namespace HexDemo
{
    internal interface IAdventureView
    {
        bool IsOverlayOpen { get; }
        void Initialize();
        void ShowProfessionSelection(string networkStatus, Action<HexCardProfession> choose);
        void HideProfessionSelection();
        void BuildMap(HexMapData map, string summary, string currentNodeId, ISet<string> visited, Action<string> enterNode);
        void RefreshMap(string summary, HexMapData map, string currentNodeId, ISet<string> visited);
        void ShowMap();
        void HideMapForRoom();
        void ShowOverlay(string title, string body, IReadOnlyList<HexUiChoice> choices);
        void ShowShop(IReadOnlyList<HexShopOfferView> offers, Func<int, bool> purchase, Func<int> getGold, Action leave);
        void ClearOverlay();
    }
}
