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

	private Label _label;
	private Vector2 _worldPosition;

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
			parent.AddChild(damageNumber);
		}
		else
		{
			entity.AddChild(damageNumber);
		}

		// Try to find Area2D with CircleShape2D to spawn at random position within it
		Area2D area2D = entity.FindChild("*", owned: false) as Area2D;
		float spawnRadius = DefaultSpawnRadius;
		
		if (area2D != null)
		{
			CircleShape2D circleShape = null;
			// Find the first CollisionShape2D child with a CircleShape2D
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
		damageNumber.Display(damageAmount, style);
	}

	private static Vector2 GetRandomPositionInCircle(float radius)
	{
		// Generate random position within a circle using rejection sampling
		float angle = GD.Randf() * Mathf.Tau;
		float distance = Mathf.Sqrt(GD.Randf()) * radius;
		return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
	}

	public override void _Ready()
	{
		_label = GetNode<Label>("Label");
		if (_label == null)
		{
			GD.PrintErr("DamageNumber: Label child node not found");
		}
	}

	private void Display(float damageAmount, DamageNumberStyle style)
	{
		if (_label == null)
			return;

		_label.Text = damageAmount.ToString("0");

		LabelSettings labelSettings = style switch
		{
			DamageNumberStyle.Standard => StandardLabelSettings,
			DamageNumberStyle.Weakness => WeaknessLabelSettings,
			_ => StandardLabelSettings
		};

		_label.LabelSettings = labelSettings;

		Vector2 popTarget = style == DamageNumberStyle.Weakness ? new Vector2(.2f, .2f) : new Vector2(.15f, .15f);
		Vector2 settleTarget = style == DamageNumberStyle.Weakness ? new Vector2(0.3f, 0.3f) : new Vector2(0.25f, 0.25f);
		Vector2 floatTarget = Position + new Vector2(0f, style == DamageNumberStyle.Weakness ? -24f : -18f);

		Tween tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "scale", popTarget, 0.08f);
		tween.TweenProperty(this, "scale", settleTarget, 0.08f);

		tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "position", floatTarget, FloatUpDuration);
		tween.Parallel().TweenProperty(_label, "modulate:a", 0f, FloatUpDuration);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
