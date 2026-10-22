using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Mirrors the former project's rig: the body and the weapon each own an AnimationPlayer that plays the same
// clip name at the same speed. Both sheets share one frame layout, so they cannot drift out of alignment.
public partial class PlayerAnimator : Node
{
    public const string BODY_LIBRARY_PATH = "res://#Resources/Animations/PlayerBody.tres";
    public const string WEAPON_LIBRARY_PATH = "res://#Resources/Animations/PlayerWeapon.tres";
    public const string ARC_EFFECT_LIBRARY_PATH = "res://#Resources/Animations/SoulArcEffect.tres";
    public static readonly bool WEAPON_LAYER_ENABLED = false;
    public static readonly bool ARC_EFFECT_ENABLED = false;
    public const float ATTACK_CLIP_LENGTH = 0.525f;
    public const float ATTACK_STRIKE_FRACTION = 0.15f / ATTACK_CLIP_LENGTH;
    public const float ATTACK_RECOVER_FRACTION = 0.35f / ATTACK_CLIP_LENGTH;
    private const int SHEET_COLUMNS = 14;
    private const int SHEET_ROWS = 13;
    private const float FEET_OFFSET = 15f;
    private static readonly string[] DIRECTIONS = { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };
    private static readonly string[] CARDINALS = { "E", "S", "W", "N" };

    public Sprite2D BodySprite { get; private set; }
    public Sprite2D WeaponSprite { get; private set; }
    public Node2D WeaponRoot { get; private set; }
    public AnimationPlayer BodyPlayer { get; private set; }
    public AnimationPlayer WeaponPlayer { get; private set; }
    public string CurrentClip { get; private set; } = "";

    private AnimationLibrary arcEffectLibrary;

    public void Build(Node2D body, Material bodyMaterial)
    {
        Name = "Animator";
        BodySprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = Art.Texture("Entities/Player/Kio_Body.png"),
            Hframes = SHEET_COLUMNS,
            Vframes = SHEET_ROWS,
            Offset = new Vector2(0, -FEET_OFFSET),
            Material = bodyMaterial,
        };
        body.AddChild(BodySprite);
        BodyPlayer = CreatePlayer("Body Animation Player", BODY_LIBRARY_PATH);
        body.AddChild(BodyPlayer);

        WeaponRoot = new Node2D { Name = "WEAPON", ZIndex = 1, ZAsRelative = true };
        body.AddChild(WeaponRoot);
        WeaponSprite = new Sprite2D
        {
            Name = "Weapon",
            Texture = Art.Texture("Entities/Player/Kio_Weapon.png"),
            Hframes = SHEET_COLUMNS,
            Vframes = SHEET_ROWS,
            Offset = new Vector2(0, -FEET_OFFSET),
        };
        WeaponRoot.AddChild(WeaponSprite);
        WeaponPlayer = CreatePlayer("Weapon Animation Player", WEAPON_LIBRARY_PATH);
        WeaponRoot.AddChild(WeaponPlayer);
        // The held-weapon layer is parked while the Scythe is telekinetic; the rig stays intact for later art.
        WeaponRoot.Visible = WEAPON_LAYER_ENABLED;
        WeaponPlayer.Active = WEAPON_LAYER_ENABLED;

