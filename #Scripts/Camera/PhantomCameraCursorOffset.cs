using Godot;

public partial class PhantomCameraCursorOffset : Node2D
{
	[Export] private NodePath phantomCameraPath;
	[Export] private NodePath cursorDeadzonePath;
	[Export] private Vector2 maxCursorOffset = new Vector2(64f, 32f);
	[Export] private float offsetLerpSpeed = 20f;
	[Export] private float returnLerpSpeed = 10f;

	private Node2D phantomCamera;
	private Area2D cursorDeadzone;
	private CollisionShape2D cursorDeadzoneShape;
	private Vector2 currentOffset = Vector2.Zero;

	private Camera cameraScript;

	public override void _Ready()
	{
		if (!phantomCameraPath.IsEmpty)
		{
			phantomCamera = GetNodeOrNull<Node2D>(phantomCameraPath);
		}
		else
		{
			phantomCamera = GetParentOrNull<Node2D>();
		}

		if (!cursorDeadzonePath.IsEmpty)
		{
			cursorDeadzone = GetNodeOrNull<Area2D>(cursorDeadzonePath);
		}

		if (cursorDeadzone != null)
		{
			cursorDeadzoneShape = cursorDeadzone.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		}

		cameraScript = GetViewport().GetCamera2D() as Camera;
	}

	public override void _Process(double delta)
	{
		if (phantomCamera == null) return;

		bool isLocked = cameraScript != null && cameraScript.IsLocked;
		Vector2 targetOffset = Vector2.Zero;

		if (!isLocked)
		{
			Vector2 mouseGlobal = GetGlobalMousePosition();
			bool isInsideDeadzone = IsMouseInsideDeadzone(mouseGlobal);

			if (!isInsideDeadzone)
			{
				Vector2 desired = mouseGlobal - phantomCamera.GlobalPosition;
				desired = new Vector2(
					Mathf.Clamp(desired.X, -maxCursorOffset.X, maxCursorOffset.X),
					Mathf.Clamp(desired.Y, -maxCursorOffset.Y, maxCursorOffset.Y)
				);
				targetOffset = desired;
			}
		}

		float speed = (isLocked || targetOffset == Vector2.Zero) ? returnLerpSpeed : offsetLerpSpeed;
		currentOffset = currentOffset.Lerp(targetOffset, 1f - Mathf.Exp(-speed * (float)delta));

		phantomCamera.Set("follow_offset", currentOffset);
	}

	private bool IsMouseInsideDeadzone(Vector2 mouseGlobal)
	{
		if (cursorDeadzoneShape == null || cursorDeadzoneShape.Shape == null)
		{
			return false;
		}

		if (cursorDeadzoneShape.Shape is RectangleShape2D rectShape)
		{
			Vector2 localPoint = cursorDeadzoneShape.ToLocal(mouseGlobal);
			Rect2 localRect = new Rect2(-rectShape.Size * 0.5f, rectShape.Size);
			return localRect.HasPoint(localPoint);
		}

		if (cursorDeadzoneShape.Shape is CircleShape2D circleShape)
		{
			Vector2 localPoint = cursorDeadzoneShape.ToLocal(mouseGlobal);
			return localPoint.Length() <= circleShape.Radius;
		}

		return false;
	}
}
