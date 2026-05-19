using Godot;
using System;

public partial class CameraFocus : Camera
{
    public const float CAMERA_FOCUS_RANGE = 300f;

    [Export] protected float focusSpeed = 4f;
    [Export] protected float offsetRadius = 100;
    [Export] protected bool showDebugSphere = false;

    protected Vector2 FocusOffset { get; set; } = Vector2.Zero;
    protected Vector2 ShakeOffset { get; set; } = Vector2.Zero;
    protected ColorRect debugSphere;

    protected float shakeDecay = .01f;
    protected float shakeIntensity = 0f;
    protected float shakeTimer = 0f;

    private Tween centerOffsetTween;
    private bool isCenteringToPlayer = false;
    private float tweenDuration = .4f;

    protected bool focusActive = true;
    public bool FocusActive => focusActive;
    public override bool IsLocked => focusActive && Enemy != null;

    public override void _Ready()
    {
        base._Ready();

        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        
        debugSphere = new ColorRect();
        debugSphere.CustomMinimumSize = new Vector2(8, 8);
        debugSphere.Color = new Color(1, 1, 0, 0.7f); 
        debugSphere.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(debugSphere);
        debugSphere.Visible = showDebugSphere;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(Keybinds.FocusToggle))
        {
            focusActive = !focusActive;
            if (!focusActive)
            {
                StartCenterTween();
                Enemy = null;
                ResetShakeOffset();
            }
        }

        if (!focusActive)
        {
            Offset = GetCameraOffset();
            return;
        }

        UpdateFocusOffset((float)delta);
        UpdateShakeOffset(delta);
        Offset = GetCameraOffset();
        UpdateDebugSphere();
    }

    protected virtual void UpdateFocusOffset(float delta)
    {
        Vector2 cursorPosition = GetGlobalMousePosition();
        Enemy targetEnemy = Enemy.GetBestCameraTarget(cursorPosition);
        Enemy = targetEnemy;

        if (targetEnemy == null)
        {
            StartCenterTween();
            return;
        }

        StopCenterTween();

        Vector2 desiredOffset = Vector2.Zero;
        Vector2 toEnemy = targetEnemy.GlobalPosition - Player.GlobalPosition;
        desiredOffset = toEnemy * 0.5f;

        if (desiredOffset.Length() > offsetRadius)
            desiredOffset = desiredOffset.Normalized() * offsetRadius;

        float lerpWeight = 1f - Mathf.Exp(-focusSpeed * delta);
        FocusOffset = FocusOffset.Lerp(desiredOffset, lerpWeight);
    }

    protected void UpdateShakeOffset(double delta)
    {
        ShakeOffset = CameraFeedback.TenacityBreakShake(delta, ref shakeDecay, ref shakeIntensity, ref shakeTimer);
    }

    protected void ResetShakeOffset()
    {
        ShakeOffset = Vector2.Zero;
        shakeDecay = .01f;
        shakeIntensity = 0f;
        shakeTimer = 0f;
    }

    protected Vector2 GetCameraOffset()
    {
        return FocusOffset + ShakeOffset;
    }

    protected void UpdateDebugSphere()
    {
        if (debugSphere != null && Player != null)
        {
            Vector2 focusPoint = Player.GlobalPosition + FocusOffset;
            debugSphere.GlobalPosition = focusPoint - new Vector2(4, 4);
            debugSphere.Visible = showDebugSphere;
        }
    }

    protected void StartCenterTween()
    {
        if (isCenteringToPlayer)
            return;

        centerOffsetTween?.Kill();
        isCenteringToPlayer = true;
        centerOffsetTween = CreateTween();
        centerOffsetTween.SetTrans(Tween.TransitionType.Sine);
        centerOffsetTween.SetEase(Tween.EaseType.InOut);
        centerOffsetTween.TweenProperty(this, nameof(FocusOffset), Vector2.Zero, tweenDuration);
        centerOffsetTween.Finished += OnCenterTweenFinished;
    }

    protected void StopCenterTween()
    {
        if (!isCenteringToPlayer)
            return;

        centerOffsetTween?.Kill();
        centerOffsetTween = null;
        isCenteringToPlayer = false;
    }

    private void OnCenterTweenFinished()
    {
        centerOffsetTween = null;
        isCenteringToPlayer = false;
    }

    public override void ShakeCamera(float intensity = 0f)
    {
        CameraFeedback.ShakeCamera(ref shakeIntensity, ref shakeTimer, intensity);
    }
}
