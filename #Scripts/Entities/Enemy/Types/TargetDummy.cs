using Godot;

namespace Anomaly;

// Practice target: rooted, harmless and unkillable. It runs the full enemy hit pipeline (numbers, bar, Tenacity,
// weakness) and shows the running damage of the current string so swings and Arcs can be compared.
public partial class TargetDummy : Enemy
{
    private const string TEXTURE_PATH = "Entities/Enemies/Dummy/Practice_Dummy.png";
    private const int FRAME_COUNT = 17;
    private const float RESET_AFTER = 3f;
    private static readonly (int First, int Last, float Fps) IDLE = (0, 3, 6f);
    private static readonly (int First, int Last, float Fps) TAKE_DAMAGE = (4, 8, 16f);
    private static readonly (int First, int Last, float Fps) SPIN = (9, 16, 18f);

    private (int First, int Last, float Fps) clip = IDLE;
    private float clipTime;
    private float clock;
    private float stringStart;
    private float stringDamage;
    private int stringHits;
    private Label readout;

    public override string DisplayName => "Practice Dummy";
    protected override string SpriteFolder => "Dummy";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/TargetDummy.tres";
    protected override float SpriteFeetOffset => 15f;
    protected override float BarHeight => 6f;
    protected override bool UsesAnimationSet => false;
    protected override Color IchorColor => new(0.85f, 0.72f, 0.5f);
    protected override float BaseCameraPriority => 0.3f;
    public override bool IsCombatant => false;

    protected override void ConfigureEnemy()
    {
        Radius = 6f;
        HurtRadius = 9f;
        HeightSpan = 22f;
        MoveSpeed = 0f;
        AggroRadius = 0f;
        FlinchResistance = 9999f;
        Weakness = DamageType.Piercing;
        SoulReward = 0;
        ResidueReward = 0;
        Sprite.Texture = Art.Texture(TEXTURE_PATH);
        Sprite.Hframes = FRAME_COUNT;

        readout = UiFactory.Label("", Art.BodyFont, 8, new Color(1f, 0.92f, 0.75f), 2);
        readout.HorizontalAlignment = HorizontalAlignment.Center;
        readout.Position = new Vector2(-40f, 4f);
        readout.Size = new Vector2(80f, 10f);
        readout.Modulate = new Color(1f, 1f, 1f, 0f);
        Body.AddChild(readout);
    }

    protected override float ComputeMaxHealth() => Stats.MaxHealth;

    public override HitResponse ReceiveHit(HitInfo hit)
    {
        // Never dies: a finishing hit shatters the bar like a kill, and it reassembles full.
        if (!IsDead && hit.Damage >= Health)
        {
            ResourceBar.Break(hit.Direction, true);
            Health = MaxHealth + hit.Damage;
        }
        return base.ReceiveHit(hit);
    }

    protected override void TickEntity(float delta)
    {
        base.TickEntity(delta);
        clock += delta;
        MoveVelocity = Vector2.Zero;

        if (TimeSinceDamaged > RESET_AFTER)
        {
            if (IsAggroed)
                Calm();
            if (Health < MaxHealth)
                RestoreHealth(MaxHealth * delta * 1.5f);
            stringHits = 0;
            readout.Modulate = new Color(1f, 1f, 1f, Mathf.MoveToward(readout.Modulate.A, 0f, delta * 2f));
        }

        clipTime += delta;
        int length = clip.Last - clip.First + 1;
        int frame = (int)(clipTime * clip.Fps);
        if (clip != IDLE && frame >= length)
        {
            clip = IDLE;
            clipTime = 0f;
            frame = 0;
        }
        Sprite.Frame = clip.First + frame % length;
    }

    protected override void OnHit(HitInfo hit, ImpactResult impact)
    {
        base.OnHit(hit, impact);
        if (Mathf.Abs(hit.Direction.X) > 0.1f)
            Sprite.FlipH = hit.Direction.X < 0f;
        clip = hit.IsFinisher || impact.TenacityShattered ? SPIN : TAKE_DAMAGE;
        clipTime = 0f;

        if (stringHits == 0)
        {
            stringStart = clock;
            stringDamage = 0f;
        }
        stringHits++;
        stringDamage += hit.Damage;
        float elapsed = Mathf.Max(clock - stringStart, 1f);
        readout.Text = $"{Mathf.RoundToInt(stringDamage)} in {stringHits}  ·  {Mathf.RoundToInt(stringDamage / elapsed)} dps";
        readout.Modulate = Colors.White;
    }

    protected override void ChaseBehaviour(float delta) => Brake(delta, 9999f);

    protected override bool WantsToAttack() => false;

    protected override void OnAttackStart() { }

    protected override bool UpdateAttack(float delta) => true;
}
