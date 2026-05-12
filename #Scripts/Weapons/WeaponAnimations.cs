using Godot;
using System;

public static class WeaponAnimations
{
    public static void PlayAttackAnimation(AnimationPlayer animationPlayer, string animationName, float attackDuration, float heavyAttackDuration)
    {
        bool isAttackAnimation = IsAttackAnimation(animationName);
        
        if (isAttackAnimation)
        {
            bool isHeavy = animationName == "Weapon_Spin" || animationName.Contains("Spin");
            
            float desiredDuration = isHeavy ? heavyAttackDuration : attackDuration;
            float nativeLength = GetAnimationDuration(animationPlayer, animationName);
            float speedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
            
            animationPlayer.SpeedScale = speedScale;
        }
        else
        {
            animationPlayer.SpeedScale = 1f;
        }
        
        if (animationPlayer.CurrentAnimation != animationName || !animationPlayer.IsPlaying())
        {
            GD.Print($"[WeaponAnimations.PlayAttackAnimation] Playing: {animationName}");
            animationPlayer.Play(animationName);
        }
    }

    public static float GetDesiredAttackDuration(float attackDuration, float heavyAttackDuration, bool isHeavy)
    {
        return isHeavy ? heavyAttackDuration : attackDuration;
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

    public static float GetNativeAnimationLength(AnimationPlayer animationPlayer, string direction, bool isHeavy)
    {
        if (animationPlayer == null)
            return 1f;
        
        string[] candidates = GetAttackAnimationCandidates(direction, isHeavy);
        foreach (string animationName in candidates)
        {
            if (animationPlayer.HasAnimation(animationName))
                return GetAnimationDuration(animationPlayer, animationName);
        }
        
        return 1f;
    }

    public static void PlayStateAnimation(AnimationPlayer animationPlayer, string animationName, float attackDuration, float heavyAttackDuration)
    {
        if (animationPlayer == null)
            return;

        if (animationPlayer.HasAnimation(animationName))
        {
            PlayAttackAnimation(animationPlayer, animationName, attackDuration, heavyAttackDuration);
            return;
        }

        string alt = animationName;
        if (animationName.StartsWith("Weapon_"))
            alt = animationName.Substring("Weapon_".Length);

        if (!string.IsNullOrEmpty(alt) && animationPlayer.HasAnimation(alt))
        {
            PlayAttackAnimation(animationPlayer, alt, attackDuration, heavyAttackDuration);
            return;
        }

        if (animationPlayer.HasAnimation("Weapon_Idle_Down"))
        {
            animationPlayer.SpeedScale = 1f;
            GD.Print($"[WeaponAnimations.PlayStateAnimation] Fallback to idle");
            animationPlayer.Play("Weapon_Idle_Down");
        }
    }

    public static bool IsAttackAnimation(string animationName)
    {
        return animationName.StartsWith("Weapon_Attack") ||
               animationName.StartsWith("Attack") ||
               animationName == "Weapon_Spin";
    }

    public static string ExtractDirection(string animationName)
    {
        if (animationName.Contains("Up")) return "Up";
        if (animationName.Contains("Down")) return "Down";
        if (animationName.Contains("Left")) return "Left";
        if (animationName.Contains("Right")) return "Right";
        return "Down";
    }

    public static bool IsHeavyAttack(string animationName)
    {
        return animationName == "Weapon_Spin" || animationName.Contains("Spin");
    }

    public static string[] GetAttackAnimationCandidates(string direction, bool isHeavy)
    {
        if (isHeavy)
        {
            return new[] { "Weapon_Spin", "Weapon_Attack_Spin", $"Weapon_Attack_{direction}", "Weapon_Attack_Down", "Weapn_Attack_Up" };
        }

        return new[] { $"Weapon_Attack_{direction}", "Weapon_Attack_Down", "Weapon_Attack_Left", "Weapon_Attack_Right", "Weapon_Attack_Up", "Weapn_Attack_Up" };
    }

    public static string GetAttackAnimationName(AnimationPlayer animationPlayer, string direction, bool isHeavy)
    {
        string[] candidates = GetAttackAnimationCandidates(direction, isHeavy);
        foreach (string animationName in candidates)
        {
            if (animationPlayer != null && animationPlayer.HasAnimation(animationName))
                return animationName;
        }
        return null;
    }
}
