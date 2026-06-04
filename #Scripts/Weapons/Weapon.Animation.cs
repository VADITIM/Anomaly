public partial class Weapon
{
    public float GetNativeAnimationLength(string direction, bool isHeavy)
    {
        return WeaponAnimations.GetNativeAnimationLength(weaponAnimationPlayer, direction, isHeavy, attackSequenceIndex);
    }

    private float GetStateAnimationDuration(string animationName)
    {
        if (!WeaponAnimations.IsAttackAnimation(animationName))
            return 0f;

        return GetCurrentAttackSequenceDuration(WeaponAnimations.IsHeavyAttack(animationName));
    }


    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        string resolvedAnim = WeaponAnimations.GetAttackAnimationName(
            weaponAnimationPlayer, direction, isHeavy, attackSequenceIndex);


        float duration = GetCurrentAttackSequenceDuration(isHeavy);
        WeaponAnimations.PlayAttackAnimation(weaponAnimationPlayer, resolvedAnim, duration);

        currentArc?.PrepareAttack(direction, isHeavy, attackSequenceIndex);
    }

    public void OnAttackAnimationFinished()
    {
        bool isLastComboStep = attackSequenceIndex >= MaxComboSteps - 1;

        if (isLastComboStep)
        {
            comboCooldownTimer = ComboFinisherCooldown;
            ResetAttackSequence();
        }
        else
        {
            comboWindowTimer = ComboFollowUpWindow;
        }
    }

    public void PlayStateAnimation(string animationName)
    {
        if (weaponAnimationPlayer == null)
            return;

        float desiredDuration = GetStateAnimationDuration(animationName);
        WeaponAnimations.PlayStateAnimation(weaponAnimationPlayer, animationName, desiredDuration);
    }


}