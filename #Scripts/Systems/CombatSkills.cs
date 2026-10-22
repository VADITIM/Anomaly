using Godot;

namespace Anomaly;

// The combat arts Kio learns with level points. The string starts three strikes long: the trunk of the tree adds the
// fourth strike, then the fifth, and only then do the four branches open out of the Resonance ring, each an inner art
// that leads to an outer one. The charged arts fire by holding Strike through a strike of the string.
public enum CombatSkill
{
    FourthStrike,
    FifthStrike,
    RisingReap,
    Juggle,
    SkyRhythm,
    MeteorSlam,
    Momentum,
    Echo,
    Whirlwind,
    Cataclysm,
}

// Branch TRUNK is the combo trunk; Outer marks the second node of a branch or trunk.
public sealed record CombatSkillInfo(CombatSkill Skill, string Name, string Line, string Detail, int Cost, CombatSkill? Requires, int Branch, bool Outer);

public static class CombatSkills
{
    public const int TRUNK = -1;
    public const int BRANCHES = 4;

    // Listed in CombatSkill order. Branches run up-left, up-right, down-left and down-right from the ring.
    public static readonly CombatSkillInfo[] All =
    {
        new(CombatSkill.FourthStrike, "4TH STRIKE", "The string grows to four strikes", "A lunging buzzsaw that grinds whatever it pins", 1, null, TRUNK, false),
        new(CombatSkill.FifthStrike, "5TH STRIKE", "The string grows to five strikes", "A leaping overhead slam, the Arc's manifestation. Opens the four paths", 1, CombatSkill.FourthStrike, TRUNK, true),
        new(CombatSkill.RisingReap, "RISING REAP", "Hold Strike on the 1st hit", "An upward reap that launches foes and carries Kio up after them", 1, CombatSkill.FifthStrike, 0, false),
        new(CombatSkill.Juggle, "JUGGLE", "Air strikes lift foes", "Strikes thrown in the air pop foes up and keep them aloft", 2, CombatSkill.RisingReap, 0, true),
        new(CombatSkill.SkyRhythm, "SKY RHYTHM", "Air strings hang longer", "Air strikes cost half stamina and landed hits hold Kio aloft longer", 1, CombatSkill.FifthStrike, 1, false),
        new(CombatSkill.MeteorSlam, "METEOR SLAM", "Hold Strike in the air", "Hang, charge, then plunge. The higher the fall, the wider and harder the impact", 2, CombatSkill.SkyRhythm, 1, true),
        new(CombatSkill.Momentum, "MOMENTUM", "Each strike hits harder", "Every strike deeper into the string deals more damage", 1, CombatSkill.FifthStrike, 2, false),
        new(CombatSkill.Echo, "ECHO", "The finisher strikes twice", "A ghost of the finisher lands a moment later where it struck", 2, CombatSkill.Momentum, 2, true),
        new(CombatSkill.Whirlwind, "WHIRLWIND", "Hold Strike on the 3rd hit", "A spinning cyclone that drifts forward and cuts everything around Kio", 1, CombatSkill.FifthStrike, 3, false),
        new(CombatSkill.Cataclysm, "CATACLYSM", "Hold Strike on the 5th hit", "A leaping slam that cracks the ground in a wide shockwave, then an aftershock", 2, CombatSkill.Whirlwind, 3, true),
    };

    public static CombatSkillInfo Info(CombatSkill skill) => All[(int)skill];

    public static Color BranchColor(int branch) => branch switch
    {
        TRUNK => new Color(0.9f, 0.86f, 0.78f),
        0 => new Color(0.93f, 0.75f, 0.47f),
        1 => new Color(0.8f, 0.5f, 1f),
        2 => new Color(0.95f, 0.25f, 0.25f),
        _ => new Color(1f, 0.62f, 0.1f),
    };

    public static bool Has(CombatSkill skill) => SaveManager.PlayerStats.Skills.Contains(skill.ToString());

    public static bool Unlockable(CombatSkill skill)
    {
        CombatSkillInfo info = Info(skill);
        return !Has(skill) && (info.Requires is not CombatSkill required || Has(required));
    }

    public static void Learn(CombatSkill skill)
    {
        if (!Has(skill))
            SaveManager.PlayerStats.Skills.Add(skill.ToString());
    }
}
