using System;

namespace Anomaly;

public static class PlayerSignals
{
	public static event Action<float, float> HealthChanged;
	public static event Action<float, float> StaminaChanged;
	public static event Action<float, float, float> VesselChanged;
	public static event Action<float, float, bool> CorruptionChanged;
	public static event Action<int> SoulsChanged;
	public static event Action<SoulWeaponArc> ArcChanged;
	public static event Action<float> SpecialReadinessChanged;
	public static event Action<bool> AetheriacStateChanged;
	public static event Action HealReady;
	public static event Action Damaged;
	public static event Action Died;
	public static event Action Respawned;
	public static event Action<int, int, int, int> ResonanceChanged;
	public static event Action<int> ResonanceGained;

	public static void EmitHealthChanged(float current, float maximum) => HealthChanged?.Invoke(current, maximum);
	public static void EmitStaminaChanged(float current, float maximum) => StaminaChanged?.Invoke(current, maximum);
	public static void EmitVesselChanged(float current, float maximum, float healCharge) => VesselChanged?.Invoke(current, maximum, healCharge);
	public static void EmitCorruptionChanged(float current, float maximum, bool active) => CorruptionChanged?.Invoke(current, maximum, active);
	public static void EmitSoulsChanged(int souls) => SoulsChanged?.Invoke(souls);
	public static void EmitArcChanged(SoulWeaponArc arc) => ArcChanged?.Invoke(arc);
	public static void EmitSpecialReadinessChanged(float readiness) => SpecialReadinessChanged?.Invoke(readiness);
	public static void EmitAetheriacStateChanged(bool active) => AetheriacStateChanged?.Invoke(active);
	public static void EmitHealReady() => HealReady?.Invoke();
	public static void EmitDamaged() => Damaged?.Invoke();
	public static void EmitDied() => Died?.Invoke();
	public static void EmitRespawned() => Respawned?.Invoke();
	public static void EmitResonanceChanged(int resonance, int residue, int needed, int points) => ResonanceChanged?.Invoke(resonance, residue, needed, points);
	public static void EmitResonanceGained(int resonance) => ResonanceGained?.Invoke(resonance);
}

public static class EnemySignals
{
	public static event Action<Enemy> Killed;
	public static event Action<Enemy> TenacityShattered;
	public static event Action<Enemy> Aggroed;

	public static void EmitKilled(Enemy enemy) => Killed?.Invoke(enemy);
	public static void EmitTenacityShattered(Enemy enemy) => TenacityShattered?.Invoke(enemy);
	public static void EmitAggroed(Enemy enemy) => Aggroed?.Invoke(enemy);
}

public static class PropSignals
{
	public static event Action<Prop> Broken;

	public static void EmitBroken(Prop prop) => Broken?.Invoke(prop);
}

public static class WorldSignals
{
	public static event Action<string, string> RegionEntered;
	public static event Action<string> VoidHeartDestroyed;
	public static event Action<string> InteractionPromptChanged;
	public static event Action<bool> MenuFocusChanged;

	public static void EmitRegionEntered(string title, string subtitle) => RegionEntered?.Invoke(title, subtitle);
	public static void EmitVoidHeartDestroyed(string id) => VoidHeartDestroyed?.Invoke(id);
	public static void EmitInteractionPromptChanged(string prompt) => InteractionPromptChanged?.Invoke(prompt);
	// The forge menu clears the HUD for itself while it is open.
	public static void EmitMenuFocusChanged(bool focused) => MenuFocusChanged?.Invoke(focused);
}
