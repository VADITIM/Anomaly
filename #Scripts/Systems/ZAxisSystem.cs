using Godot;

public class ZAxisSystem
{
    public float JumpImpulse         { get; set; } = 300f;
    public float JumpFallSpeed       { get; set; } = 1200f;
    public float ShadowScaleWhenJump { get; set; } = 0.5f;

    public Sprite2D SpriteShadow { get; set; }
    public Node2D   WeaponNode   { get; set; }

    private Entity owner;
    private Sprite2D sprite;

    private float jumpTimer = 0f;
    private float jumpVelocity = 0f;
    private float jumpPosition = 0f;
    private Vector2 spriteBasePosition = Vector2.Zero;
    private Vector2 weaponNodeBasePosition = Vector2.Zero;
    private Vector2 shadowBaseScale = Vector2.One;

    public bool IsJumping => jumpTimer > 0f;

    public void Initialize(Entity owner)
    {
        this.owner = owner;
        sprite = owner.GetNodeOrNull<Sprite2D>("Sprite");

        if (sprite != null)
            spriteBasePosition = sprite.Position;
        if (WeaponNode != null)
            weaponNodeBasePosition = WeaponNode.Position;
        if (SpriteShadow != null)
            shadowBaseScale = SpriteShadow.Scale;
    }

    public void Jump()
    {
        if (jumpTimer > 0f)
            return;

        jumpVelocity = JumpImpulse;
        jumpTimer = 1f;
    }

    public void Update(float delta)
    {
        if (jumpTimer <= 0f)
            return;

        jumpVelocity -= JumpFallSpeed * delta;
        jumpPosition += jumpVelocity * delta;
        jumpPosition  = Mathf.Max(0f, jumpPosition);

        UpdateSpriteOffset(jumpPosition);

        float maxHeight   = (JumpImpulse * JumpImpulse) / (2f * JumpFallSpeed);
        float heightRatio = maxHeight > 0f ? jumpPosition / maxHeight : 0f;
        heightRatio       = Mathf.Clamp(heightRatio, 0f, 1f);
        UpdateShadowScale(heightRatio);

        if (jumpPosition <= 0f && jumpVelocity < 0f)
        {
            jumpTimer    = 0f;
            jumpVelocity = 0f;
            jumpPosition = 0f;
            UpdateSpriteOffset(0f);
            if (SpriteShadow != null)
                SpriteShadow.Scale = shadowBaseScale;
        }
    }

    private void UpdateSpriteOffset(float offset)
    {
        if (sprite != null)
            sprite.Position = spriteBasePosition + new Vector2(0, -offset);

        if (WeaponNode != null)
            WeaponNode.Position = weaponNodeBasePosition + new Vector2(0, -offset);
    }

    private void UpdateShadowScale(float heightRatio)
    {
        if (SpriteShadow == null)
            return;

        float shadowScale  = Mathf.Lerp(1f, ShadowScaleWhenJump, heightRatio);
        SpriteShadow.Scale = shadowBaseScale * shadowScale;
    }
}
