using Godot;

public partial class Weapon
{
    public void StartAttackSequence(bool isHeavy)
    {
        _entitiesHitThisSwing.Clear();

        if (isHeavy)
        {
            ResetAttackSequence();
            return;
        }

        _queuedAttackFollowUp = false;
        _comboWindowTimer = 0f;
    }

    public void QueueAttackFollowUp()
    {
        if (_comboWindowTimer > 0f)
        {
            _queuedAttackFollowUp = true;
        }
    }

    public bool TryConsumeQueuedAttack(bool isHeavy, out float duration)
    {
        duration = 0f;

        if (!_queuedAttackFollowUp)
            return false;

        _queuedAttackFollowUp = false;
        _comboWindowTimer = 0f;

        if (isHeavy)
        {
            duration = _currentArc?.HeavyAttackDuration ?? 1.5f;
            return true;
        }

        _attackSequenceIndex = Mathf.Min(_attackSequenceIndex + 1, MaxComboSteps - 1);

        int clampedIndex = Mathf.Clamp(_attackSequenceIndex, 0, _attackDurations.Length - 1);
        duration = _currentArc != null
            ? _currentArc.GetAttackSequenceDuration(clampedIndex)
            : _attackDurations[clampedIndex];
        return true;
    }

    public void ResetAttackSequence()
    {
        _attackSequenceIndex = 0;
        _comboWindowTimer = 0f;
        _queuedAttackFollowUp = false;
    }

    private void UpdateComboTimers(float delta)
    {
        if (_comboCooldownTimer > 0f)
        {
            _comboCooldownTimer = Mathf.Max(_comboCooldownTimer - delta, 0f);
            if (_comboCooldownTimer <= 0f)
                    return;
        }

        if (_comboWindowTimer > 0f)
        {
            _comboWindowTimer = Mathf.Max(_comboWindowTimer - delta, 0f);
            if (_comboWindowTimer <= 0f)
            {
                ResetAttackSequence();
            }
        }
    }

    private float GetCurrentAttackSequenceDuration(bool isHeavy)
    {
        if (_currentArc != null)
        {
            if (isHeavy)
                return _currentArc.HeavyAttackDuration;

            return _currentArc.GetAttackSequenceDuration(_attackSequenceIndex);
        }

        if (isHeavy)
            return 1.5f;

        if (_attackDurations == null || _attackDurations.Length == 0)
            return 0.37f;

        int clampedIndex = Mathf.Clamp(_attackSequenceIndex, 0, _attackDurations.Length - 1);
        return _attackDurations[clampedIndex];
    }
}
