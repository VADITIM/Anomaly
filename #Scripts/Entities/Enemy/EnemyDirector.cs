using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Coordinates the pack around Kio. Only a few enemies may commit to an attack at once (tokens); the rest are given
// spread-out flank angles to hold, so a group surrounds and rotates in instead of queueing single file.
public sealed class EnemyDirector
{
    private const int MELEE_TOKENS = 2;
    private const int RANGED_TOKENS = 2;

    private readonly HashSet<Enemy> melee = new();
    private readonly HashSet<Enemy> ranged = new();
    private readonly List<Enemy> pack = new();

    public bool TryClaim(Enemy enemy)
    {
        HashSet<Enemy> pool = enemy.IsRanged ? ranged : melee;
        if (pool.Contains(enemy))
            return true;
        if (pool.Count >= (enemy.IsRanged ? RANGED_TOKENS : MELEE_TOKENS))
            return false;
        pool.Add(enemy);
        return true;
    }

    public bool CanEngage(Enemy enemy)
    {
        HashSet<Enemy> pool = enemy.IsRanged ? ranged : melee;
        return pool.Contains(enemy) || pool.Count < (enemy.IsRanged ? RANGED_TOKENS : MELEE_TOKENS);
    }

    public bool Holds(Enemy enemy) => melee.Contains(enemy) || ranged.Contains(enemy);

    public void Release(Enemy enemy)
    {
        melee.Remove(enemy);
        ranged.Remove(enemy);
    }

    public void Update(GameWorld world)
    {
        pack.Clear();
        Player player = world.Player;
        if (player == null || player.IsDead)
            return;
        foreach (Entity entity in world.Entities)
            if (entity is Enemy enemy && enemy.IsAggroed && !enemy.IsDead && enemy.IsCombatant)
                pack.Add(enemy);
        if (pack.Count == 0)
            return;

        Vector2 center = player.GlobalPosition;
        pack.Sort((a, b) => (a.GlobalPosition - center).Angle().CompareTo((b.GlobalPosition - center).Angle()));
        float origin = (pack[0].GlobalPosition - center).Angle();
        float step = Mathf.Tau / pack.Count;
        for (int i = 0; i < pack.Count; i++)
            pack[i].FlankAngle = origin + step * i;
    }
}
