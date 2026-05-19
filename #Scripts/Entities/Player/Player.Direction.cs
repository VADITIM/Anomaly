using Godot;

public partial class Player
{
    public enum MovementDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    private string GetDirectionFromAngle(float angleDegrees, out bool flipH)
    {
        while (angleDegrees > 180) angleDegrees -= 360;
        while (angleDegrees < -180) angleDegrees += 360;

        flipH = false;

        if (angleDegrees >= -22.5f && angleDegrees < 22.5f)
            return "E";
        if (angleDegrees >= 22.5f && angleDegrees < 67.5f)
            return "SE";
        if (angleDegrees >= 67.5f && angleDegrees < 112.5f)
            return "S";
        if (angleDegrees >= 112.5f && angleDegrees < 157.5f)
            return "SW";
        if (angleDegrees >= 157.5f || angleDegrees < -157.5f)
            return "W";
        if (angleDegrees >= -157.5f && angleDegrees < -112.5f)
            return "NW";
        if (angleDegrees >= -112.5f && angleDegrees < -67.5f)
            return "N";
        
        return "NE";
    }
    
    private string GetDirectionFromVector(Vector2 direction, out bool flipH)
    {
        flipH = false;
        if (direction == Vector2.Zero)
            return "S";
        
        float angle = Mathf.RadToDeg(direction.Angle());
        return GetDirectionFromAngle(angle, out flipH);
    }
    
    protected override void ApplyFacing(bool flipH)
    {
        Sprite.FlipH = flipH;
    }
}