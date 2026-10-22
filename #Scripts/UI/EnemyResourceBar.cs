using Godot;

namespace Anomaly;

// Health with a damage ghost (shown while the enemy is wounded at all, in a fight or not), Tenacity beneath (white while filling, gold while the stagger window is open),
// and level arches above: stage 1 has none, each further stage adds one.
// Every change is a single-shot consequence on its own clock, run on real time so hitstop never delays it:
// damage cuts the bar on the frame of the hit and leaves a white slab that collapses after the string,
// a heal sweeps in behind a leading slab, a stagger breaks the Tenacity strip, and death shatters the bar.
public partial class EnemyResourceBar : Node2D
{
    public const float WIDTH = 33f;
    public static readonly Vector2 TENACITY_ORIGIN = new(0, 4);
    public static readonly Color GHOST_COLOR = new(0.95f, 0.2f, 0.26f);
    public static readonly Color HEAL_COLOR = new(0.55f, 0.95f, 1f);
    public static readonly Color STAGGER_COLOR = new(1f, 0.82f, 0.35f);

    private const float VISIBLE_AFTER_DAMAGE = 3.5f;
    private static readonly Vector2 PIVOT = new(WIDTH * 0.5f, 4.5f);

    private const float FLASH_TIME = 0.05f;
    private const float PUNCH_TIME = 0.24f;
    private const float SHAKE_STEP = 0.035f;
    private const float GHOST_HOLD = 0.16f;
    private const float GHOST_COLLAPSE = 0.28f;
    private const float HEAL_HOLD = 0.06f;
    private const float HEAL_SWEEP = 0.22f;
    private const float DISCRETE_HEAL = 0.025f;
    private const float TENACITY_FLASH = 0.1f;
    private const float STAGGER_SWEEP = 0.18f;
    private const float APPEAR_TIME = 0.22f;
    private const float LEAVE_TIME = 0.14f;
    private const float HIDDEN_SCALE = 0.2f;
    private const float BREAK_HOLD = 0.07f;
    private const float ASSEMBLE_DELAY = 0.45f;
    private const float ASSEMBLE_TIME = 0.24f;
    private const float ASSEMBLE_STEP = 0.025f;
    private const float ASSEMBLE_DROP = 12f;
    private static readonly float[] SHAKE_STEPS = { 1f, -0.6f, 0.3f };

    private Enemy owner;
    private ShaderMaterial material;

    private float health = 1f;
    private float ghost = 1f;
    private float ghostFrom = 1f;
    private float ghostClock = 99f;
    private float lead = 1f;
    private bool healing;
    private float healFrom;
    private float healTo;
    private float healClock;

    private float flashTime;
    private Color flashColor = Colors.White;
    private float punchClock = 99f;
    private float punchStrength;
    private float shakeClock = 99f;
    private float shakeAmplitude;
    private float hitSign = 1f;

    private float tenacitySeen;
    private float tenacityFlashFrom;
    private float tenacityFlashTime;
    private bool wasStaggered;
    private float staggerClock;

    private bool shown;
    private float presenceClock = 99f;
    private float presenceFrom = HIDDEN_SCALE;
    private float presenceScale = HIDDEN_SCALE;

    private float breakClock = -1f;
    private Vector2 breakDirection;
    private bool reassemble;
    private bool broken;
    private float assembleClock;
    private float[] columns;
    private float[] columnDelays;

    public void Setup(Enemy enemy, float height)
    {
        owner = enemy;
        Position = new Vector2(-WIDTH * 0.5f, height);
        ZIndex = 5;
        ZAsRelative = true;
    }

    public override void _Ready()
    {
        material = new ShaderMaterial { Shader = ShaderLibrary.Entity };
        Material = material;
        Visible = false;
    }

    public void OnDamaged(HitInfo hit)
    {
        if (Mathf.Abs(hit.Direction.X) > 0.1f)
            hitSign = Mathf.Sign(hit.Direction.X);
        Sync(0f);
    }

    public void OnKilled(HitInfo hit) => Break(hit.Direction, false);

