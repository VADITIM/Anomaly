using Godot;

public abstract partial class Enemy
{
    public void InitializeEnemy()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;

        StatsDisplay();
        InitializeTenacity();
        InitializeBars();
        InitializeStateMachine();

        PlayAnimation(GetCurrentEnemyAnimation());
    }

    private void StatsDisplay()
    {
        testDisplay = GetNodeOrNull<RichTextLabel>("Label")
                      ?? GetNodeOrNull<RichTextLabel>("CanvasLayer/Label")
                      ?? GetTree().CurrentScene?.FindChild("Label", true, false) as RichTextLabel;

        if (testDisplay != null)
            testDisplay.BbcodeEnabled = true;
    }

    private void InitializeTenacity()
    {
        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        if (TenacityCooldownCue != null)
            TenacityCooldownCue.Visible = false;

        maxTenacity = tenacity;
    }

    private void InitializeStateMachine()
    {
        StateMachine = GetNodeOrNull<EnemyStateMachine>("EnemyStateMachine");

        if (StateMachine == null)
        {
            StateMachine = new EnemyStateMachine();
            AddChild(StateMachine);
        }

        StateMachine.SetMaxStaggers(maxStaggers);
        StateMachine.Target = Player;

        StateMachine.OnDied += OnDeathHandler;
        StateMachine.OnStateChanged += OnStateChangedHandler;

        TenacitySystem = new TenacitySystem(this, this, StateMachine);
    }

    public override void InitializeBars()
    {
        SetHealth(health);
        SetMaxHealth(health);

        base.InitializeBars();
        TenacityBar = InitializeEntity.FindTextureProgressBar(ResourceBarControl, "Tenacity Bar");
    }
}
