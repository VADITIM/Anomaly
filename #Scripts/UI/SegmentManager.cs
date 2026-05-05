using Godot;
using System.Collections.Generic;

public partial class SegmentManager : Control
{
	[Export] public Player Player;
	private readonly Dictionary<BarCases, BarConfig> _bars = new();
	
	[Export] public Control HealthSegmentsContainer;
	[Export] public PackedScene HealthFullSegmentScene;
	[Export] public PackedScene HealthEmptySegmentScene;
	[Export] public TextureRect HealthAbilityTexture;
	private Vector2 _healthEndSpriteOriginalPos;
	private List<Node> _healthSegments = new List<Node>();

	[Export] public Control StaminaSegmentsContainer;
	[Export] public PackedScene StaminaFullSegmentScene;
	[Export] public PackedScene StaminaEmptySegmentScene;
	[Export] public PackedScene StaminaDodgeSegmentScene; // Special segment for dodge cost markers
	[Export] public TextureRect StaminaEndTexture;
	private Vector2 _staminaEndSpriteOriginalPos;
	private List<Node> _staminaSegments = new List<Node>();
	private List<Node> _staminaSpecialSegments = new List<Node>(); // Track special dodge cost markers
	
	[Export] public Control VesselSegmentsContainer;
	[Export] public PackedScene VesselFullSegmentScene;
	[Export] public PackedScene VesselEmptySegmentScene;
	[Export] public TextureRect VesselEndTexture;
	private Vector2 _xpEndSpriteOriginalPos;
	private List<Node> _vesselSegments = new List<Node>();
	
	private int _lastMaxHealth = -1;
	private int _lastMaxStamina = -1;
	private int _lastMaxXp = -1;
	private int _lastHealth = -1;
	private int _lastStamina = -1;
	private int _lastXp = -1;
	

	private class BarConfig
	{
		public Control BarNode;
		public PackedScene FullScene;
		public PackedScene EmptyScene;
		public TextureRect EndSprite;
		public Vector2 OriginalEndPos;
		public List<Node> Segments;
		public string Name;
		public int LastMax = -1;
		public int LastValue = -1;
	}

	private void UpdateBar(BarCases bar, int maxValue, int currentValue)
	{
		if (!_bars.TryGetValue(bar, out var cfg) || cfg.BarNode == null)
			return;

		if (maxValue != cfg.LastMax)
		{
			cfg.LastMax = maxValue;
			RebuildSegments(cfg.BarNode, cfg.Segments, maxValue, cfg.FullScene, cfg.EmptyScene, cfg.Name);
			UpdateEndSpritePosition(cfg.EndSprite, cfg.OriginalEndPos, maxValue, cfg.FullScene);
			switch (bar)
			{
				case BarCases.Health: _lastMaxHealth = maxValue; break;
				case BarCases.Stamina: _lastMaxStamina = maxValue; break;
				case BarCases.XP: _lastMaxXp = maxValue; break;
			}
		}

		if (currentValue != cfg.LastValue)
		{
			cfg.LastValue = currentValue;
			UpdateSegmentStates(cfg.Segments, currentValue, cfg.FullScene, cfg.EmptyScene);
			switch (bar)
			{
				case BarCases.Health: _lastHealth = currentValue; break;
				case BarCases.Stamina: _lastStamina = currentValue; break;
				case BarCases.XP: _lastXp = currentValue; break;
			}
		}
	}
	
	public enum BarCases
	{
		Health,
		Stamina,
		XP
	}

	private void InstantiateBarCases()
	{
		_bars[BarCases.Health] = new BarConfig
		{
			BarNode = HealthSegmentsContainer,
			FullScene = HealthFullSegmentScene,
			EmptyScene = HealthEmptySegmentScene,
			EndSprite = HealthAbilityTexture,
			OriginalEndPos = _healthEndSpriteOriginalPos,
			Segments = _healthSegments,
			Name = "Health",
			LastMax = _lastMaxHealth,
			LastValue = _lastHealth
		};
		
		_bars[BarCases.Stamina] = new BarConfig
		{
			BarNode = StaminaSegmentsContainer,
			FullScene = StaminaFullSegmentScene,
			EmptyScene = StaminaEmptySegmentScene,
			EndSprite = StaminaEndTexture,
			OriginalEndPos = _staminaEndSpriteOriginalPos,
			Segments = _staminaSegments,
			Name = "Stamina",
			LastMax = _lastMaxStamina,
			LastValue = _lastStamina
		};

		_bars[BarCases.XP] = new BarConfig
		{
			BarNode = VesselSegmentsContainer,
			FullScene = VesselFullSegmentScene,
			EmptyScene = VesselEmptySegmentScene,
			EndSprite = VesselEndTexture,
			OriginalEndPos = _xpEndSpriteOriginalPos,
			Segments = _vesselSegments,
			Name = "XP",
			LastMax = _lastMaxXp,
			LastValue = _lastXp
		};
	}

