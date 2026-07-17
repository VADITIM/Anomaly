using Godot;

// Drives the Player's resource meters (TextureProgressBars) from PlayerStats.
// Bars no longer draw segments: each stat upgrade widens its bar by WidthPerUpgrade
// pixels to the right. The Health Star tracks the Health Bar's right edge, and the
// Vessel Bar's width is tied to Health upgrades while its max value stays fixed.
public partial class PlayerResourceBars : Control
{
	[Export] public float WidthPerUpgrade = 5f;

	private Player _player;

	private TextureProgressBar _healthBar;
	private TextureProgressBar _healthStar;
	private TextureProgressBar _corruptionBar;
	private TextureProgressBar _vesselBar;
	private TextureProgressBar _staminaBar;
	private TextureProgressBar _staminaStar;

	private float _healthBarBaseWidth;
	private Vector2 _healthStarBasePos;
	private float _vesselBarBaseWidth;
	private float _staminaBarBaseWidth;
	private Vector2 _staminaStarBasePos;

	private float _lastHealth = -1f;
	private float _lastMaxHealth = -1f;
	private int _lastHealthUpgrades = -1;
	private float _lastCorruption = -1f;
	private float _lastVessel = -1f;
	private float _lastHealthS = -1f;
	private float _lastStamina = -1f;
	private float _lastMaxStamina = -1f;
	private int _lastStaminaUpgrades = -1;
	private float _lastStaminaS = -1f;

	public override void _Ready()
	{
		_player = GetTree().Root.FindChild("Player", true, false) as Player;
		ResolveBars();

		// Scene layout has not resolved yet in _Ready, so the bars' authored sizes
		// read as zero. Cache them one frame later, then draw the first state.
		CallDeferred(MethodName.InitAfterLayout);
	}

	public override void _Process(double delta)
	{
		PlayerStats stats = _player?.Stats;
		if (stats == null)
			return;

		SyncHealth(stats);
		SyncCorruption(stats);
		SyncVessel(stats);
		SyncHealthS(stats);
		SyncStamina(stats);
		SyncStaminaS(stats);
	}

	public void Refresh()
	{
		InvalidateCache();
	}

	private void InitAfterLayout()
	{
		CacheBaseLayout();
		InvalidateCache();
	}

	private void ResolveBars()
	{
		_healthBar = GetNode<TextureProgressBar>("Health Bar");
		_healthStar = GetNode<TextureProgressBar>("Health Star");
		_corruptionBar = GetNode<TextureProgressBar>("Corruption Bar");
		_vesselBar = GetNode<TextureProgressBar>("Vessel Bar");
		_staminaBar = GetNode<TextureProgressBar>("Stamina Bar");
		_staminaStar = GetNode<TextureProgressBar>("Stamina Star");
	}

	private void CacheBaseLayout()
	{
		_healthBarBaseWidth = _healthBar.Size.X;
		_healthStarBasePos = _healthStar.Position;
		_vesselBarBaseWidth = _vesselBar.Size.X;
		_staminaBarBaseWidth = _staminaBar.Size.X;
		_staminaStarBasePos = _staminaStar.Position;

		_vesselBar.MaxValue = 100f;
		_corruptionBar.MaxValue = 100f;
		_healthStar.MaxValue = 100f;
		_staminaStar.MaxValue = 100f;
	}

	// Forces every Sync to redraw on the next tick regardless of change detection.
	private void InvalidateCache()
	{
		_lastHealth = _lastMaxHealth = -1f;
		_lastHealthUpgrades = -1;
		_lastCorruption = _lastVessel = _lastHealthS = -1f;
		_lastStamina = _lastMaxStamina = _lastStaminaS = -1f;
		_lastStaminaUpgrades = -1;
	}

	private void SyncHealth(PlayerStats stats)
	{
		float health = stats.GetCurrent(StatType.Health);
		float maxHealth = stats.GetCurrentMax(StatType.Health);
		int upgrades = stats.GetUpgradeLevels(StatType.Health);

		if (Mathf.IsEqualApprox(health, _lastHealth)
			&& Mathf.IsEqualApprox(maxHealth, _lastMaxHealth)
			&& upgrades == _lastHealthUpgrades)
			return;

		_lastHealth = health;
		_lastMaxHealth = maxHealth;
		_lastHealthUpgrades = upgrades;

		_healthBar.MaxValue = maxHealth;
		_healthBar.Value = health;

		float extraWidth = upgrades * WidthPerUpgrade;
		// Only the width is managed here; each bar keeps its authored height.
		_healthBar.Size = new Vector2(_healthBarBaseWidth + extraWidth, _healthBar.Size.Y);
		_healthStar.Position = _healthStarBasePos + new Vector2(extraWidth, 0f);

		// Vessel width scales with Health upgrades; its max value never changes.
		_vesselBar.Size = new Vector2(_vesselBarBaseWidth + extraWidth, _vesselBar.Size.Y);
	}

	private void SyncCorruption(PlayerStats stats)
	{
		float corruption = stats.GetCurrent(StatType.Corruption);
		if (Mathf.IsEqualApprox(corruption, _lastCorruption))
			return;

		_lastCorruption = corruption;
		_corruptionBar.Value = Mathf.Clamp(corruption, 0f, 100f);
	}

	private void SyncVessel(PlayerStats stats)
	{
		float vessel = stats.GetCurrent(StatType.Vessel);
		if (Mathf.IsEqualApprox(vessel, _lastVessel))
			return;

		_lastVessel = vessel;
		_vesselBar.Value = Mathf.Clamp(vessel, 0f, 100f);
	}

	private void SyncHealthS(PlayerStats stats)
	{
		float healthS = stats.GetCurrent(StatType.HealthS);
		if (Mathf.IsEqualApprox(healthS, _lastHealthS))
			return;

		_lastHealthS = healthS;
		_healthStar.Value = Mathf.Clamp(healthS, 0f, 100f);
	}

	private void SyncStamina(PlayerStats stats)
	{
		float stamina = stats.GetCurrent(StatType.Stamina);
		float maxStamina = stats.GetCurrentMax(StatType.Stamina);
		int upgrades = stats.GetUpgradeLevels(StatType.Stamina);

		if (Mathf.IsEqualApprox(stamina, _lastStamina)
			&& Mathf.IsEqualApprox(maxStamina, _lastMaxStamina)
			&& upgrades == _lastStaminaUpgrades)
			return;

		_lastStamina = stamina;
		_lastMaxStamina = maxStamina;
		_lastStaminaUpgrades = upgrades;

		_staminaBar.MaxValue = maxStamina;
		_staminaBar.Value = stamina;

		float extraWidth = upgrades * WidthPerUpgrade;
		_staminaBar.Size = new Vector2(_staminaBarBaseWidth + extraWidth, _staminaBar.Size.Y);
		_staminaStar.Position = _staminaStarBasePos + new Vector2(extraWidth, 0f);
	}

	private void SyncStaminaS(PlayerStats stats)
	{
		float staminaS = stats.GetCurrent(StatType.StaminaS);
		if (Mathf.IsEqualApprox(staminaS, _lastStaminaS))
			return;

		_lastStaminaS = staminaS;
		_staminaStar.Value = Mathf.Clamp(staminaS, 0f, 100f);
	}
}
