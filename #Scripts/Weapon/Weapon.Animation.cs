public partial class Weapon
{
    public float GetNativeAnimationLength(string direction, bool isHeavy)
    {
        return WeaponAnimations.GetNativeAnimationLength(_animationPlayer, direction, isHeavy, _attackSequenceIndex);
    }

    private float GetStateAnimationDuration(string animationName)
    {
        if (!WeaponAnimations.IsAttackAnimation(animationName))
            return 0f;

        return GetAttackAnimationDuration(null, WeaponAnimations.IsHeavyAttack(animationName));
    }


    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        string resolvedAnim = WeaponAnimations.GetAttackAnimationName(
            _animationPlayer, direction, isHeavy, _attackSequenceIndex);


        float duration = GetAttackAnimationDuration(direction, isHeavy);
        WeaponAnimations.PlayAttackAnimation(_animationPlayer, resolvedAnim, duration);

        _currentArc?.PrepareAttack(direction, isHeavy, _attackSequenceIndex);
    }

    public void OnAttackAnimationFinished()
    {
        bool isLastComboStep = _attackSequenceIndex >= MaxComboSteps - 1;

        if (isLastComboStep)
        {
            _comboCooldownTimer = ComboFinisherCooldown;
            ResetAttackSequence();
        }
        else
        {
            _comboWindowTimer = ComboFollowUpWindow;
        }
    }

    public void PlayStateAnimation(string animationName)
    {
        if (_animationPlayer == null)
            return;

        float desiredDuration = GetStateAnimationDuration(animationName);
        WeaponAnimations.PlayStateAnimation(_animationPlayer, animationName, desiredDuration);
    }
}
