using System.Collections.Generic;
using Godot;

namespace Anomaly;

public partial class VoidFragment : Node2D
{
    private static readonly Color DeepVoid = new(0.06f, 0.0f, 0.12f);
    private static readonly Color VoidUnderglow = new(0.42f, 0.1f, 0.48f);
    private static readonly Rect2 GroundRegion = new(0, 0, 128, 64);
    private static readonly Rect2 FaceRegion = new(32, 192, 128, 64);

    public float Depth { get; private set; }

    private Vector2 anchor;
    private float phase;
    private float bobSpeed;
    private float driftRadius;
    private Vector2[] top;
    private Vector2[] face;
    private SpriteRegion? cargo;

    public void Setup(RandomNumberGenerator random, Vector2 anchorPosition)
    {
        anchor = anchorPosition;
        Depth = random.RandfRange(0.12f, 0.6f);
        phase = random.RandfRange(0f, Mathf.Tau);
        bobSpeed = random.RandfRange(0.35f, 0.7f);
        driftRadius = random.RandfRange(2f, 6f);

        float width = random.RandfRange(30f, 52f) * (random.Randf() < 0.3f ? 1.8f : 1f);
        float height = width * random.RandfRange(0.4f, 0.6f);
        top = BuildTop(random, width, height);
        face = BuildFace(random, top, width);
        if (random.Randf() < 0.55f)
            cargo = PickCargo(random);

        float scale = 1f - Depth * 0.45f;
        Scale = new Vector2(scale, scale);
        Modulate = Colors.White.Lerp(DeepVoid, Mathf.Clamp(Depth * 0.8f, 0f, 0.5f));
        Rotation = random.RandfRange(-0.06f, 0.06f);
    }

    private static SpriteRegion PickCargo(RandomNumberGenerator random)
    {
        float roll = random.Randf();
        if (roll < 0.4f)
            return Art.Rubble[random.RandiRange(0, Art.Rubble.Length - 1)];
        if (roll < 0.7f)
            return Art.DeadShrubs[random.RandiRange(0, Art.DeadShrubs.Length - 1)];
        return Art.Relics[random.RandiRange(0, Art.Relics.Length - 1)];
    }

    private static Vector2[] BuildTop(RandomNumberGenerator random, float width, float height)
    {
        const int points = 10;
        var outline = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float angle = Mathf.Tau * i / points + random.RandfRange(-0.18f, 0.18f);
            float reach = random.RandfRange(0.72f, 1f);
            outline[i] = new Vector2(Mathf.Cos(angle) * width * 0.5f * reach, Mathf.Sin(angle) * height * 0.5f * reach).Round();
        }
        return outline;
    }

    // The underside hangs straight down from the southern half of the outline, deepest at the centre, so the
    // lower chain always stays below the upper one and the polygon never folds over itself.
    private static Vector2[] BuildFace(RandomNumberGenerator random, Vector2[] outline, float width)
    {
        var rim = new List<Vector2>();
        foreach (Vector2 point in outline)
            if (point.Y >= -0.5f && !rim.Exists(existing => Mathf.IsEqualApprox(existing.X, point.X)))
                rim.Add(point);
        rim.Sort((a, b) => a.X.CompareTo(b.X));
        float depth = width * random.RandfRange(0.55f, 1.1f);
        var polygon = new List<Vector2>(rim);
        for (int i = rim.Count - 1; i >= 0; i--)
        {
            float edge = Mathf.Abs(rim[i].X) / (width * 0.5f);
            float hang = Mathf.Max(3f, depth * (1f - edge * 0.85f) * random.RandfRange(0.45f, 1f));
            polygon.Add((rim[i] + new Vector2(0, hang)).Round());
        }
        return polygon.ToArray();
    }

    public void Follow(Vector2 cameraCenter, float time)
    {
        Vector2 parallax = (cameraCenter - anchor) * Depth * 0.25f;
        Vector2 drift = new(Mathf.Sin(time * bobSpeed * 0.4f + phase) * driftRadius, Mathf.Sin(time * bobSpeed + phase) * 2.5f);
        Position = (anchor + parallax + drift).Round();
    }

    public override void _Draw()
    {
        Texture2D wall = Art.Texture(Art.WALL);
        Texture2D ground = Art.Texture(Art.GRASS);
        float faceBottom = 0f;
        foreach (Vector2 point in face)
            faceBottom = Mathf.Max(faceBottom, point.Y);

        float faceSquash = Mathf.Min(1f, FaceRegion.Size.Y / Mathf.Max(1f, faceBottom));
        var faceColors = new Color[face.Length];
        var faceUvs = new Vector2[face.Length];
        for (int i = 0; i < face.Length; i++)
        {
            float fall = Mathf.Clamp(face[i].Y / Mathf.Max(1f, faceBottom), 0f, 1f);
            faceColors[i] = new Color(0.7f, 0.66f, 0.78f).Lerp(VoidUnderglow, fall * 0.85f);
            faceUvs[i] = RegionUv(wall, FaceRegion, new Vector2(face[i].X + 64f, face[i].Y * faceSquash));
        }
        DrawPolygon(face, faceColors, faceUvs, wall);

        var topColors = new Color[top.Length];
        var topUvs = new Vector2[top.Length];
        Color groundTint = TerrainPainter.GrassTint.Darkened(0.15f);
        for (int i = 0; i < top.Length; i++)
        {
            topColors[i] = groundTint;
            topUvs[i] = RegionUv(ground, GroundRegion, top[i] + GroundRegion.Size * 0.5f);
        }
        DrawPolygon(top, topColors, topUvs, ground);
        DrawPolyline(Close(top), new Color(0.04f, 0.0f, 0.08f, 0.7f), 1f);

        if (cargo is SpriteRegion region)
            DrawTextureRectRegion(Art.Texture(region.TexturePath), new Rect2(-region.Origin, region.Region.Size), region.Region, RuinScatter.StoneTint);
    }

    private static Vector2[] Close(Vector2[] outline)
    {
        var closed = new Vector2[outline.Length + 1];
        outline.CopyTo(closed, 0);
        closed[outline.Length] = outline[0];
        return closed;
    }

    private static Vector2 RegionUv(Texture2D texture, Rect2 region, Vector2 local)
    {
        Vector2 clamped = local.Clamp(Vector2.Zero, region.Size);
        return (region.Position + clamped) / texture.GetSize();
    }
}
