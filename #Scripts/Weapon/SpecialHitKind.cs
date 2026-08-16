// Which signature effect an Arc fires on its special-hit interval. The Resource
// selects the kind and supplies its tuning; execution lives in Weapon.SpecialHit.
public enum SpecialHitKind
{
    None,
    MultiHit,
    DelayedShatter
}
