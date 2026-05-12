using Godot;

/// <summary>
/// Handles tween-based weapon swapping with sprite animation.
/// When a weapon is clicked in the inventory, the current weapon sprite tweens to the
/// inventory item's TextureRect, exchanges textures, and tweens back.
/// </summary>
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

    /// <summary>
    /// Initiates a weapon swap animation from the current weapon to the selected inventory item.
    /// </summary>
    public void AnimateWeaponSwap(TextureRect inventoryItemTextureRect, PackedScene weaponArcScene)
    {
        if (isSwapping || currentWeaponSprite == null || inventoryItemTextureRect == null)
            return;

        isSwapping = true;
        selectedInventoryItem = inventoryItemTextureRect;

        // Store original properties
        Vector2 originalPosition = currentWeaponSprite.GlobalPosition;
        Vector2 originalScale = currentWeaponSprite.Scale;
        Texture2D originalTexture = currentWeaponSprite.Texture;

        // Get inventory item's global position and size
        Vector2 targetPosition = inventoryItemTextureRect.GlobalPosition + inventoryItemTextureRect.Size / 2;
        Vector2 targetScale = Vector2.One * 0.5f; // Shrink to inventory size

        // Create tween for movement and scaling
        activeTween = CreateTween();
        activeTween.SetTrans(Tween.TransitionType.Quad);
        activeTween.SetEase(Tween.EaseType.InOut);
        activeTween.SetParallel(true); // Run movement and scale simultaneously

        // Tween sprite to inventory item position
        activeTween.TweenProperty(currentWeaponSprite, "global_position", targetPosition, TweenDuration);
        activeTween.TweenProperty(currentWeaponSprite, "scale", targetScale, TweenDuration);

        // After reaching inventory, swap textures
        activeTween.Finished += () => OnTextureSwapped(originalPosition, originalScale, originalTexture, weaponArcScene);
    }

    private void OnTextureSwapped(Vector2 originalPosition, Vector2 originalScale, Texture2D originalTexture, PackedScene weaponArcScene)
    {
        // Swap textures between current weapon sprite and inventory item
        Texture2D inventoryTexture = selectedInventoryItem.Texture;
        
        currentWeaponSprite.Texture = inventoryTexture;
        selectedInventoryItem.Texture = originalTexture;

        // Equip the new weapon arc
        if (player?.GetNode("WeaponManager") is WeaponManager weaponManager)
        {
            weaponManager.SlotWeaponArcFromScene(weaponArcScene);
        }

        // Tween sprite back to original position with new texture
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
        // Kill any active tweens when the node is freed
        activeTween?.Kill();
    }
}
