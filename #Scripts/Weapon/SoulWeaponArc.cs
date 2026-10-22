using Godot;

namespace Anomaly;

[GlobalClass]
public partial class SoulWeaponArc : Resource
{
    [Export] public string ArcId = "soul_scythe_arc";
    [Export] public string ArcName = "Soul Scythe Arc";
    [Export] public DamageType PrimaryDamageType = DamageType.Slashing;
    [Export] public float BaseEfficiency = 0.95f;
    [Export] public float MatchedEfficiency = 1.30f;
    [Export] public DamageType SpecialMoveDamageType = DamageType.Piercing;
    [Export] public SpecialMoveKind SpecialMove = SpecialMoveKind.ScytheThrow;
    [Export] public PackedScene SpecialMoveEffect;
    [Export] public float SpecialCooldown = 4f;
    [Export] public float SpecialDamageMultiplier = 1.4f;
    [Export] public float ManifestDamageMultiplier = 1.15f;
    [Export] public float ManifestKnockbackMultiplier = 1f;
    [Export] public float ManifestTenacityMultiplier = 1f;
    [Export] public float ManifestRangeBonus = 0f;
    [Export] public float ManifestArcDegrees = 220f;
    [Export] public Color TrailColor = new(0.75f, 0.95f, 1f);
    [Export] public Texture2D Icon;
    [Export(PropertyHint.MultilineText)] public string Description = "";
}
