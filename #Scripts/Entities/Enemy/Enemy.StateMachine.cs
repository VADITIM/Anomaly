using Godot;

public abstract partial class Enemy
{
    public bool IsStaggered => TenacitySystem?.IsStaggered ?? false;
    public bool IsInStaggerWindow => TenacitySystem?.IsInStaggerWindow ?? false;
    public bool IsRecovering => TenacitySystem?.IsRecovering ?? false;
    public bool IsDead => StateMachine?.IsDead ?? false;
    public int CurrentStaggers => TenacitySystem?.CurrentStaggerCount ?? 0;
    public bool HasCameraFocus => HasBeenHit && !IsDead && IsWithinCameraFocusRange();

    private void InitializeStateMachine()
    {
        StateMachine.SetMaxStaggers(MaxStaggers);
        StateMachine.Target = Player;

        StateMachine.OnDied += OnDeathHandler;
        StateMachine.OnStateChanged += OnStateChangedHandler;

        TenacitySystem = new TenacitySystem(this, this, StateMachine);
    }

    protected override State GetCurrentAnimationState()
    {
        return StateMachine?.CurrentState ?? State.Idle;
    }

    private void OnStateChangedHandler(EnemyState oldState, EnemyState newState)
    {
        OnStateChanged(oldState, newState);
        UpdateAnimation();
    }

    protected virtual void OnStateChanged(EnemyState oldState, EnemyState newState) { }

    private void OnDeathHandler()
    {
        OnDeath();

        GetBehavior<ResourceBarBehavior>()?.DestroyBars();

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Die_Down"))
        {
            AnimationPlayer.Play("Die_Down");
            float animationLength = (float)AnimationPlayer.GetAnimation("Die_Down").Length;
            SceneTreeTimer timer = GetTree().CreateTimer((double)Mathf.Max(animationLength, 0.1f));
            timer.Timeout += QueueFree;
            return;
        }

        QueueFree();
    }
}
