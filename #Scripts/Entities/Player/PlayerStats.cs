using Godot;

namespace Anomaly;

[GlobalClass]
public partial class PlayerStats : Resource
{
    [Export] public SavePoint LastSavePoint = SavePoint.None;

    // Resonance: Kio's level. Residue is progress toward the next one; points are attuned at the Rend Anvil.
    [Export] public int Resonance = 1;
    [Export] public int Residue = 0;
    [Export] public int UnspentPoints = 0;
    [Export] public int Vitality = 0;
    [Export] public int Endurance = 0;
    [Export] public int Might = 0;
    [Export] public int Resolve = 0;
    // Learned combat arts, by CombatSkill name so reordering the enum never scrambles a save.
    [Export] public Godot.Collections.Array<string> Skills = new();

    public float EffectiveMaxHealth(EntityStats baseStats) =>
        LevelingSystem.MaxHealth(this, baseStats.MaxHealth);

    public float VesselPotency(float basePotency) =>
        basePotency + Resolve * LevelingSystem.POTENCY_PER_RESOLVE;

    public int GetAspect(Aspect aspect) => aspect switch
    {
        Aspect.Vitality => Vitality,
        Aspect.Endurance => Endurance,
        Aspect.Might => Might,
        _ => Resolve,
    };

    public void RaiseAspect(Aspect aspect)
    {
        switch (aspect)
        {
            case Aspect.Vitality:
                Vitality++;
                break;
            case Aspect.Endurance:
                Endurance++;
                break;
            case Aspect.Might:
                Might++;
                break;
            default:
                Resolve++;
                break;
        }
    }
}
