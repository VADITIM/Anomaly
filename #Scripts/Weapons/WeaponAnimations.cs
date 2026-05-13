using Godot;
using System;

public static class WeaponAnimations
{
    // Animation name format: Weapon_Attack_{Direction}_{Number}
    // Direction values: Top, Bottom, Left, Right   (number: 1-4)
    // Heavy / spin: Weapon_Spin

    public static void PlayAttackAnimation(AnimationPlayer animationPlayer, string animationName, float attackDuration, float heavyAttackDuration)
    {
        float desiredDuration = IsHeavyAttack(animationName) ? heavyAttackDuration : attackDuration;
        PlayAttackAnimation(animationPlayer, animationName, desiredDuration);
    }

    public static void PlayAttackAnimation(AnimationPlayer animationPlayer, string animationName, float desiredDuration)
    {
        if (animationPlayer == null || !animationPlayer.HasAnimation(animationName))
            return;

        if (IsAttackAnimation(animationName))
        {
            float nativeLength = GetAnimationDuration(animationPlayer, animationName);
            float speedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
            animationPlayer.SpeedScale = speedScale;
            GD.Print($"[Animation] PlayAttackAnimation -> {animationName} | desired={desiredDuration:F3}s | native={nativeLength:F3}s | speed={speedScale:F3}");
            animationPlayer.Play(animationName);
        }
        else
        {
            animationPlayer.SpeedScale = 1f;
            if (animationPlayer.CurrentAnimation != animationName || !animationPlayer.IsPlaying())
                animationPlayer.Play(animationName);
        }
    }

    public static float GetDesiredAttackDuration(float attackDuration, float heavyAttackDuration, bool isHeavy)
    {
        return isHeavy ? heavyAttackDuration : attackDuration;
    }

    public static void PlayStateAnimation(AnimationPlayer animationPlayer, string animationName, float desiredDuration)
    {
        if (animationPlayer == null)
            return;

        if (animationPlayer.HasAnimation(animationName))
        {
            PlayAttackAnimation(animationPlayer, animationName, desiredDuration);
            return;
        }

        // Strip leading "Weapon_" prefix and try again.
        string alt = animationName;
        if (animationName.StartsWith("Weapon_"))
            alt = animationName.Substring("Weapon_".Length);

        if (!string.IsNullOrEmpty(alt) && animationPlayer.HasAnimation(alt))
        {
            PlayAttackAnimation(animationPlayer, alt, desiredDuration);
            return;
        }

        // Fallback to idle.
        if (animationPlayer.HasAnimation("Weapon_Idle_Down"))
        {
            animationPlayer.SpeedScale = 1f;
            animationPlayer.Play("Weapon_Idle_Down");
        }
    }

    public static float GetAnimationDuration(AnimationPlayer animationPlayer, string animationName)
    {
        if (animationPlayer == null || !animationPlayer.HasAnimation(animationName))
            return 1f;

        Animation animation = animationPlayer.GetAnimation(animationName);
        if (animation == null)
            return 1f;

        return Mathf.Max(0.1f, (float)animation.Length);
    }

    /// Returns the native length of the attack animation that would play for the
    /// given direction, heavy flag, and sequence index.
    public static float GetNativeAnimationLength(AnimationPlayer animationPlayer, string direction, bool isHeavy, int sequenceIndex = 0)
    {
        if (animationPlayer == null)
            return 1f;

        string[] candidates = GetAttackAnimationCandidates(direction, isHeavy, sequenceIndex);
        foreach (string animationName in candidates)
        {
            if (animationPlayer.HasAnimation(animationName))
                return GetAnimationDuration(animationPlayer, animationName);
        }

        return 1f;
    }

    public static bool IsAttackAnimation(string animationName)
    {
        return animationName.StartsWith("Weapon_Attack") ||
               animationName.StartsWith("Attack") ||
               animationName == "Weapon_Spin";
    }

    /// Extracts a normalised direction token from an animation name.
    /// Returns one of: Top, Bottom, Left, Right.
    public static string ExtractDirection(string animationName)
    {
        if (animationName.Contains("Top"))    return "Top";
        if (animationName.Contains("Bottom")) return "Bottom";
        if (animationName.Contains("Left"))   return "Left";
        if (animationName.Contains("Right"))  return "Right";
        // Legacy aliases kept for backwards-compat.
        if (animationName.Contains("Up"))     return "Top";
        if (animationName.Contains("Down"))   return "Bottom";
        return "Bottom";
    }

    /// Normalises an incoming direction string to the canonical form used in
    /// animation names (e.g. "Down" → "Bottom", "Up" → "Top").
    public static string NormaliseDirection(string direction)
    {
        return direction switch
        {
            "Up"    => "Top",
            "Down"  => "Bottom",
            _       => direction   // Left, Right, Top, Bottom pass through unchanged
        };
    }

    public static bool IsHeavyAttack(string animationName)
    {
        return animationName == "Weapon_Spin" || animationName.Contains("Spin");
    }

    /// Returns an ordered list of candidate animation names to try, most
    /// specific first. Format: Weapon_Attack_{Direction}_{Number}
    /// sequenceIndex is 0-based; the animation number is index + 1 (1-4).
    public static string[] GetAttackAnimationCandidates(string direction, bool isHeavy, int sequenceIndex = 0)
    {
        if (isHeavy)
        {
            return new[]
            {
                "Weapon_Spin",
                "Weapon_Attack_Spin",
                "Weapon_Attack_Bottom",
                "Weapon_Attack_Down"    // legacy fallback
            };
        }

        string normDir    = NormaliseDirection(direction);
        int sequenceNumber = Mathf.Clamp(sequenceIndex + 1, 1, 4);

        // Primary candidates use the new naming convention.
        // Fallbacks cover common legacy names so nothing silently breaks.
        return new[]
        {
            $"Weapon_Attack_{normDir}_{sequenceNumber}",   // ← canonical, e.g. Weapon_Attack_Top_2
            $"Weapon_Attack_{normDir}",                    // direction without number
            $"Weapon_Attack_{direction}_{sequenceNumber}", // original direction string (Up/Down/…)
            $"Weapon_Attack_{direction}",                  // original direction without number
            "Weapon_Attack_Bottom",
            "Weapon_Attack_Down"
        };
    }

    /// Returns the name of the first matching animation in the player, or null.
    /// Logs every candidate tried so missing animations are immediately visible.
    public static string GetAttackAnimationName(AnimationPlayer animationPlayer, string direction, bool isHeavy, int sequenceIndex = 0)
    {
        string[] candidates = GetAttackAnimationCandidates(direction, isHeavy, sequenceIndex);
        foreach (string animationName in candidates)
        {
            if (animationPlayer != null && animationPlayer.HasAnimation(animationName))
            {
                GD.Print($"[WeaponAnimations] Resolved animation: '{animationName}' (sequenceIndex={sequenceIndex})");
                return animationName;
            }
        }

        // Nothing matched — dump what we tried and what IS available so it's easy to fix.
        GD.PrintErr($"[WeaponAnimations] No animation found! Tried: [{string.Join(", ", candidates)}]");
        if (animationPlayer != null)
        {
            var list = animationPlayer.GetAnimationList();
            GD.PrintErr($"[WeaponAnimations] Available animations: [{string.Join(", ", list)}]");
        }
        return null;
    }
}