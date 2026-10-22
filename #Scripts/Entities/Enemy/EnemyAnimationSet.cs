using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Enemy sheets ship as D_/S_/U_ strips of six 48px frames; side strips face left and are mirrored for the right.
public sealed class EnemyAnimationSet
{
    public const int FRAME_COUNT = 6;
    private static readonly Dictionary<string, EnemyAnimationSet> cache = new();
    private static readonly string[] DIRECTIONS = { "D", "S", "U" };
    // Strips per animation, indexed like DIRECTIONS. Looked up every frame, so no key is built per call.
    private readonly Dictionary<string, Texture2D[]> strips = new();

    public static EnemyAnimationSet Load(string folder)
    {
        if (cache.TryGetValue(folder, out EnemyAnimationSet set))
            return set;
        set = new EnemyAnimationSet();
        foreach (string animation in new[] { "Walk", "Walk2", "Attack", "Special", "Death", "Death2" })
        {
            var strip = new Texture2D[DIRECTIONS.Length];
            for (int i = 0; i < DIRECTIONS.Length; i++)
            {
                string path = $"res://#Assets/Entities/Enemies/{folder}/{DIRECTIONS[i]}_{animation}.png";
                if (ResourceLoader.Exists(path))
                    strip[i] = GD.Load<Texture2D>(path);
            }
            if (strip[0] != null)
                set.strips[animation] = strip;
        }
        cache[folder] = set;
        return set;
    }

    public bool Has(string animation) => strips.ContainsKey(animation);

    public Texture2D Resolve(string animation, Vector2 facing, out bool flip)
    {
        int direction;
        flip = false;
        if (Mathf.Abs(facing.X) > Mathf.Abs(facing.Y) * 0.9f)
        {
            direction = 1;
            flip = facing.X > 0f;
        }
        else
        {
            direction = facing.Y >= 0f ? 0 : 2;
        }
        if (!strips.TryGetValue(animation, out Texture2D[] strip))
            strip = strips["Walk"];
        return strip[direction];
    }
}
