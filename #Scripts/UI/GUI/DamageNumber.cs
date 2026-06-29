using Godot;

public enum DamageNumberStyle
{
    Standard,
    Weakness
}

public partial class DamageNumber : Node2D
{
    private const string DamageNumberScenePath = "res://#Scenes/UI/GUI/Damage Number.tscn";
    private const float FloatUpDuration = 0.35f;
    private const float DefaultSpawnRadius = 10f;

    private static readonly PackedScene DamageNumberScene = ResourceLoader.Load<PackedScene>(DamageNumberScenePath);
    private static readonly LabelSettings StandardLabelSettings = GD.Load<LabelSettings>("uid://b3fckkt7fb5ia");
    private static readonly LabelSettings WeaknessLabelSettings = GD.Load<LabelSettings>("uid://bnhgs46udcqgv");

    private Label label;
    private Vector2 worldPosition;
    private float pendingDamage;
    private DamageNumberStyle pendingStyle;

    public static void Spawn(Entity entity, float damageAmount, DamageNumberStyle style, Node parent = null)
    {
        if (entity == null || DamageNumberScene == null)
        {
            GD.PrintErr("Failed to spawn damage number: entity or scene is null");
            return;
        }

        DamageNumber damageNumber = DamageNumberScene.Instantiate<DamageNumber>();

        if (parent != null)
        {
            parent.CallDeferred(Node.MethodName.AddChild, damageNumber);
        }
        else
        {
            entity.CallDeferred(Node.MethodName.AddChild, damageNumber);
        }

        Area2D area2D = entity.FindChild("*", owned: false) as Area2D;
        float spawnRadius = DefaultSpawnRadius;

        if (area2D != null)
        {
            CircleShape2D circleShape = null;
            foreach (Node child in area2D.GetChildren())
            {
                if (child is CollisionShape2D collisionShape && collisionShape.Shape is CircleShape2D circle)
                {
                    circleShape = circle;
                    break;
                }
            }

            if (circleShape != null)
            {
                spawnRadius = circleShape.Radius;
            }
        }

        damageNumber.Position = GetRandomPositionInCircle(spawnRadius);
        damageNumber.pendingDamage = damageAmount;
        damageNumber.pendingStyle = style;
    }

    private static Vector2 GetRandomPositionInCircle(float radius)
    {
        float angle = GD.Randf() * Mathf.Tau;
        float distance = Mathf.Sqrt(GD.Randf()) * radius;
        return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
    }

    public override void _Ready()
    {
        label = GetNode<Label>("Label");
        if (label == null)
        {
            GD.PrintErr("DamageNumber: Label child node not found");
            return;
        }
        Display(pendingDamage, pendingStyle);
    }

    private void Display(float damageAmount, DamageNumberStyle style)
    {
        if (label == null)
            return;

        label.Text = damageAmount.ToString("0");

        LabelSettings labelSettings = style switch
        {
            DamageNumberStyle.Standard => StandardLabelSettings,
            DamageNumberStyle.Weakness => WeaknessLabelSettings,
            _ => StandardLabelSettings
        };

        label.LabelSettings = labelSettings;

        Vector2 popTarget    = style == DamageNumberStyle.Weakness ? new Vector2(.2f, .2f) : new Vector2(.15f, .15f);
        Vector2 settleTarget = style == DamageNumberStyle.Weakness ? new Vector2(0.3f, 0.3f) : new Vector2(0.25f, 0.25f);
        Vector2 floatTarget  = Position + new Vector2(0f, style == DamageNumberStyle.Weakness ? -24f : -18f);

        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", popTarget, 0.08f);
        tween.TweenProperty(this, "scale", settleTarget, 0.08f);

        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", floatTarget, FloatUpDuration);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, FloatUpDuration);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
