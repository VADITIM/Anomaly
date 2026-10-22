using Godot;

namespace Anomaly;

public enum HitSource
{
    Player,
    Enemy,
    Environment,
}

public sealed class HitInfo
{
    public float Damage;
    public DamageType DamageType;
    public Vector2 Direction;
    public float Knockback;
    public float TenacityDamage;
    public float Lift;
    public float HitStop = 0.035f;
    public float Efficiency = 1f;
    public bool IsCritical;
    public bool IsFinisher;
    // Landed by the strike on which the equipped Arc manifests.
    public bool IsArcInterval;
    public bool IsWeaknessHit;
    public float StaminaCost;
    public HitSource Source;
    public Node2D Attacker;
    public Vector2 ContactPoint;
}

public enum HitResponse
{
    Ignored,
    Landed,
    Dodged,
    PerfectDodged,
    Killed,
}

public interface IHittable
{
    Vector2 GroundPosition { get; }
    float Z { get; }
    float HurtRadius { get; }
    float HeightSpan { get; }
    bool CanReceivePlayerHits { get; }
    HitResponse ReceiveHit(HitInfo hit);
}