    // Freezes on a white flash for a beat, then splits into falling columns; a reassembling bar rains back in.
    public void Break(Vector2 direction, bool rebuild)
    {
        if (breakClock >= 0f || broken)
            return;
        breakDirection = direction == Vector2.Zero ? Vector2.Up : direction.Normalized();
        reassemble = rebuild;
        breakClock = 0f;
        shown = true;
        presenceScale = 1f;
        presenceClock = 99f;
        ghostFrom = ghost = Mathf.Max(ghost, health);
        health = 0f;
        lead = 0f;
        healing = false;
        ghostClock = 0f;
        Flash(Colors.White, BREAK_HOLD + 0.02f);
        Punch(0.35f);
        Shake(3f);
    }

    public override void _Process(double delta)
    {
        Sync(Motion.Unscaled(delta));
        QueueRedraw();
    }

    private void Sync(float dt)
    {
        flashTime -= dt;
        punchClock += dt;
        shakeClock += dt;
        material.SetShaderParameter(StringNames.Of("flash"), flashTime > 0f ? 1f : 0f);
        material.SetShaderParameter(StringNames.Of("flash_color"), flashColor);

        if (breakClock >= 0f)
        {
            breakClock += dt;
            ghostClock += dt;
            Visible = true;
            Modulate = Colors.White;
            if (breakClock >= BREAK_HOLD)
                Shatter();
            return;
        }
        if (broken)
        {
            UpdateAssembly(dt);
            return;
        }

        UpdatePresence(dt);
        TrackHealth(dt);
        TrackTenacity(dt);
    }

    private void UpdatePresence(float dt)
    {
        bool shouldShow = !owner.IsDead && (owner.TimeSinceDamaged < VISIBLE_AFTER_DAMAGE || owner.Impact.IsStaggered || owner.HealthRatio < 0.999f);
        if (shouldShow != shown)
        {
            shown = shouldShow;
            presenceFrom = presenceScale;
            presenceClock = 0f;
        }
        presenceClock += dt;
        if (shown)
        {
            presenceScale = Mathf.Lerp(presenceFrom, 1f, Motion.BackOut(presenceClock / APPEAR_TIME));
            Visible = true;
        }
        else
        {
            float t = presenceClock / LEAVE_TIME;
            presenceScale = Mathf.Lerp(presenceFrom, HIDDEN_SCALE, Motion.Power2In(t));
            Visible = t < 1f;
        }
        Modulate = Colors.White;
    }

    private void TrackHealth(float dt)
    {
        float target = owner.HealthRatio;
        if (target < health - 0.0001f)
            LandDamage(health - target, target);
        else if (target > lead + 0.0001f)
            BeginHeal(target);
        else if (target < lead)
            lead = healTo = target;

        if (healing)
        {
            healClock += dt;
            health = Mathf.Lerp(healFrom, healTo, Motion.Power3InOut(healClock / HEAL_SWEEP));
            if (healClock >= HEAL_SWEEP)
            {
                health = healTo;
                healing = false;
            }
        }

        ghostClock += dt;
        if (ghostClock > GHOST_HOLD)
            ghost = Mathf.Lerp(ghostFrom, health, Motion.Power3InOut((ghostClock - GHOST_HOLD) / GHOST_COLLAPSE));
        ghost = Mathf.Max(ghost, health);
        lead = Mathf.Max(lead, health);
    }

    // The cut lands on the frame of the hit; every hit in a string restarts the hold, so the slab collapses once
    // the string ends and shows the whole chunk it took.
    private void LandDamage(float lost, float target)
    {
        healing = false;
        ghostFrom = ghost = Mathf.Max(ghost, health);
        ghostClock = 0f;
        health = lead = target;

        float weight = Mathf.Clamp(lost * 4f, 0f, 1f);
        Flash(Colors.White, FLASH_TIME);
        Punch(0.18f + 0.32f * weight);
        Shake(1f + 3f * weight);
        if (lost >= 0.12f)
            Chips(new Vector2(WIDTH * (target + lost * 0.5f), 2f), Mathf.RoundToInt(4 + 8 * weight), GHOST_COLOR);
    }

