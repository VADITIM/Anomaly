using Godot;
using System;

public partial class ResourceSegment : Node2D
{
	[Export] public Player player;
	
	[Export] public int segmentWidth = 1;
	
	[Export] public Node2D HealthBarNode;
	[Export] public Texture2D HealthFullTexture;
	[Export] public Texture2D HealthEmptyTexture;
	[Export] public Sprite2D HealthSpecialSprite;

	[Export] public Node2D StaminaBarNode;
	[Export] public Texture2D StaminaFullTexture;
	[Export] public Texture2D StaminaEmptyTexture;
	[Export] public Sprite2D StaminaEndSprite;
	
	[Export] public Node2D XpBarNode;
	[Export] public Texture2D XpFullTexture;
	[Export] public Texture2D XpEmptyTexture;
	[Export] public Sprite2D XpEndSprite;

	private int _lastMaxHealth, _lastMaxStamina, _lastMaxXp, _lastHealth, _lastStamina, _lastXp = -1;

	private Vector2 _healthEndSpriteOriginalPos;
	private Vector2 _staminaEndSpriteOriginalPos;
	private Vector2 _xpEndSpriteOriginalPos;
	
	private BarDrawer _healthDrawer;
	private BarDrawer _staminaDrawer;
	private BarDrawer _xpDrawer;
	
	private ResourceManager _resourceManager;

	public override void _Ready()
	{
		if (player == null)
			player = GetTree().Root.FindChild("Player", true, false) as Player;
		
		if (player != null)
		{
			_resourceManager = player.ResourceManager;
		}
		
		if (HealthSpecialSprite != null)
			_healthEndSpriteOriginalPos = HealthSpecialSprite.Position;
		if (StaminaEndSprite != null)
			_staminaEndSpriteOriginalPos = StaminaEndSprite.Position;
		if (XpEndSprite != null)
			_xpEndSpriteOriginalPos = XpEndSprite.Position;
		
		if (HealthBarNode != null)
		{
			_healthDrawer = new BarDrawer();
			HealthBarNode.AddChild(_healthDrawer);
		}
		
		if (StaminaBarNode != null)
		{
			_staminaDrawer = new BarDrawer();
			StaminaBarNode.AddChild(_staminaDrawer);
		}
		
		if (XpBarNode != null)
		{
			_xpDrawer = new BarDrawer();
			XpBarNode.AddChild(_xpDrawer);
		}
	}

	public override void _Process(double delta)
	{
		if (_resourceManager == null) return;
		
		int maxHealth = (int)_resourceManager.MaxHealth;
		int maxStamina = (int)_resourceManager.MaxStamina;
		int health = (int)_resourceManager.Health;
		int stamina = (int)_resourceManager.Stamina;
		
		if (maxHealth != _lastMaxHealth || health != _lastHealth)
		{
			_lastMaxHealth = maxHealth;
			_lastHealth = health;
			
			if (HealthSpecialSprite != null)
				HealthSpecialSprite.Position = _healthEndSpriteOriginalPos + new Vector2(maxHealth * segmentWidth - 1, 0);
			
			if (_healthDrawer != null)
			{
				_healthDrawer.SetData(HealthFullTexture, HealthEmptyTexture, maxHealth, health, segmentWidth);
				_healthDrawer.QueueRedraw();
			}
		}
		
		if (maxStamina != _lastMaxStamina || stamina != _lastStamina)
		{
			_lastMaxStamina = maxStamina;
			_lastStamina = stamina;
			
			if (StaminaEndSprite != null)
				StaminaEndSprite.Position = _staminaEndSpriteOriginalPos + new Vector2(maxStamina * segmentWidth - 1, 0);
			
			if (_staminaDrawer != null)
			{
				_staminaDrawer.SetData(StaminaFullTexture, StaminaEmptyTexture, maxStamina, stamina, segmentWidth);
				_staminaDrawer.QueueRedraw();
			}
		}
		
		int maxXp = (int)_resourceManager.MaxXp;
		int xp = (int)_resourceManager.Xp;
		
		if (maxXp != _lastMaxXp || xp != _lastXp)
		{
			_lastMaxXp = maxXp;
			_lastXp = xp;
			
			if (XpEndSprite != null)
				XpEndSprite.Position = _xpEndSpriteOriginalPos + new Vector2(maxXp * segmentWidth - 1, 0);
			
			if (_xpDrawer != null)
			{
				_xpDrawer.SetData(XpFullTexture, XpEmptyTexture, maxXp, xp, segmentWidth);
				_xpDrawer.QueueRedraw();
			}
		}
	}
}

public partial class BarDrawer : Node2D
{
	private Texture2D _fullTexture;
	private Texture2D _emptyTexture;
	private int _max;
	private int _current;
	private int _segmentWidth;

	public void SetData(Texture2D fullTexture, Texture2D emptyTexture, int max, int current, int segmentWidth)
	{
		_fullTexture = fullTexture;
		_emptyTexture = emptyTexture;
		_max = max;
		_current = current;
		_segmentWidth = segmentWidth;
	}

	public override void _Draw()
	{
		if (_fullTexture == null || _emptyTexture == null) return;
		
		for (int i = 0; i < _max; i++)
		{
			var texture = i < _current ? _fullTexture : _emptyTexture;
			// Middle-left pivot: x starts at left edge, y centered
			var pos = new Vector2(i * _segmentWidth, -texture.GetHeight() * 0.5f);
			DrawTexture(texture, pos);
		}
	}
}
