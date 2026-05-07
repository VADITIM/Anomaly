using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class WeaponMenu : Control
{
    public Player Player;
    [Export] public WeaponManager WeaponManager;
    [Export] public WeaponInventory WeaponInventory;
    [Export] public GridContainer WeaponGrid;
    [Export] public PackedScene WeaponButtonScene;

    private HBoxContainer _navBar;
    private Button _prevButton;
    private Button _nextButton;
    private Label _pageLabel;

    private int _currentPage = 0;

    private const int ItemsPerRow = 5;
    private const int RowsPerPage = 5;
    private const int PageSize = ItemsPerRow * RowsPerPage;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Pass;
        ProcessMode = ProcessModeEnum.Always;

        Player ??= GetTree().Root.FindChild("Player", true, false) as Player;
        WeaponManager ??= Player?.GetNodeOrNull<WeaponManager>("WeaponManager");
        WeaponInventory ??= Player?.GetNodeOrNull<WeaponInventory>("WeaponInventory");

        BuildUi();
        
        if (WeaponGrid != null)
        {
            WeaponGrid.Columns = ItemsPerRow;
            WeaponGrid.AddThemeConstantOverride("h_separation", 200);
            WeaponGrid.AddThemeConstantOverride("v_separation", 200);
            WeaponGrid.MouseFilter = MouseFilterEnum.Pass;
        }

        if (WeaponInventory != null)
            WeaponInventory.InventoryChanged += RefreshGrid;

        RefreshGrid();
    }

    private void BuildUi()
    {
        _navBar = new HBoxContainer();
        _navBar.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_navBar);

        _prevButton = new Button { Text = "<" };
        _prevButton.Pressed += () => ChangePage(-1);
        _navBar.AddChild(_prevButton);

        _pageLabel = new Label();
        _pageLabel.MouseFilter = MouseFilterEnum.Ignore;
        _navBar.AddChild(_pageLabel);

        _nextButton = new Button { Text = ">" };
        _nextButton.Pressed += () => ChangePage(1);
        _navBar.AddChild(_nextButton);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.IsEcho() && key.Keycode == Key.T)
        {
            Visible = !Visible;
        }
    }

    private void ChangePage(int delta)
    {
        int pageCount = GetPageCount();
        if (pageCount == 0)
            return;

        _currentPage = Mathf.Clamp(_currentPage + delta, 0, pageCount - 1);
        RefreshGrid();
    }

    private int GetPageCount()
    {
        if (WeaponInventory == null || WeaponInventory.WeaponScenes.Count == 0)
            return 0;
        return Mathf.Max(1, WeaponInventory.GetPageCount(PageSize));
    }

    private void RefreshGrid()
    {
        if (WeaponGrid == null) return;
        
        foreach (Node child in WeaponGrid.GetChildren())
            child.QueueFree();

        if (WeaponInventory == null || WeaponButtonScene == null)
        {
            _pageLabel.Text = WeaponInventory == null ? "No inventory" : "No button scene";
            return;
        }

        int pageCount = GetPageCount();
        _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, pageCount - 1));

        IEnumerable<(PackedScene scene, int index)> pageItems = WeaponInventory.GetPageItems(_currentPage, PageSize);
        List<(PackedScene scene, int index)> items = pageItems.ToList();

        foreach ((PackedScene scene, int index) in items)
        {
            string displayName = GetDisplayName(scene);
            bool isEquipped = WeaponManager?.IsSceneEquipped(scene) ?? false;

            Control weaponItemControl = WeaponButtonScene.Instantiate<Control>();
            Button button = weaponItemControl.GetNode<Button>("Button");
            TextureRect textureRect = weaponItemControl.GetNode<TextureRect>("Button/TextureRect");
            
            if (button == null || textureRect == null)
            {
                GD.PrintErr("WeaponButtonScene must contain Button and Button/TextureRect nodes");
                weaponItemControl.QueueFree();
                continue;
            }

            button.Disabled = isEquipped;
            button.MouseFilter = MouseFilterEnum.Stop;
            button.FocusMode = FocusModeEnum.All;
            
            Texture2D weaponTexture = GetWeaponTexture(scene);
            if (weaponTexture != null)
            {
                textureRect.Texture = weaponTexture;
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
                textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                textureRect.CustomMinimumSize = new Vector2(150, 150);
            }

            if (isEquipped)
            {
                weaponItemControl.Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            int slotIndex = index;
            PackedScene weaponScene = scene;
            
            button.Pressed += () => {
                GD.Print($"Button pressed: {displayName}");
                HandleEquipLeft(weaponScene, slotIndex);
            };
            button.GuiInput += (InputEvent evt) => {
                if (evt is InputEventMouseButton mouse && mouse.Pressed && !mouse.IsEcho() && mouse.ButtonIndex == MouseButton.Right)
                {
                    GD.Print($"Right click on: {displayName}");
                    HandleEquipRight(weaponScene, slotIndex);
                    GetViewport().SetInputAsHandled();
                }
            };
            
            WeaponGrid.AddChild(weaponItemControl);
        }

        _pageLabel.Text = pageCount <= 0 ? "0/0" : $"{_currentPage + 1}/{pageCount}";
    }

    private Texture2D GetWeaponTexture(PackedScene weaponScene)
    {
        if (weaponScene == null) return null;
        
        var tempWeapon = weaponScene.Instantiate();
        Texture2D texture = null;
        
        var sprite = tempWeapon.FindChild("*", true, false) as Sprite2D;
        if (sprite != null && sprite.Texture != null)
        {
            texture = sprite.Texture;
        }
        else
        {
            var animatedSprite = tempWeapon.FindChild("*", true, false) as AnimatedSprite2D;
            if (animatedSprite != null && animatedSprite.SpriteFrames != null)
            {
                var frames = animatedSprite.SpriteFrames;
                if (frames.HasAnimation("default") && frames.GetFrameCount("default") > 0)
                {
                    texture = frames.GetFrameTexture("default", 0);
                }
            }
        }
        
        tempWeapon.QueueFree();
        return texture;
    }

    private void HandleEquipLeft(PackedScene weaponScene, int index)
    {
        GD.Print($"HandleEquipLeft called for weapon at index {index}");
        if (WeaponManager == null || weaponScene == null)
        {
            GD.Print("WeaponManager or weaponScene is null!");
            return;
        }

        int currentSlot = WeaponManager.GetCurrentSlotIndex();
        WeaponManager.EquipWeaponToSlot(weaponScene, currentSlot);
        RefreshGrid();
    }

    private void HandleEquipRight(PackedScene weaponScene, int index)
    {
        GD.Print($"HandleEquipRight called for weapon at index {index}");
        if (WeaponManager == null || weaponScene == null)
        {
            GD.Print("WeaponManager or weaponScene is null!");
            return;
        }

        int currentSlot = WeaponManager.GetCurrentSlotIndex();
        int otherSlot = currentSlot == 0 ? 1 : 0;
        WeaponManager.EquipWeaponToSlot(weaponScene, otherSlot);
        RefreshGrid();
    }

    private static string GetDisplayName(PackedScene scene)
    {
        if (scene == null)
            return "Unknown";

        string path = scene.ResourcePath;
        if (string.IsNullOrEmpty(path))
            return scene.ResourceName;

        string[] parts = path.Split('/')
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        string fileName = parts.Length > 0 ? parts[^1] : scene.ResourceName;
        return fileName.Replace(".tscn", string.Empty);
    }
}