    private void BeginHeal(float target)
    {
        bool discrete = target - lead > DISCRETE_HEAL;
        if (!healing || discrete)
        {
            healFrom = health;
            healClock = discrete ? -HEAL_HOLD : 0f;
            healing = true;
        }
        lead = healTo = target;
        if (!discrete)
            return;
        Flash(HEAL_COLOR, FLASH_TIME);
        Punch(0.14f);
    }

    private void TrackTenacity(float dt)
    {
        tenacityFlashTime -= dt;
        staggerClock += dt;
        bool staggered = owner.Impact.IsStaggered;
        if (staggered && !wasStaggered)
            BeginStagger();
        else if (!staggered && wasStaggered)
            EndStagger();
        wasStaggered = staggered;

        float tenacity = owner.Impact.TenacityRatio;
        if (!staggered && tenacity > tenacitySeen + 0.001f)
        {
            tenacityFlashFrom = tenacityFlashTime > 0f ? Mathf.Min(tenacityFlashFrom, tenacitySeen) : tenacitySeen;
            tenacityFlashTime = TENACITY_FLASH;
        }
        tenacitySeen = tenacity;
    }

    private void BeginStagger()
    {
        staggerClock = 0f;
        tenacityFlashTime = 0f;
        Flash(Colors.White, FLASH_TIME * 1.8f);
        Punch(0.5f);
        Shake(3f);
        Chips(TENACITY_ORIGIN + new Vector2(WIDTH * 0.5f, 2f), 10, STAGGER_COLOR);
    }

    private void EndStagger()
    {
        Flash(STAGGER_COLOR, FLASH_TIME);
        Punch(-0.12f);
    }

    private void Flash(Color color, float duration)
    {
        flashColor = color;
        flashTime = Mathf.Max(flashTime, duration);
    }

    private void Punch(float strength)
    {
        punchStrength = punchClock < PUNCH_TIME ? Mathf.Max(strength, punchStrength * (1f - punchClock / PUNCH_TIME)) : strength;
        punchClock = 0f;
    }

    private void Shake(float amplitude)
    {
        shakeAmplitude = shakeClock < SHAKE_STEP * SHAKE_STEPS.Length ? Mathf.Max(amplitude, shakeAmplitude) : amplitude;
        shakeClock = 0f;
    }

    private void Chips(Vector2 localPosition, int amount, Color color)
    {
        Vector2 direction = new Vector2(hitSign * 0.6f, -1f).Normalized();
        Vfx.Burst(owner.World.Effects, ToGlobal(localPosition), amount, color, 110f, direction, 50f, 0.45f, 380f, 1.5f, 20f);
    }

    private void Shatter()
    {
        breakClock = -1f;
        broken = true;
        columns = BarTextures.CutColumns();
        BarFrame frame = Frame();
        Transform2D transform = GetGlobalTransform();
        float spread = reassemble ? 0.6f : 1f;
        for (int i = 0; i < columns.Length - 1; i++)
        {
            var shard = new EnemyBarShard();
            shard.Setup(frame, columns[i], columns[i + 1], transform, breakDirection, spread);
            owner.World.Effects.AddChild(shard);
        }
        Vfx.Burst(owner.World.Effects, ToGlobal(PIVOT), 8, Colors.White, 90f, breakDirection, 70f, 0.3f, 200f, 1.5f, 40f, true);

        assembleClock = 0f;
        columnDelays = new float[columns.Length - 1];
        int[] order = new int[columnDelays.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int i = 0; i < order.Length; i++)
            columnDelays[order[i]] = ASSEMBLE_DELAY + i * ASSEMBLE_STEP + GD.Randf() * ASSEMBLE_STEP * 0.8f;
    }

    // The assembly: columns fall in from above in shuffled order and snap opaque, rather than fading up.
    private void UpdateAssembly(float dt)
    {
        if (!reassemble || owner.IsDead)
        {
            Visible = false;
            return;
        }
        assembleClock += dt;
        Visible = assembleClock >= ASSEMBLE_DELAY;
        health = ghost = lead = owner.HealthRatio;
        ghostClock = 99f;
        tenacitySeen = owner.Impact.TenacityRatio;
        wasStaggered = owner.Impact.IsStaggered;
        float last = 0f;
        foreach (float delay in columnDelays)
            last = Mathf.Max(last, delay);
        if (assembleClock < last + ASSEMBLE_TIME)
            return;
        broken = false;
        shown = true;
        presenceScale = 1f;
        Flash(Colors.White, FLASH_TIME);
        Punch(0.2f);
    }