        arcEffectLibrary = GD.Load<AnimationLibrary>(ARC_EFFECT_LIBRARY_PATH);
    }

    private static AnimationPlayer CreatePlayer(string name, string libraryPath)
    {
        var player = new AnimationPlayer { Name = name };
        player.AddAnimationLibrary("", GD.Load<AnimationLibrary>(libraryPath));
        return player;
    }

    public static string Direction8(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
            return "S";
        return DIRECTIONS[Mathf.PosMod(Mathf.RoundToInt(direction.Angle() / (Mathf.Pi / 4f)), 8)];
    }

    public static string Direction4(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
            return "S";
        return CARDINALS[Mathf.PosMod(Mathf.RoundToInt(direction.Angle() / (Mathf.Pi / 2f)), 4)];
    }

    public static Vector2 Snap8(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
            return Vector2.Down;
        float step = Mathf.Pi / 4f;
        return Vector2.FromAngle(Mathf.Round(direction.Angle() / step) * step);
    }

    // One-shot clips restart from frame zero and are time-stretched to the gameplay duration,
    // so the strike frame always lands on the hit window no matter how the attack is tuned.
    public void PlayOneShot(string clip, float duration)
    {
        float speed = ClipLength(clip) / Mathf.Max(duration, 0.0001f);
        StringName name = StringNames.Of(clip);
        foreach (AnimationPlayer player in Players())
        {
            if (!player.HasAnimation(name))
                continue;
            player.SpeedScale = speed;
            player.Play(name);
            player.Seek(0, true);
        }
        CurrentClip = clip;
    }

    // Looping clips keep their playhead when only the speed changes, which avoids foot-sliding pops.
    public void PlayLoop(string clip, float speedScale = 1f)
    {
        StringName name = StringNames.Of(clip);
        foreach (AnimationPlayer player in Players())
        {
            if (!player.HasAnimation(name))
                continue;
            player.SpeedScale = speedScale;
            if (player.CurrentAnimation != name || !player.IsPlaying())
            {
                double position = CurrentClip.Length > 0 && SameFamily(CurrentClip, clip) ? player.CurrentAnimationPosition : 0;
                player.Play(name);
                player.Seek(position % player.GetAnimation(name).Length, true);
            }
        }
        CurrentClip = clip;
    }

    public void Hold(string clip, float time)
    {
        StringName name = StringNames.Of(clip);
        foreach (AnimationPlayer player in Players())
        {
            if (!player.HasAnimation(name))
                continue;
            if (player.CurrentAnimation != name)
                player.Play(name);
            player.Seek(time, true);
            player.Pause();
        }
        CurrentClip = clip;
    }

    public float ClipLength(string clip)
    {
        StringName name = StringNames.Of(clip);
        return BodyPlayer.HasAnimation(name) ? (float)BodyPlayer.GetAnimation(name).Length : ATTACK_CLIP_LENGTH;
    }

    // "Move" + "SE" -> "Move_SE", built once per pair since locomotion asks for it every frame.
    private static readonly Dictionary<(string, string), string> clipNames = new();

    public static string ClipName(string family, string direction)
    {
        if (!clipNames.TryGetValue((family, direction), out string name))
            clipNames[(family, direction)] = name = family + "_" + direction;
        return name;
    }

    public void SetAlpha(float alpha)
    {
        BodySprite.Modulate = new Color(1f, 1f, 1f, alpha);
        WeaponRoot.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    public void SetVisible(bool visible)
    {
        BodySprite.Visible = visible;
        WeaponSprite.Visible = visible;
    }

    // The Arc's manifestation burst is its own small scene with its own player, like the old Soul Scythe Arc node.
    public void SpawnArcEffect(Node parent, Vector2 position, bool flip, Color tint, float duration)
    {
        if (!ARC_EFFECT_ENABLED || arcEffectLibrary == null)
            return;
        var root = new Node2D { Position = position, ZIndex = 5 };
        var sprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = Art.Texture("Weapons/Arcs/SoulScytheArcEffect.png"),
            Hframes = 4,
            Vframes = 4,
            Scale = Vector2.One * 0.26f,
            FlipH = flip,
            Modulate = tint,
        };
        root.AddChild(sprite);
        var player = new AnimationPlayer { Name = "Arc Animation Player" };
        player.AddAnimationLibrary("", arcEffectLibrary);
        root.AddChild(player);
        parent.AddChild(root);

        float length = (float)arcEffectLibrary.GetAnimation("Effect").Length;
        player.SpeedScale = length / Mathf.Max(duration, 0.05f);
        player.Play("Effect");
        player.AnimationFinished += _ => root.QueueFree();
    }

    private static bool SameFamily(string a, string b) => a.Split('_')[0] == b.Split('_')[0];

    private AnimationPlayer[] players;

    private AnimationPlayer[] Players() => players ??= WEAPON_LAYER_ENABLED ? new[] { BodyPlayer, WeaponPlayer } : new[] { BodyPlayer };
}
