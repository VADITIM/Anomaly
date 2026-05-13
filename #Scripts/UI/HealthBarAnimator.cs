using Godot;

public class HealthBarAnimator
{
    private Control resourceBarControl;
    private TextureProgressBar healthBar;
    private TextureProgressBar healthBarGhost;

    private float _ghostValue = 0f;
    private float _ghostTarget = 0f;
    private float _noDamageTimer = 0f;
    private bool  _ghostDraining = false;

    private const float GhostDelay = 1.5f;
    private const float GhostDrainSpeed = 0.5f;

    private Vector2 _barBasePosition = Vector2.Zero;
    private float _shakeTimer = 0f;
    private float _shakeDuration = 0.3f;
    private float _shakeIntensity = 6f;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public void Initialize(Control resourceControl, TextureProgressBar health, TextureProgressBar ghost, float initialHealth)
    {
        resourceBarControl = resourceControl;
        healthBar = health;
        healthBarGhost = ghost;

        if (resourceBarControl != null)
            _barBasePosition = resourceBarControl.Position;

        _rng.Randomize();

        _ghostValue = initialHealth;
        _ghostTarget = initialHealth;
        if (healthBarGhost != null)
        {
            healthBarGhost.Value = _ghostValue;
        }
    }

    public void Update(float delta)
    {
        UpdateBarShake(delta);
        UpdateGhostBar(delta);
    }

    public void OnHealthChanged(float newHealth, float max)
    {
        if (healthBar != null)
        {
            healthBar.MaxValue = Mathf.Max(max, 1f);
            healthBar.Value = Mathf.Clamp(newHealth, 0f, Mathf.Max(max, 1f));
        }

        if (healthBarGhost == null) return;

        if (newHealth < _ghostTarget)
        {
            _ghostTarget = newHealth;
            _noDamageTimer = 0f;
            _ghostDraining = false;
            healthBarGhost.MaxValue = Mathf.Max(max, 1f);
            TriggerBarShake();
        }
        else
        {
            if (newHealth > _ghostValue)
            {
                _ghostValue = newHealth;
                _ghostTarget = newHealth;
                healthBarGhost.MaxValue = Mathf.Max(max, 1f);
                healthBarGhost.Value = _ghostValue;
            }
        }
    }

    public void TriggerBarShake()
    {
        if (resourceBarControl == null) return;
        _shakeTimer = _shakeDuration;
    }

    private void UpdateGhostBar(float delta)
    {
        if (healthBarGhost == null) return;

        if (!_ghostDraining)
        {
            _noDamageTimer += delta;
            if (_noDamageTimer >= GhostDelay)
                _ghostDraining = true;
        }

        if (_ghostDraining && _ghostValue > _ghostTarget)
        {
            float drainAmount = Mathf.Max(1f, (float)healthBarGhost.MaxValue) * GhostDrainSpeed * delta;
            _ghostValue = Mathf.MoveToward(_ghostValue, _ghostTarget, drainAmount);
            healthBarGhost.Value = _ghostValue;

            if (Mathf.IsEqualApprox(_ghostValue, _ghostTarget))
                _ghostDraining = false;
        }
    }

    private void UpdateBarShake(float delta)
    {
        if (resourceBarControl == null || _shakeTimer <= 0f) return;

        _shakeTimer -= delta;

        if (_shakeTimer > 0f)
        {
            float t = _shakeTimer / _shakeDuration;
            float current = _shakeIntensity * t;

            resourceBarControl.Position = _barBasePosition + new Vector2(
                _rng.RandfRange(-current, current),
                _rng.RandfRange(-current, current)
            );
        }
        else
        {
            _shakeTimer = 0f;
            resourceBarControl.Position = _barBasePosition;
        }
    }
}
