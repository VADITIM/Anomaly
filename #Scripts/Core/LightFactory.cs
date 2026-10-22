using System.Collections.Generic;
using Godot;

namespace Anomaly;

public static class LightFactory
{
    private static readonly Dictionary<int, GradientTexture2D> radialCache = new();

    // Scene lights add onto each other; the registry lets a carried light (the player's) give way to them instead of stacking.
    private static readonly HashSet<PointLight2D> sceneLights = new();
    private const float FALLOFF_KNEE = 0.45f;

    public static PointLight2D Point(Color color, float energy, float scale, Vector2 position, int size = 256)
    {
        var light = new PointLight2D
        {
            Texture = Radial(size),
            Color = color,
            Energy = energy,
            TextureScale = scale,
            Position = position,
        };
        light.TreeEntered += () => sceneLights.Add(light);
        light.TreeExiting += () => sceneLights.Remove(light);
        return light;
    }

    // Strongest energy any other scene light lands at this point, following the Radial texture's alpha falloff.
    public static float EnergyAt(Vector2 globalPosition, PointLight2D except)
    {
        float strongest = 0f;
        foreach (PointLight2D light in sceneLights)
        {
            if (light == except || !light.Enabled || !light.IsVisibleInTree())
                continue;
            float radius = light.Texture.GetWidth() * 0.5f * light.TextureScale * light.GlobalScale.X;
            float t = light.GlobalPosition.DistanceTo(globalPosition) / radius;
            if (t >= 1f)
                continue;
            float falloff = t < FALLOFF_KNEE ? Mathf.Lerp(1f, FALLOFF_KNEE, t / FALLOFF_KNEE) : Mathf.Lerp(FALLOFF_KNEE, 0f, (t - FALLOFF_KNEE) / (1f - FALLOFF_KNEE));
            strongest = Mathf.Max(strongest, light.Energy * falloff);
        }
        return strongest;
    }

    public static GradientTexture2D Radial(int size)
    {
        if (radialCache.TryGetValue(size, out GradientTexture2D cached))
            return cached;
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        gradient.AddPoint(0.45f, new Color(1f, 1f, 1f, 0.45f));
        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            Width = size,
            Height = size,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
        radialCache[size] = texture;
        return texture;
    }
}
