using Godot;
using System;

public static class WeaponAnimations
{
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

        string alt = animationName;
        if (animationName.StartsWith("Weapon_"))
            alt = animationName.Substring("Weapon_".Length);

        if (!string.IsNullOrEmpty(alt) && animationPlayer.HasAnimation(alt))
        {
            PlayAttackAnimation(animationPlayer, alt, desiredDuration);
            return;
        }

        if (animationPlayer.HasAnimation("Idle_Down") || animationPlayer.HasAnimation("Weapon_Idle_Down"))
        {
            animationPlayer.SpeedScale = 1f;
            animationPlayer.Play(animationPlayer.HasAnimation("Idle_Down") ? "Idle_Down" : "Weapon_Idle_Down");
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
             animationName == "Weapon_Spin" ||
             animationName == "Attack_Spin";
    }

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

    public static string NormaliseDirection(string direction)
    {
        return direction switch
        {
            "Up"    => "Top",
            "Down"  => "Bottom",
            _       => direction   
        };
    }

    public static bool IsHeavyAttack(string animationName)
    {
        return animationName == "Weapon_Spin" || animationName == "Attack_Spin" || animationName.Contains("Spin");
    }

    public static string[] GetAttackAnimationCandidates(string direction, bool isHeavy, int sequenceIndex = 0)
    {
        if (isHeavy)
        {
            return new[]
            {
                "Attack_Spin",
                "Attack_Attack_Spin",
                "Attack_Attack_Bottom",
                "Attack_Attack_Down"    
            };
        }

        string normDir    = NormaliseDirection(direction);
        int sequenceNumber = Mathf.Clamp(sequenceIndex + 1, 1, 4);

        return new[]
        {
            $"Attack_{normDir}_{sequenceNumber}",   
            $"Attack_{normDir}",                    
            $"Attack_{direction}_{sequenceNumber}", 
            $"Attack_{direction}",                  
            "Attack_Bottom",
            "Attack_Down"
        };
    }

    public static string GetAttackAnimationName(AnimationPlayer animationPlayer, string direction, bool isHeavy, int sequenceIndex = 0)
    {
        string[] candidates = GetAttackAnimationCandidates(direction, isHeavy, sequenceIndex);
        foreach (string animationName in candidates)
        {
            if (animationPlayer != null && animationPlayer.HasAnimation(animationName))
            {
                return animationName;
            }
        }

        if (animationPlayer != null)
        {
            var list = animationPlayer.GetAnimationList();
        }
        return null;
    }
}