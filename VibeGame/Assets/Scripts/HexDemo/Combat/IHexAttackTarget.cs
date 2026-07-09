namespace HexDemo
{
    public enum HexAttackTargetKind
    {
        Unit = 0,
        Structure = 1,
    }

    public interface IHexAttackTarget
    {
        HexAxialCoord TargetCoord { get; }
        bool IsAttackTargetValid { get; }
        HexAttackTargetKind AttackTargetKind { get; }
    }
}
