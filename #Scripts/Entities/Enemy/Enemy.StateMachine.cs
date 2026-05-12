using Godot;

public abstract partial class Enemy
{
    private void OnStateChangedHandler(EnemyState oldState, EnemyState newState)
    {
        OnStateChanged(oldState, newState);
        PlayAnimation(GetCurrentEnemyAnimation());
    }

    protected virtual void OnStateChanged(EnemyState oldState, EnemyState newState) { }

    private string GetCurrentEnemyAnimation()
    {
        if (StateMachine != null && StateMachine.IsDead)
            return "Die_Down";

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Move_Down"))
            return "Move_Down";

        return null;
    }

    private void OnDeathHandler()
    {
        OnDeath();

        ResourceBarControl?.QueueFree();

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Die_Down"))
        {
            AnimationPlayer.Play("Die_Down");
            float animationLength = AnimationPlayer.GetAnimation("Die_Down").Length;
            SceneTreeTimer timer = GetTree().CreateTimer((double)Mathf.Max(animationLength, 0.1f));
            timer.Timeout += QueueFree;
            return;
        }

        QueueFree();
    }
}
