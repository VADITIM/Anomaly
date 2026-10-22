using Godot;

namespace Anomaly;

// F1 god mode, debug builds only. Kio cannot be hurt or die, hovers through walls and over the Void, never runs out of
// stamina, specials have no cooldown, and souls never run out. Souls picked up meanwhile are banked; turning it off
// hands back the real count, so the debug hoard never reaches a save.
public partial class Player
{
    private const int GOD_SOULS = 999999;
    private const float GOD_SPEED_SCALE = 1.6f;
    private const float GOD_HOVER = 8f;

    public bool GodMode { get; private set; }
    // The souls Kio really holds: what saves record, whatever god mode shows.
    public int BankedSouls => GodMode ? bankedSouls : Souls;

    private int bankedSouls;

    public void ToggleGodMode()
    {
        if (IsDead)
            return;
        GodMode = !GodMode;
        NoClip = GodMode;
        IgnorePits = GodMode;
        if (GodMode)
        {
            bankedSouls = Souls;
            Souls = GOD_SOULS;
            Health = MaxHealth;
            Stamina = MaxStamina;
            specialCooldownTimer = 0f;
        }
        else
        {
            Souls = bankedSouls;
            BodyOffset = Vector2.Zero;
            SaveManager.Progression.HeldSouls = Souls;
        }
        BroadcastAll();
        Sfx.Play("ui_confirm", -4f, GodMode ? 1.3f : 0.7f);
        World.Hud.Toast(GodMode ? "GOD MODE  (F1 to leave)" : "God mode off.");
    }

    private void UpdateGodMode()
    {
        if (!GodMode)
            return;
        Health = MaxHealth;
        Stamina = MaxStamina;
        BodyOffset = new Vector2(0f, -GOD_HOVER - 2f * Mathf.Sin(animationTime * 3f));
    }
}