	public override void _Ready()
	{
		if (Player == null)
			Player = GetTree().Root.FindChild("Player", true, false) as Player;
		
			_healthEndSpriteOriginalPos = HealthAbilityTexture.Position;
			_staminaEndSpriteOriginalPos = StaminaEndTexture.Position;
			_xpEndSpriteOriginalPos = VesselEndTexture.Position;
			InstantiateBarCases();
	}


	public override void _Process(double delta)
	{
            UpdateBar(BarCases.Health, (int)Player.Stats.GetCurrentMax("Health"), (int)Player.Stats.GetCurrent("Health"));
            UpdateBar(BarCases.Stamina, (int)Player.Stats.GetCurrentMax("Stamina"), (int)Player.Stats.GetCurrent("Stamina"));
            UpdateStaminaSpecialSegments();
            UpdateBar(BarCases.XP, (int)Player.Stats.GetCurrentMax("Vessel"), (int)Player.Stats.GetCurrent("Vessel"));
	}

	private void RebuildSegments(Control barNode, List<Node> segments, int maxCount, PackedScene fullSegmentScene, PackedScene emptySegmentScene, string barName)
	{
		if (maxCount <= 0) return;
		
		foreach (var segment in segments)
		{
			segment.QueueFree();
		}
		segments.Clear();

		float currentXPosition = 0;
		
		for (int i = 0; i < maxCount; i++)
		{
			var segmentInstance = emptySegmentScene.Instantiate();

			if (segmentInstance is Node2D Segment)
			{
				Segment.Position = new Vector2(currentXPosition, 0);

				float segmentWidth = 1;
				currentXPosition += segmentWidth;
			}
			else if (segmentInstance is Control control)
			{
				control.Position = new Vector2(currentXPosition, 0);
				float segmentWidth = 1;
				currentXPosition += segmentWidth;
			}
			
			barNode.AddChild(segmentInstance);
			segments.Add(segmentInstance);
		}
	}
	
	private void UpdateSegmentStates(List<Node> segments, int currentValue, PackedScene fullSegmentScene, PackedScene emptySegmentScene)
	{
		if (fullSegmentScene == null || emptySegmentScene == null) return;
		
		for (int i = 0; i < segments.Count; i++)
		{
			var targetScene = (i < currentValue) ? fullSegmentScene : emptySegmentScene;
			
			var currentSegment = segments[i];
			var currentScenePath = currentSegment.SceneFilePath;
			var targetScenePath = targetScene.ResourcePath;
			
			if (currentScenePath != targetScenePath)
			{
				var position = Vector2.Zero;
				
				if (currentSegment is Node2D Segment)
					position = Segment.Position;
				else if (currentSegment is Control Control)
					position = Control.Position;
				
				var newSegment = targetScene.Instantiate();
				
				if (newSegment is Node2D NewSegment)
					NewSegment.Position = position;
				else if (newSegment is Control NewControl)
					NewControl.Position = position;
				
				var parent = currentSegment.GetParent();
				var index = currentSegment.GetIndex();
				currentSegment.QueueFree();
				parent.AddChild(newSegment);
				parent.MoveChild(newSegment, index);
				segments[i] = newSegment;
			}
		}
	}
	
	private void UpdateEndSpritePosition(TextureRect endSprite, Vector2 originalPos, int maxCount, PackedScene segmentScene)
	{
		if (segmentScene == null) return;
		
		var tempSegment = segmentScene.Instantiate();
		float segmentWidth = 1;
		tempSegment.QueueFree();
		
		float xOffset = maxCount * segmentWidth;
		endSprite.Position = originalPos + new Vector2(xOffset, 0);
	}
	
	private void UpdateStaminaSpecialSegments()
	{
		if (StaminaSegmentsContainer == null || StaminaDodgeSegmentScene == null || Player == null)
			return;
		
		// Get dodge cost from Dodge class
		float dodgeCost = Dodge.DodgeStaminaCost;
int maxStamina = (int)Player.Stats.GetCurrentMax("Stamina");
		
		// Calculate how many special segments we need (one at each dodge cost interval)
		List<int> specialPositions = new List<int>();
		for (float pos = dodgeCost; pos < maxStamina; pos += dodgeCost)
		{
			specialPositions.Add((int)pos);
		}
		
		// Remove old special segments
		foreach (var segment in _staminaSpecialSegments)
		{
			segment.QueueFree();
		}
		_staminaSpecialSegments.Clear();
		
		// Create special segments at dodge cost positions
		foreach (int position in specialPositions)
		{
			var specialSegment = StaminaDodgeSegmentScene.Instantiate();
			
			float xPosition = position * 1; // 1 is segment width
			
			if (specialSegment is Node2D node2D)
			{
				node2D.Position = new Vector2(xPosition, 0);
			}
			else if (specialSegment is Control control)
			{
				control.Position = new Vector2(xPosition, 0);
			}
			
			StaminaSegmentsContainer.AddChild(specialSegment);
			_staminaSpecialSegments.Add(specialSegment);
		}
	}
}
