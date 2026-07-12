using UnityEngine;
using UnityEngine.UI;

namespace HexDemo
{
    public sealed partial class HexBattleUI
    {
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        private void ApplyReferenceLayout1920(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            // Top band: player/status on the left, turn in the center, deck/log on the right.
            SetRect(FindByPath<RectTransform>(canvasRoot, "HUD/Status"), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(540f, 52f));
            if (_playerStatusBar?.Root != null)
                SetRect(_playerStatusBar.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(900f, 40f));
            SetRect(FindByPath<RectTransform>(canvasRoot, "HUD/Turn"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(260f, 54f));
            SetRect(FindByPath<RectTransform>(canvasRoot, "HUD/Deck"), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(270f, 52f));
            SetRect(canvasRoot.Find("PlayLog") as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -82f), new Vector2(170f, 68f));

            // Left information column. Consumables sit below status; resources sit below consumables.
            SetRect(_consumableBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -104f), new Vector2(554f, 140f));
            SetRect(_consumableTooltip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(586f, -104f), new Vector2(360f, 172f));
            SetRect(canvasRoot.Find("ResourcePanel") as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -252f), new Vector2(240f, 92f));

            // Bottom interaction band. Card hand owns the center; pile buttons stay in a left cluster.
            SetRect(canvasRoot.Find("HandPanel") as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1120f, 292f));
            if (_handRoot != null)
                _handRoot.sizeDelta = new Vector2(1080f, 250f);
            SetRect(canvasRoot.Find("DrawPile") as RectTransform, Vector2.zero, Vector2.zero, new Vector2(18f, 24f), new Vector2(160f, 104f));
            SetRect(canvasRoot.Find("DiscardPile") as RectTransform, Vector2.zero, Vector2.zero, new Vector2(186f, 24f), new Vector2(160f, 104f));
            SetRect(canvasRoot.Find("ExhaustPile") as RectTransform, Vector2.zero, Vector2.zero, new Vector2(18f, 136f), new Vector2(160f, 104f));
            SetRect(canvasRoot.Find("ActionPanel") as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(220f, 88f));

            // Enemy hand overlay must cover the entire reference canvas so any click can close it.
            if (_enemyHandOverlay != null)
            {
                _enemyHandOverlay.anchorMin = Vector2.zero;
                _enemyHandOverlay.anchorMax = Vector2.one;
                _enemyHandOverlay.pivot = new Vector2(0.5f, 0.5f);
                _enemyHandOverlay.anchoredPosition = Vector2.zero;
                _enemyHandOverlay.offsetMin = Vector2.zero;
                _enemyHandOverlay.offsetMax = Vector2.zero;
            }

            SetRect(_enemyHandPopup, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(660f, 330f));
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
