using Godot;

public partial class WeaponSwapAnimator : Node
{
    private Player player;
    private Sprite2D currentWeaponSprite;
    private TextureRect selectedInventoryItem;
    private Tween activeTween;
    private bool isSwapping = false;

    private const float TweenDuration = 0.6f;
    private const string TweenEaseType = "ease";

    public override void _Ready()
    {
        player = GetTree().Root.FindChild("Player", true, false) as Player;
        if (player != null && player.Weapon != null)
        {
            currentWeaponSprite = player.Weapon.WeaponSprite;
        }
    }

    public void AnimateWeaponSwap(TextureRect inventoryItemTextureRect, PackedScene weaponArcScene)
    {
        if (isSwapping || currentWeaponSprite == null || inventoryItemTextureRect == null)
            return;

        isSwapping = true;
        selectedInventoryItem = inventoryItemTextureRect;

        Vector2 originalPosition = currentWeaponSprite.GlobalPosition;
        Vector2 originalScale = currentWeaponSprite.Scale;
        Texture2D originalTexture = currentWeaponSprite.Texture;

        Vector2 targetPosition = inventoryItemTextureRect.GlobalPosition + inventoryItemTextureRect.Size / 2;
        Vector2 targetScale = Vector2.One * 0.5f;

        activeTween = CreateTween();
        activeTween.SetTrans(Tween.TransitionType.Quad);
        activeTween.SetEase(Tween.EaseType.InOut);
        activeTween.SetParallel(true);

        activeTween.TweenProperty(currentWeaponSprite, "global_position", targetPosition, TweenDuration);
        activeTween.TweenProperty(currentWeaponSprite, "scale", targetScale, TweenDuration);

        activeTween.Finished += () => OnTextureSwapped(originalPosition, originalScale, originalTexture, weaponArcScene);
    }

    private void OnTextureSwapped(Vector2 originalPosition, Vector2 originalScale, Texture2D originalTexture, PackedScene weaponArcScene)
    {
        Texture2D inventoryTexture = selectedInventoryItem.Texture;
        
        currentWeaponSprite.Texture = inventoryTexture;
        selectedInventoryItem.Texture = originalTexture;

        if (player?.GetNode("WeaponManager") is WeaponManager weaponManager)
        {
            weaponManager.SlotWeaponArcFromScene(weaponArcScene);
        }

        activeTween = CreateTween();
        activeTween.SetTrans(Tween.TransitionType.Quad);
        activeTween.SetEase(Tween.EaseType.InOut);
        activeTween.SetParallel(true);

        activeTween.TweenProperty(currentWeaponSprite, "global_position", originalPosition, TweenDuration);
        activeTween.TweenProperty(currentWeaponSprite, "scale", originalScale, TweenDuration);

        activeTween.Finished += () => OnSwapAnimationComplete();
    }

    private void OnSwapAnimationComplete()
    {
        isSwapping = false;
        selectedInventoryItem = null;
    }

    public override void _ExitTree()
    {
        activeTween?.Kill();
    }
}