    private BarFrame Frame()
    {
        bool staggered = owner.Impact.IsStaggered;
        float staggerFill = 0f;
        if (staggered)
            staggerFill = owner.Impact.StaggerTimeRemaining / owner.Impact.StaggerDuration * Motion.Power3InOut(staggerClock / STAGGER_SWEEP);
        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.GetTicksMsec() / 1000f * 14f);
        return new BarFrame(
            health,
            ghost,
            ghostClock < GHOST_HOLD ? Colors.White : GHOST_COLOR,
            lead,
            HEAL_COLOR,
            tenacitySeen,
            tenacityFlashTime > 0f ? tenacityFlashFrom : tenacitySeen,
            staggered,
            staggerFill,
            new Color(pulse, pulse, pulse),
            staggered && staggerClock < STAGGER_SWEEP);
    }

    public override void _Draw()
    {
        if (broken && !reassemble)
            return;
        float shake = 0f;
        int step = (int)(shakeClock / SHAKE_STEP);
        if (step < SHAKE_STEPS.Length)
            shake = Mathf.Round(SHAKE_STEPS[step] * shakeAmplitude * hitSign);
        float punch = punchClock < PUNCH_TIME ? punchStrength * (1f - Motion.BackOut(punchClock / PUNCH_TIME)) : 0f;
        Vector2 scale = new Vector2(1f + punch * 0.45f, 1f + punch) * presenceScale;
        DrawSetTransformMatrix(new Transform2D(0f, scale, 0f, PIVOT + new Vector2(shake, 0f)) * Transform2D.Identity.Translated(-PIVOT));

        BarFrame frame = Frame();
        if (broken)
        {
            for (int i = 0; i < columnDelays.Length; i++)
            {
                float t = (assembleClock - columnDelays[i]) / ASSEMBLE_TIME;
                if (t <= 0f)
                    continue;
                Vector2 drop = new(0f, -Mathf.Round((1f - Motion.Power4Out(t)) * ASSEMBLE_DROP));
                BarTextures.Shared.Draw(this, frame, columns[i], columns[i + 1], drop);
            }
            return;
        }

        BarTextures.Shared.Draw(this, frame, 0f, WIDTH, Vector2.Zero);
        int arches = Mathf.Clamp(owner.EnemyLevel - 1, 0, 5);
        Color archColor = owner.EnemyLevel >= 6 ? new Color(0.9f, 0.2f, 1f) : new Color(1f, 0.82f, 0.4f);
        for (int i = 0; i < arches; i++)
        {
            Vector2 center = new(WIDTH * 0.5f + (i - (arches - 1) * 0.5f) * 5f, -1.5f);
            DrawArc(center, 2f, Mathf.Pi, Mathf.Tau, 6, archColor, 1f);
        }
    }
}

public readonly record struct BarFrame(
    float Health,
    float Ghost,
    Color GhostColor,
    float Lead,
    Color LeadColor,
    float Tenacity,
    float TenacityFlashFrom,
    bool Staggered,
    float StaggerFill,
    Color StaggerPulse,
    bool StaggerSweeping);

// The bar's layers, drawable whole or as one vertical column so the shards and the assembly reuse the exact art.
// White silhouettes of the fills let a slab arrive at full chroma instead of being tinted down from the art.
public sealed class BarTextures
{
    private const string ROOT = "UI/Resources UI/Enemy/";
    private static BarTextures shared;

    private readonly Texture2D healthUnder;
    private readonly Texture2D healthProgress;
    private readonly Texture2D healthOver;
    private readonly Texture2D healthSlab;
    private readonly Texture2D tenacityUnder;
    private readonly Texture2D tenacityFilling;
    private readonly Texture2D tenacityStagger;
    private readonly Texture2D tenacityOver;
    private readonly Texture2D tenacitySlab;

    public static BarTextures Shared => shared ??= new BarTextures(ROOT);

