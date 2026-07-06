using Godot;

public partial class SegmentManager : Control
{
	public Player Player;

	[Export] public TextureProgressBar HealthBar;
	[Export] public TextureProgressBar HealthStar;
	[Export] public TextureProgressBar CorruptionBar;
	[Export] public TextureProgressBar VesselBar;
	[Export] public TextureProgressBar StaminaBar;
	[Export] public TextureProgressBar StaminaStar;

	[Export] public float HealthBarMaxWidth = 100f;
	private float HealthBarMaxValue = 100f;
	private float HealthStatMaxValue = 200f;
	private float HealthStarOffsetThreshold = 50f;

	private Vector2 _healthBarBaseSize;
	private Vector2 _healthStarBasePos;

	private float lastMaxHealth = -1f;
	private float lastHealth = -1f;
	private float lastCorruption = -1f;
	private float lastVessel = -1f;
	private float lastHealthS = -1f;
	private float lastMaxStamina = -1f;
	private float lastStamina = -1f;
	private float lastStaminaS = -1f;

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

		int maxHealth = (int)Player.Stats.GetCurrentMax(StatType.Health);
		int health = (int)Player.Stats.GetCurrent(StatType.Health);
		float corruption = Player.Stats.GetCurrent(StatType.Corruption);
		float vessel = Player.Stats.GetCurrent(StatType.Vessel);
		float healthS = Player.Stats.GetCurrent(StatType.HealthS);
		int maxStamina = (int)Player.Stats.GetCurrentMax(StatType.Stamina);
		float stamina = Player.Stats.GetCurrent(StatType.Stamina);
		float staminaS = Player.Stats.GetCurrent(StatType.StaminaS);

		if (maxHealth != lastMaxHealth || health != lastHealth)
		{
			lastMaxHealth = maxHealth;
			lastHealth = health;
			UpdateHealthUI(maxHealth, health);
		}

		if (!Mathf.IsEqualApprox(corruption, lastCorruption))
		{
			lastCorruption = corruption;
			UpdateCorruptionUI(corruption);
		}

		if (!Mathf.IsEqualApprox(vessel, lastVessel) || !Mathf.IsEqualApprox(healthS, lastHealthS))
		{
			lastVessel = vessel;
			lastHealthS = healthS;
			UpdateVesselUI(vessel);
			UpdateHealthSUI(healthS);
		}

		if (maxStamina != lastMaxStamina || !Mathf.IsEqualApprox(stamina, lastStamina) || !Mathf.IsEqualApprox(staminaS, lastStaminaS))
		{
			lastMaxStamina = maxStamina;
			lastStamina = stamina;
			UpdateStaminaUI(maxStamina, stamina);
			lastStaminaS = staminaS;
			UpdateStaminaSUI(staminaS);
		}
	}

	private void ResolveBars()
	{
		Node root = GetParent() ?? this;

		HealthBar ??= root.FindChild("Health Bar", true, false) as TextureProgressBar;
		HealthStar ??= root.FindChild("Health Star", true, false) as TextureProgressBar;
		CorruptionBar ??= root.FindChild("Corruption Bar", true, false) as TextureProgressBar;
		VesselBar ??= root.FindChild("Vessel Bar", true, false) as TextureProgressBar;
		StaminaBar ??= root.FindChild("Stamina Bar", true, false) as TextureProgressBar;
		StaminaStar ??= root.FindChild("Stamina Star", true, false) as TextureProgressBar;
	}

	private void CacheBaseLayout()
	{
		if (HealthBar != null)
			_healthBarBaseSize = HealthBar.Size;
		if (HealthStar != null)
			_healthStarBasePos = HealthStar.Position;
	}

	private void UpdateAll()
	{
		if (Player?.Stats == null)
			return;

		int maxHealth = (int)Player.Stats.GetCurrentMax(StatType.Health);
		int health = (int)Player.Stats.GetCurrent(StatType.Health);
		float corruption = Player.Stats.GetCurrent(StatType.Corruption);
		float vessel = Player.Stats.GetCurrent(StatType.Vessel);
		float healthS = Player.Stats.GetCurrent(StatType.HealthS);
		int maxStamina = (int)Player.Stats.GetCurrentMax(StatType.Stamina);
		float stamina = Player.Stats.GetCurrent(StatType.Stamina);
		float staminaS = Player.Stats.GetCurrent(StatType.StaminaS);

		lastMaxHealth = maxHealth;
		lastHealth = health;
		lastVessel = vessel;
		lastCorruption = corruption;
		lastHealthS = healthS;
		lastMaxStamina = maxStamina;
		lastStamina = stamina;
		lastStaminaS = staminaS;

		UpdateHealthUI(maxHealth, health);
		UpdateCorruptionUI(corruption);
		UpdateVesselUI(vessel);
		UpdateHealthSUI(healthS);
		UpdateStaminaUI(maxStamina, stamina);
		UpdateStaminaSUI(staminaS);

		if (VesselBar != null)
		{
			VesselBar.Visible = true;
			VesselBar.ZIndex = 10;
		}
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
			HealthStar.Value = Player.Stats.GetCurrent(StatType.HealthS);
			float extraOffset = Mathf.Max(0f, GetHealthBarWidthFromMax(maxHealth) - HealthStarOffsetThreshold);
			HealthStar.Position = _healthStarBasePos + new Vector2(extraOffset, 0f);
		}
	}

	private void UpdateCorruptionUI(float corruption)
	{
		if (CorruptionBar == null)
			return;

		CorruptionBar.MaxValue = 100f;
		CorruptionBar.Value = Mathf.Clamp(corruption, 0f, 100f);
	}

	private void UpdateVesselUI(float vessel)
	{
		if (VesselBar == null)
			return;

		VesselBar.MaxValue = 100f;
		VesselBar.Value = Mathf.Clamp(vessel, 0f, 100f);
	}

	private void UpdateHealthSUI(float healthS)
	{
		if (HealthStar == null)
			return;

		HealthStar.MaxValue = 100f;
		HealthStar.Value = Mathf.Clamp(healthS, 0f, 100f);
	}

	private void UpdateStaminaUI(float maxStamina, float stamina)
	{
		if (StaminaBar != null)
		{
			StaminaBar.MaxValue = GetStaminaBarWidthFromMax(maxStamina);
			StaminaBar.Value = GetStaminaBarWidthFromValue(stamina);
			StaminaBar.Size = new Vector2(GetStaminaBarWidthFromMax(maxStamina), StaminaBar.Size.Y);
		}
	}

	private void UpdateStaminaSUI(float staminaS)
	{
		if (StaminaStar == null)
			return;

		StaminaStar.MaxValue = 100f;
		StaminaStar.Value = Mathf.Clamp(staminaS, 0f, 100f);
	}

	private float GetHealthBarWidthFromMax(int maxHealth)
	{
		if (HealthStatMaxValue <= 0f)
			return _healthBarBaseSize.X;

		float width = Mathf.Clamp(maxHealth * 0.5f, 0f, HealthBarMaxWidth);
		return Mathf.Max(0f, width);
	}

	private float GetStaminaBarWidthFromValue(float stamina)
	{
		float width = Mathf.Max(0f, stamina * 0.5f);
		return width;
	}

	private float GetStaminaBarWidthFromMax(float maxStamina)
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
