using Godot;

public enum DamageNumberStyle { Standard, Staggered, Weakness }

public static class DamageNumberSpawner
{
    private static readonly Color StandardColor = Colors.White;
    private static readonly Color StaggeredColor = new Color(1f, 0.89f, 0.25f);
    private static readonly Color WeaknessColor = new Color(1f, 0.25f, 0.25f);

    public static void Spawn(Entity entity, float damageAmount, DamageNumberStyle style)
    {
        if (entity == null)
            return;

        Node sceneRoot = entity.GetTree()?.CurrentScene ?? entity.GetTree()?.Root;
        if (sceneRoot == null)
            return;

        DamageNumber damageNumber = new DamageNumber();
        sceneRoot.AddChild(damageNumber);
        damageNumber.Begin(
            CreateDamageNumberText(damageAmount, style),
            GetDamageNumberColor(style),
            GetDamageNumberSpawnPosition(entity),
            style == DamageNumberStyle.Weakness
        );
    }

    private static Vector2 GetDamageNumberSpawnPosition(Entity entity)
    {
        CollisionShape2D hurtbox = entity.FindChild("Hurtbox", true, false) as CollisionShape2D;
        if (hurtbox == null || hurtbox.Shape == null)
            return entity.GlobalPosition;

        Vector2 localPoint = SamplePointInsideShape(hurtbox.Shape);
        return hurtbox.ToGlobal(localPoint);
    }

    private static Vector2 SamplePointInsideShape(Shape2D shape)
    {
        if (shape is RectangleShape2D rectangle)
        {
            return new Vector2(
                RandomRange(-rectangle.Size.X * 0.5f, rectangle.Size.X * 0.5f),
                RandomRange(-rectangle.Size.Y * 0.5f, rectangle.Size.Y * 0.5f)
            );
        }

        if (shape is CircleShape2D circle)
        {
            float angle = RandomRange(0f, Mathf.Tau);
            float radius = circle.Radius * Mathf.Sqrt(GD.Randf());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        if (shape is CapsuleShape2D capsule)
        {
            float radius = capsule.Radius;
            float halfBodyHeight = capsule.Height * 0.5f;
            float totalHalfHeight = halfBodyHeight + radius;

            for (int i = 0; i < 8; i++)
            {
                Vector2 candidate = new Vector2(
                    RandomRange(-radius, radius),
                    RandomRange(-totalHalfHeight, totalHalfHeight)
                );

                if (Mathf.Abs(candidate.Y) <= halfBodyHeight)
                    return candidate;

                Vector2 capCenter = new Vector2(0f, candidate.Y > 0f ? halfBodyHeight : -halfBodyHeight);
                if (candidate.DistanceSquaredTo(capCenter) <= radius * radius)
                    return candidate;
            }

            return Vector2.Zero;
        }

        return Vector2.Zero;
    }

    private static float RandomRange(float min, float max)
    {
        return min + (GD.Randf() * (max - min));
    }

    private static string CreateDamageNumberText(float damageAmount, DamageNumberStyle style)
    {
        int value = Mathf.RoundToInt(damageAmount);
        return style == DamageNumberStyle.Weakness ? $"{value}!!" : value.ToString();
    }

    private static Color GetDamageNumberColor(DamageNumberStyle style)
    {
        return style switch
        {
            DamageNumberStyle.Staggered => StaggeredColor,
            DamageNumberStyle.Weakness => WeaknessColor,
            _ => StandardColor,
        };
    }
}
