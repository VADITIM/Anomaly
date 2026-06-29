using Godot;

public class CommonDamageFlash : IEntityBehavior
{
    private const string DAMAGE_FLASH_SHADER_PATH = "res://#Shaders/damageflash.gdshader";

    public float FlashDuration { get; set; } = 0.5f;
    public Color FlashColor    { get; set; } = Colors.White;

    private Entity owner;
    private Sprite2D sprite;
    private ShaderMaterial flashMaterial;
    private Tween flashTween;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
        EnsureFlashMaterial();
    }

    public void OnProcess(double delta) { }
    public void OnPhysicsProcess(double delta) { }
    public void OnExitTree() { }

    public void Flash()
    {
        EnsureFlashMaterial();
        if (flashMaterial == null)
            return;

        flashTween?.Kill();
        flashMaterial.SetShaderParameter("flash_color", FlashColor);
        flashMaterial.SetShaderParameter("flash_value", 1f);

        flashTween = owner.CreateTween();
        flashTween.TweenProperty(flashMaterial, "shader_parameter/flash_value", 0f, Mathf.Max(0.01f, FlashDuration));
    }

    private void EnsureFlashMaterial()
    {
        if (flashMaterial != null)
            return;

        sprite = owner.GetNodeOrNull<Sprite2D>("Sprite");
        if (sprite == null)
            return;

        Shader shader = GD.Load<Shader>(DAMAGE_FLASH_SHADER_PATH);
        if (shader == null)
            return;

        if (sprite.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == shader)
        {
            flashMaterial = existingMaterial;
        }
        else
        {
            flashMaterial = new ShaderMaterial { Shader = shader };
            sprite.Material = flashMaterial;
        }

        flashMaterial.SetShaderParameter("flash_color", FlashColor);
        flashMaterial.SetShaderParameter("flash_value", 0f);
    }
}
