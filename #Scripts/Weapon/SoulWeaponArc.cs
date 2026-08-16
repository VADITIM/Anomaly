using Godot;

// Designer-authored Arc data (design.md §3.5). Pure data — holds no runtime
// state, so one .tres is shared safely across every Scythe instance.
[GlobalClass]
public partial class SoulWeaponArc : Resource
{
    [Export] public string ArcName { get; set; } = "Soul Scythe Arc";
    [Export] public WeaponAttackType AttackType { get; set; } = WeaponAttackType.Slashing;

    [ExportGroup("Combat Multipliers")]
    [Export] public float DamageMultiplier { get; set; } = 1f;
    [Export] public float TenacityMultiplier { get; set; } = 1f;
    [Export] public float PenetrationMultiplier { get; set; } = 1f;
    [Export] public float StaminaCostMultiplier { get; set; } = 1f;
    [Export] public float HeavyStaminaCostMultiplier { get; set; } = 1f;
    [Export] public float StaminaRestoreMultiplier { get; set; } = 1f;

    [ExportGroup("Impact")]
    [Export] public float Knockback { get; set; } = 0f;
    [Export] public float PlayerPushForce { get; set; } = 0f;

    // The Arc's heavy attack — design.md §3.5 "Special Move". Cooldown source is
    // still [UNDEFINED]; SpecialCooldownDuration is the interim gate.
    [ExportGroup("Heavy Attack")]
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float HeavyAttackDuration { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,10,0.05")] public float SpecialCooldownDuration { get; set; } = 0.5f;

    // Special hit: every Nth landed swing triggers the Arc's signature effect.
    // The counter runs continuously and is NOT reset when the combo wraps, so an
    // interval of 3 against a 4-step combo lands on combo steps 3, 2, 1, 4, ...
    [ExportGroup("Special Hit")]
    [Export(PropertyHint.Range, "1,10,1")] public int SpecialHitInterval { get; set; } = 4;
    [Export] public SpecialHitKind SpecialHit { get; set; } = SpecialHitKind.None;
    [Export] public PackedScene SpecialHitEffect { get; set; }
    [Export] public float SpecialHitTenacityMultiplier { get; set; } = 1.2f;

    // MultiHit: extra damage applications spread over MultiHitWindow.
    [Export(PropertyHint.Range, "1,8,1")] public int MultiHitCount { get; set; } = 4;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float MultiHitWindow { get; set; } = 0.2f;

    // DelayedShatter: an area burst that lands ShatterDelay after the swing.
    [Export(PropertyHint.Range, "0,2,0.05")] public float ShatterDelay { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "8,256,1")] public float ShatterRadius { get; set; } = 64f;
    [Export] public float ShatterDamageMultiplier { get; set; } = 1.5f;

    // Area bursts are the exception to same-plane combat: how many elevations above and below the
    // impact plane the shatter still threatens. 0 keeps it on the caster's plane.
    [Export(PropertyHint.Range, "0,10,1")] public float ShatterElevationSpan { get; set; } = 0f;
}
