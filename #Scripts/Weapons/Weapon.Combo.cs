using Godot;

public partial class Weapon
{
    public void StartAttackSequence(bool isHeavy)
    {
        if (isHeavy)
        {
            ResetAttackSequence();
            return;
        }

        queuedAttackFollowUp = false;
        comboWindowTimer = 0f;
    }

    public void QueueAttackFollowUp()
    {
        if (comboWindowTimer > 0f)
        {
            queuedAttackFollowUp = true;
        }
    }

    public bool TryConsumeQueuedAttack(bool isHeavy, out float duration)
    {
        duration = 0f;

        if (!queuedAttackFollowUp)
            return false;

        queuedAttackFollowUp = false;
        comboWindowTimer = 0f; 

        if (isHeavy)
        {
            duration = currentArc?.HeavyAttackDuration ?? 1.5f;
            return true;
        }

        attackSequenceIndex = Mathf.Min(attackSequenceIndex + 1, MaxComboSteps - 1);

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDurations.Length - 1);
        duration = currentArc != null
            ? currentArc.GetAttackSequenceDuration(clampedIndex)
            : attackDurations[clampedIndex];
        return true;
    }

    public void ResetAttackSequence()
    {
        attackSequenceIndex = 0;
        comboWindowTimer = 0f;
        queuedAttackFollowUp = false;
    }

    private void UpdateComboTimers(float delta)
    {
        if (comboCooldownTimer > 0f)
        {
            comboCooldownTimer = Mathf.Max(comboCooldownTimer - delta, 0f);
            if (comboCooldownTimer <= 0f)
                    return;
        }

        if (comboWindowTimer > 0f)
        {
            comboWindowTimer = Mathf.Max(comboWindowTimer - delta, 0f);
            if (comboWindowTimer <= 0f)
            {
                ResetAttackSequence();
            }
        }
    }

    private float GetCurrentAttackSequenceDuration(bool isHeavy)
    {
        if (currentArc != null)
        {
            if (isHeavy)
                return currentArc.HeavyAttackDuration;

            return currentArc.GetAttackSequenceDuration(attackSequenceIndex);
        }

        if (isHeavy)
            return 1.5f;

        if (attackDurations == null || attackDurations.Length == 0)
            return 0.37f;

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDurations.Length - 1);
        return attackDurations[clampedIndex];
    }


}