using System;
using UnityEngine;

namespace HexDemo
{
    [Serializable]
    public sealed class HexCardUiRegionLayout
    {
        [Tooltip("以卡牌左上角为原点的归一化区域，范围 0-1。")]
        public Rect normalizedRect;

        [Min(8f)]
        public float fontSize;

        public HexCardUiRegionLayout(Rect normalizedRect, float fontSize)
        {
            this.normalizedRect = normalizedRect;
            this.fontSize = fontSize;
        }

        public HexCardUiRegionLayout Clone() => new(normalizedRect, fontSize);
    }

    [CreateAssetMenu(fileName = "WarriorCardLayout", menuName = "Hex Demo/UI/Warrior Card Layout")]
    public sealed class HexCardUiLayoutSettings : ScriptableObject
    {
        public const float CardAspectRatio = 1086f / 1448f;

        public Vector2Int referenceSize = new(1086, 1448);
        public HexCardUiRegionLayout cost = DefaultCost();
        public HexCardUiRegionLayout title = DefaultTitle();
        public HexCardUiRegionLayout description = DefaultDescription();

        public static HexCardUiRegionLayout DefaultCost() =>
            new(new Rect(0.035f, 0.035f, 0.225f, 0.165f), 48f);

        public static HexCardUiRegionLayout DefaultTitle() =>
            new(new Rect(0.255f, 0.055f, 0.565f, 0.105f), 30f);

        public static HexCardUiRegionLayout DefaultDescription() =>
            new(new Rect(0.155f, 0.49f, 0.69f, 0.39f), 28f);

        public void ResetToDefaults()
        {
            referenceSize = new Vector2Int(1086, 1448);
            cost = DefaultCost();
            title = DefaultTitle();
            description = DefaultDescription();
        }

        public HexCardUiLayoutSettings CreateRuntimeCopy()
        {
            var copy = CreateInstance<HexCardUiLayoutSettings>();
            copy.name = name + " (Runtime Copy)";
            copy.referenceSize = referenceSize;
            copy.cost = (cost ?? DefaultCost()).Clone();
            copy.title = (title ?? DefaultTitle()).Clone();
            copy.description = (description ?? DefaultDescription()).Clone();
            return copy;
        }
    }
}