    private BarTextures(string root)
    {
        healthUnder = Art.Texture(root + "Health Bar/Health_Bar_Under.png");
        healthProgress = Art.Texture(root + "Health Bar/Health_Bar_Progress.png");
        healthOver = Art.Texture(root + "Health Bar/Health_Bar_Over.png");
        tenacityUnder = Art.Texture(root + "Tenacity Bar/Tenacity_Bar_Under.png");
        tenacityFilling = Art.Texture(root + "Tenacity Bar/Tenacity_Bar_Active.png");
        tenacityStagger = Art.Texture(root + "Tenacity Bar/Tenacity_Bar_Progress.png");
        tenacityOver = Art.Texture(root + "Tenacity Bar/Tenacity_Bar_Over.png");
        healthSlab = Silhouette(healthProgress);
        tenacitySlab = Silhouette(tenacityFilling);
    }

    public static float[] CutColumns()
    {
        var cuts = new System.Collections.Generic.List<float> { 0f };
        float x = 0f;
        while (x < EnemyResourceBar.WIDTH)
        {
            x = Mathf.Min(EnemyResourceBar.WIDTH, x + GD.RandRange(3, 6));
            if (EnemyResourceBar.WIDTH - x < 2f)
                x = EnemyResourceBar.WIDTH;
            cuts.Add(x);
        }
        return cuts.ToArray();
    }

    public void Draw(CanvasItem canvas, BarFrame frame, float x0, float x1, Vector2 offset)
    {
        Draw(canvas, healthUnder, offset, 0f, 1f, x0, x1, Colors.White);
        if (frame.Ghost > frame.Health)
            Draw(canvas, healthSlab, offset, frame.Health, frame.Ghost, x0, x1, frame.GhostColor);
        if (frame.Lead > frame.Health)
            Draw(canvas, healthSlab, offset, frame.Health, frame.Lead, x0, x1, frame.LeadColor);
        Draw(canvas, healthProgress, offset, 0f, frame.Health, x0, x1, Colors.White);
        Draw(canvas, healthOver, offset, 0f, 1f, x0, x1, Colors.White);

        Vector2 tenacity = offset + EnemyResourceBar.TENACITY_ORIGIN;
        Draw(canvas, tenacityUnder, tenacity, 0f, 1f, x0, x1, Colors.White);
        if (frame.Staggered)
        {
            Draw(canvas, tenacityStagger, tenacity, 0f, frame.StaggerFill, x0, x1, frame.StaggerPulse);
            if (frame.StaggerSweeping)
                Draw(canvas, tenacitySlab, tenacity, frame.StaggerFill - 1f / EnemyResourceBar.WIDTH, frame.StaggerFill, x0, x1, Colors.White);
        }
        else
        {
            Draw(canvas, tenacityFilling, tenacity, 0f, frame.TenacityFlashFrom, x0, x1, Colors.White);
            if (frame.Tenacity > frame.TenacityFlashFrom)
                Draw(canvas, tenacitySlab, tenacity, frame.TenacityFlashFrom, frame.Tenacity, x0, x1, Colors.White);
        }
        Draw(canvas, tenacityOver, tenacity, 0f, 1f, x0, x1, Colors.White);
    }

    private static void Draw(CanvasItem canvas, Texture2D texture, Vector2 origin, float from, float to, float x0, float x1, Color modulate)
    {
        Vector2 size = texture.GetSize();
        // A sliver of health still reads as one pixel rather than an empty bar.
        float end = to > 0f ? Mathf.Max(1f, Mathf.Round(size.X * Mathf.Min(to, 1f))) : 0f;
        float left = Mathf.Max(Mathf.Round(size.X * Mathf.Clamp(from, 0f, 1f)), x0);
        float right = Mathf.Min(end, x1);
        if (right <= left)
            return;
        var source = new Rect2(left, 0f, right - left, size.Y);
        canvas.DrawTextureRectRegion(texture, new Rect2(origin + new Vector2(left, 0f), source.Size), source, modulate);
    }

    private static Texture2D Silhouette(Texture2D texture)
    {
        Image image = texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
            image.SetPixel(x, y, new Color(1f, 1f, 1f, image.GetPixel(x, y).A));
        return ImageTexture.CreateFromImage(image);
    }
}
