using Godot;

public partial class SegmentManager : Control
{
	public Player Player;

	[Export] public TextureProgressBar HealthBar;
	[Export] public TextureProgressBar HealthStar;
	[Export] public TextureProgressBar VesselBar;
	[Export] public TextureProgressBar StaminaBar;
	[Export] public TextureProgressBar StaminaStar;

	[Export] public float HealthBarMaxWidth = 100f;
	private float HealthBarMaxValue = 100f;
	private float HealthStatMaxValue = 200f;
	private float HealthStarOffsetThreshold = 50f;

	private Vector2 _healthBarBaseSize;
	private Vector2 _vesselBarBaseSize;
	private Vector2 _healthStarBasePos;

	private int lastMaxHealth = -1;
	private int lastHealth = -1;
	private int lastMaxVessel = -1;
	private int lastVessel = -1;
	private int lastMaxStamina = -1;
	private int lastStamina = -1;

	public override void _Ready()
	{
		if (Player == null)
			Player = GetTree().Root.FindChild("Player", true, false) as Player;

		ResolveBars();
		CacheBaseLayout();
		UpdateAll();
	}

	public override void _Process(double delta)
	{
		if (Player?.Stats == null)
			return;

		int maxHealth = (int)Player.Stats.GetCurrentMax("Health");
		int health = (int)Player.Stats.GetCurrent("Health");
		int maxVessel = (int)Player.Stats.GetCurrentMax("Vessel");
		int vessel = (int)Player.Stats.GetCurrent("Vessel");
		int maxStamina = (int)Player.Stats.GetCurrentMax("Stamina");
		int stamina = (int)Player.Stats.GetCurrent("Stamina");

		if (maxHealth != lastMaxHealth || health != lastHealth)
		{
			lastMaxHealth = maxHealth;
			lastHealth = health;
			UpdateHealthUI(maxHealth, health);
			UpdateVesselSizeForHealth(maxHealth);
		}

		if (maxVessel != lastMaxVessel || vessel != lastVessel)
		{
			lastMaxVessel = maxVessel;
			lastVessel = vessel;
			UpdateVesselUI(maxVessel, vessel);
		}

		if (maxStamina != lastMaxStamina || stamina != lastStamina)
		{
			lastMaxStamina = maxStamina;
			lastStamina = stamina;
			UpdateStaminaUI(maxStamina, stamina);
		}
	}

	private void ResolveBars()
	{
		Node root = GetParent() ?? this;

		HealthBar ??= root.FindChild("Health Bar", true, false) as TextureProgressBar;
		HealthStar ??= root.FindChild("Health Star", true, false) as TextureProgressBar;
		VesselBar ??= root.FindChild("Vessel Bar", true, false) as TextureProgressBar;
		StaminaBar ??= root.FindChild("Stamina Bar", true, false) as TextureProgressBar;
		StaminaStar ??= root.FindChild("Stamina Star", true, false) as TextureProgressBar;
	}

	private void CacheBaseLayout()
	{
		if (HealthBar != null)
			_healthBarBaseSize = HealthBar.Size;
		if (VesselBar != null)
			_vesselBarBaseSize = VesselBar.Size;
		if (HealthStar != null)
			_healthStarBasePos = HealthStar.Position;
	}

	private void UpdateAll()
	{
		if (Player?.Stats == null)
			return;

		int maxHealth = (int)Player.Stats.GetCurrentMax("Health");
		int health = (int)Player.Stats.GetCurrent("Health");
		int maxVessel = (int)Player.Stats.GetCurrentMax("Vessel");
		int vessel = (int)Player.Stats.GetCurrent("Vessel");
		int maxStamina = (int)Player.Stats.GetCurrentMax("Stamina");
		int stamina = (int)Player.Stats.GetCurrent("Stamina");

		lastMaxHealth = maxHealth;
		lastHealth = health;
		lastMaxVessel = maxVessel;
		lastVessel = vessel;
		lastMaxStamina = maxStamina;
		lastStamina = stamina;

		UpdateHealthUI(maxHealth, health);
		UpdateVesselSizeForHealth(maxHealth);
		UpdateVesselUI(maxVessel, vessel);
		UpdateStaminaUI(maxStamina, stamina);
	}

	public void Refresh()
	{
		UpdateAll();
	}

	private void UpdateHealthUI(int maxHealth, int health)
	{
		if (HealthBar != null)
		{
			HealthBar.MaxValue = HealthBarMaxValue;
			HealthBar.Value = MapHealthToUiValue(health);
			HealthBar.Size = new Vector2(GetHealthBarWidthFromMax(maxHealth), HealthBar.Size.Y);
		}

		if (HealthStar != null)
		{
			HealthStar.MaxValue = HealthBarMaxValue;
			HealthStar.Value = MapHealthToUiValue(health);
			float extraOffset = Mathf.Max(0f, GetHealthBarWidthFromMax(maxHealth) - HealthStarOffsetThreshold);
			HealthStar.Position = _healthStarBasePos + new Vector2(extraOffset, 0f);
		}
	}

	private void UpdateVesselUI(int maxVessel, int vessel)
	{
		if (VesselBar == null)
			return;

		VesselBar.MaxValue = maxVessel;
		VesselBar.Value = vessel;
	}

	private void UpdateStaminaUI(int maxStamina, int stamina)
	{
		if (StaminaBar != null)
		{
			StaminaBar.MaxValue = GetStaminaBarWidthFromMax(maxStamina);
			StaminaBar.Value = GetStaminaBarWidthFromValue(stamina);
			StaminaBar.Size = new Vector2(GetStaminaBarWidthFromMax(maxStamina), StaminaBar.Size.Y);
		}

		if (StaminaStar != null)
		{
			StaminaStar.MaxValue = maxStamina;
			StaminaStar.Value = stamina;
		}
	}

	private void UpdateVesselSizeForHealth(int maxHealth)
	{
		if (VesselBar == null || HealthBar == null)
			return;

		float healthWidth = GetHealthBarWidthFromMax(maxHealth);
		float healthDelta = healthWidth - _healthBarBaseSize.X;
		float vesselWidth = _vesselBarBaseSize.X + healthDelta;
		VesselBar.Size = new Vector2(Mathf.Max(0f, vesselWidth), VesselBar.Size.Y);
	}

	private float GetHealthBarWidthFromMax(int maxHealth)
	{
		if (HealthStatMaxValue <= 0f)
			return _healthBarBaseSize.X;

		float width = Mathf.Clamp(maxHealth * 0.5f, 0f, HealthBarMaxWidth);
		return Mathf.Max(0f, width);
	}

	private float GetStaminaBarWidthFromValue(int stamina)
	{
		float width = Mathf.Max(0f, stamina * 0.5f);
		return width;
	}

	private float GetStaminaBarWidthFromMax(int maxStamina)
	{
		float width = Mathf.Max(0f, maxStamina * 0.5f);
		return width;
	}

	private float MapHealthToUiValue(float health)
	{
		if (HealthStatMaxValue <= 0f)
			return 0f;

		float t = Mathf.Clamp(health, 0f, HealthStatMaxValue) / HealthStatMaxValue;
		return t * HealthBarMaxValue;
	}
}
