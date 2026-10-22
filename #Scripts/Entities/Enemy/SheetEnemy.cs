using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Enemies drawn from side-view packs: one horizontal strip per clip, art facing right and mirrored for the left.
// The base states ask for "Walk" while moving or standing and "Death" on dying; those resolve to the move/idle,
// hurt and death clips here, so a type only names its attack clips itself.
public abstract partial class SheetEnemy : Enemy
{
    protected readonly record struct Clip(string File, int Frames, float Fps, bool Reverse = false, Texture2D Texture = null);

    private readonly Dictionary<string, Clip> clips = new(StringComparer.OrdinalIgnoreCase);
    private float facingSign = 1f;

    protected override bool UsesAnimationSet => false;
    protected virtual Vector2I FrameSize => new(64, 64);
    // Distance from the frame's centre down to the feet, in source pixels.
    protected virtual float FeetInFrame => 16f;
    protected virtual float SpriteScale => 1f;
    protected virtual bool ArtFacesLeft => false;
    protected bool IsMoving => MoveVelocity.LengthSquared() > 64f;

    protected sealed override void ConfigureEnemy()
    {
        Sprite.Hframes = 1;
        Sprite.RegionEnabled = true;
        Sprite.Scale = Vector2.One * SpriteScale;
        Sprite.Offset = new Vector2(0, -FeetInFrame);
        ConfigureSheet();
        facingSign = GD.Randf() < 0.5f ? 1f : -1f;
    }

    protected abstract void ConfigureSheet();

    // The strip is fetched once here, so playing a clip every frame builds no path.
    protected void Define(string name, string file, int frames, float fps, bool reverse = false) =>
        clips[name] = new Clip(file, frames, fps, reverse, Art.Texture($"Entities/Enemies/{SpriteFolder}/{file}.png"));

    protected bool HasClip(string name) => clips.ContainsKey(name);

    protected void Tint(Color color, float strength)
    {
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_strength"), strength);
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_color"), color);
    }

    protected virtual string ResolveClip(string name)
    {
        if (name != "Walk")
            return name;
        if (StateMachine != null && StateMachine.Current is EnemyState.Flinch or EnemyState.Staggered && HasClip("hurt"))
            return "hurt";
        return IsMoving && HasClip("move") ? "move" : "idle";
    }

    protected override void ApplyAnimation(string name, float time, float fps, bool loops)
    {
        string key = ResolveClip(name);
        if (!clips.TryGetValue(key, out Clip clip) && !clips.TryGetValue("idle", out clip))
            return;
        // The hurt clip always plays at its own rate: a flinch asks for a still "Walk", which would freeze it mid-knockback.
        float rate = name == "Death" ? clip.Frames / DEATH_DURATION : name == "Walk" ? (fps <= 0f && key != "hurt" ? 0f : clip.Fps) : fps;
        bool loop = name == "Walk" || loops;
        int frame = (int)(time * rate);
        frame = loop ? frame % clip.Frames : Mathf.Min(frame, clip.Frames - 1);
        if (clip.Reverse)
            frame = clip.Frames - 1 - frame;

        if (Mathf.Abs(FacingDirection.X) > 0.15f)
            facingSign = Mathf.Sign(FacingDirection.X);
        if (Sprite.Texture != clip.Texture)
            Sprite.Texture = clip.Texture;
        Sprite.RegionRect = new Rect2(frame * FrameSize.X, 0, FrameSize.X, FrameSize.Y);
        Sprite.FlipH = (facingSign < 0f) != ArtFacesLeft;
    }
}
