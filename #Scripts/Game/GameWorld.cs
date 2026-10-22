using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Akasa is the material world; the Void is what lies through its Fractures. Each is built into the same scene.
public enum WorldRealm
{
	Akasa,
	Void,
}

public partial class GameWorld : Node2D
{
	private const float RESPAWN_DELAY = 3.4f;
	private const float TRAVEL_DELAY = 0.5f;
	private const string NARRATION_SCENE = "res://#Scenes/NarrationBox.tscn";
	private const string FORGE_SCENE = "res://#Scenes/Forge.tscn";
	private static readonly Color AmbientLight = new(0.5f, 0.5f, 0.66f);
	private static readonly Color VoidAmbientLight = new(0.58f, 0.5f, 0.72f);
	// Scenery in the Void is bruised toward violet, whatever land it was torn from.
	private static readonly Color VoidSceneryTint = new(0.8f, 0.68f, 0.95f);

	// The realm survives the scene reload a Fracture crossing does; so does the Fracture Kio stepped through, which is
	// where Akasa puts him back.
	public static WorldRealm Realm { get; private set; } = WorldRealm.Akasa;
	private static Vector2? fractureReturn;
	private static bool autopilotEnteredVoid;

	public bool InVoid => Realm == WorldRealm.Void;
	private Color SceneryTint => InVoid ? VoidSceneryTint : Colors.White;

	public LevelBlueprint Blueprint { get; private set; }
	public WorldGrid Grid { get; private set; }
	public Node2D Actors { get; private set; }
	public Node2D Effects { get; private set; }
	public Node2D Overlay { get; private set; }
	public Player Player { get; private set; }
	public GameCamera Camera { get; private set; }
	public Forge Forge { get; private set; }
	public Hud Hud { get; private set; }
	public NarrationBox Narration { get; private set; }
	public PostProcessLayer PostFx { get; private set; }
	public EnemyDirector Director { get; } = new();
	public List<Entity> Entities { get; } = new();
	public List<IInteractable> Interactables { get; } = new();
	public bool IsInputBlocked => respawning || traveling || anvilMenu.IsOpen || statusMenu.IsOpen || statusMenu.OptionsOpen || worldMap.IsOpen || Narration.IsOpen;
	public MapData Map { get; private set; }
	public WorldSections Sections { get; private set; }
	public EnemyRoster Roster { get; private set; }

	private readonly List<IHittable> extraHittables = new();
	private readonly List<IHittable> targetBuffer = new();
	private readonly HashSet<string> sessionFlags = new();
	private ForgeMenu anvilMenu;
	private StatusMenu statusMenu;
	private WorldMap worldMap;
	private DeathOverlay deathOverlay;
	private SoundBank soundBank;
	private Vector2 anvilPosition;
	private SoulRemnant soulRemnant;
	private readonly List<VoidHeart> voidHearts = new();
	private Vector2? attunedBonfire;
	private Vector2 voidArrival;
	private bool traveling;
	private bool respawning;
	private float respawnTimer;

	public override void _Ready()
	{
		JitWarmup.Start();
		GameInput.Register();
		GameSettings.Load();
		SaveManager.Load();
		EnsureProgressionDefaults();

		AddChild(new TimeControlDriver { Name = "TimeControl" });
		soundBank = new SoundBank { Name = "Sound" };
		AddChild(soundBank);

		if (AutoPilot.StartsInVoid() && !autopilotEnteredVoid)
		{
			autopilotEnteredVoid = true;
			Realm = WorldRealm.Void;
		}
		Blueprint = InVoid ? LevelBlueprint.CreateVoid() : LevelBlueprint.CreateVerdanyaReach();
		Grid = Blueprint.Grid;
		Sections = new WorldSections(Grid, Blueprint.Sections) { Name = "Sections" };
		Sections.Setup(this);
		AddChild(Sections);
		Roster = new EnemyRoster(this);
		BuildVoid();

		var terrain = new TerrainLayer { Name = "Terrain" };
		AddChild(terrain);
		Actors = new Node2D { Name = "Actors", YSortEnabled = true };
		AddChild(Actors);
		Sections.BuildNodes(Actors);
		terrain.Build(Grid, Sections);
		Effects = new Node2D { Name = "Effects", ZIndex = 40 };
		AddChild(Effects);
		Overlay = new Node2D { Name = "Overlay", ZIndex = 60 };
		AddChild(Overlay);
		BuildAtmosphere();
		AddChild(new CanvasModulate { Color = InVoid ? VoidAmbientLight : AmbientLight });

		PostFx = new PostProcessLayer { Name = "PostProcess" };
		AddChild(PostFx);
		Hud = new Hud { Name = "Hud" };
		AddChild(Hud);
		Map = new MapData(Blueprint);
		var miniMap = new MiniMap { Name = "MiniMap" };
		miniMap.Setup(this, Map);
		AddChild(miniMap);
		worldMap = new WorldMap { Name = "WorldMap" };
		worldMap.Setup(this, Map);
		AddChild(worldMap);
		deathOverlay = new DeathOverlay { Name = "DeathOverlay" };
		AddChild(deathOverlay);
		Narration = GD.Load<PackedScene>(NARRATION_SCENE).Instantiate<NarrationBox>();
		AddChild(Narration);
		anvilMenu = new ForgeMenu { Name = "ForgeMenu" };
		anvilMenu.Setup(this);
		AddChild(anvilMenu);
		statusMenu = new StatusMenu { Name = "StatusMenu" };
		statusMenu.Setup(this);
		AddChild(statusMenu);

		SpawnStaticPlacements();
		Sections.RegisterScenery(Actors);
		Sections.RegisterScenery(this);
		Sections.IndexParts();
		SpawnEnemies();

		Camera = new GameCamera { Name = "Camera" };
		AddChild(Camera);
		Camera.Setup(this);
		Camera.SnapToPlayer();
		AttachAmbientParticles();

		soundBank.PlayAmbience("ambience");
		Player.BroadcastAll();
		PlayerSignals.Died += OnPlayerDied;
		StartIntro();

		if (AutoPilot.IsRequested())
			AddChild(new AutoPilot { Name = "AutoPilot" });
	}

	public override void _ExitTree()
	{
		PlayerSignals.Died -= OnPlayerDied;
		TimeControl.Reset();
	}

	private static void EnsureProgressionDefaults()
	{
		// Rotshaping is unlocked after Rà'Meska in the full game; this slice grants the three natural Arcs up front.
		foreach (string arc in ArcLibrary.ALL_ARCS)
			if (!SaveManager.Progression.UnlockedArcs.Contains(arc))
				SaveManager.Progression.UnlockedArcs.Add(arc);
		if (string.IsNullOrEmpty(SaveManager.Progression.EquippedArc))
			SaveManager.Progression.EquippedArc = ArcLibrary.DEFAULT_ARC;
	}

	private void BuildVoid()
	{
		Rect2 bounds = Grid.Bounds.Grow(240f);
		var material = new ShaderMaterial { Shader = ShaderLibrary.Void };
		material.SetShaderParameter(StringNames.Of("world_size"), bounds.Size);
		AddChild(new ColorRect
		{
			Name = "Void",
			Position = bounds.Position,
			Size = bounds.Size,
			Material = material,
			ZIndex = -100,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		var debris = new VoidDebris { Name = "VoidDebris" };
		debris.Build(Grid, bounds);
		AddChild(debris);
		// The Void has no sky, so nothing below the shards but the abyss itself.
		if (!InVoid)
			BuildCloudSea();
	}

	// A bank of clouds far below the islands, over the deep debris. It scrolls slower than the camera so it reads as depth.
	private void BuildCloudSea()
	{
		Rect2 bounds = Grid.Bounds.Grow(720f);
		var material = new ShaderMaterial { Shader = ShaderLibrary.Clouds };
		material.SetShaderParameter(StringNames.Of("world_size"), bounds.Size);
		var parallax = new Parallax2D { Name = "CloudSea", ScrollScale = new Vector2(0.82f, 0.82f), ZIndex = -70 };
		parallax.AddChild(new ColorRect
		{
			Position = bounds.Position,
			Size = bounds.Size,
			Material = material,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		AddChild(parallax);
	}

	// Haze sits above the actors and under the combat effects, so hits and slashes cut through it.
	private void BuildAtmosphere()
	{
		Rect2 bounds = Grid.Bounds.Grow(240f);
		var material = new ShaderMaterial { Shader = ShaderLibrary.Atmosphere };
		material.SetShaderParameter(StringNames.Of("world_size"), bounds.Size);
		AddChild(new ColorRect
		{
			Name = "Atmosphere",
			Position = bounds.Position,
			Size = bounds.Size,
			Material = material,
			ZIndex = 39,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		// Shadows of clouds passing overhead; under the haze so the fog still lifts them. The Void has no clouds.
		if (InVoid)
			return;
		var shadowMaterial = new ShaderMaterial { Shader = ShaderLibrary.Clouds };
		shadowMaterial.SetShaderParameter(StringNames.Of("world_size"), bounds.Size);
		shadowMaterial.SetShaderParameter(StringNames.Of("shadow_pass"), true);
		AddChild(new ColorRect
		{
			Name = "CloudShadows",
			Position = bounds.Position,
			Size = bounds.Size,
			Material = shadowMaterial,
			ZIndex = 38,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
	}

	private void AttachAmbientParticles()
	{
		// Spores over Akasa; in the Void, motes of its own light.
		Color spore = InVoid ? new Color(0.95f, 0.45f, 0.9f) : new Color(0.75f, 1f, 0.85f);
		Color sporeFar = InVoid ? new Color(0.5f, 0.85f, 1f) : new Color(0.8f, 0.85f, 1f);
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(spore, 0f));
		gradient.SetColor(1, new Color(sporeFar, 0f));
		gradient.AddPoint(0.3f, new Color(spore, 0.7f));
		gradient.AddPoint(0.7f, new Color(sporeFar, 0.5f));
		var spores = new CpuParticles2D
		{
			Amount = 46,
			Lifetime = 9f,
			Preprocess = 9f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(280, 170),
			Direction = new Vector2(0.3f, -1f),
			Spread = 40f,
			InitialVelocityMin = 3f,
			InitialVelocityMax = 10f,
			Gravity = new Vector2(2f, -2f),
			ScaleAmountMin = 1f,
			ScaleAmountMax = 2f,
			ColorRamp = gradient,
			LocalCoords = false,
			Material = Vfx.Additive,
			ZIndex = 45,
		};
		Camera.Camera.AddChild(spores);

		// Ash from what the Void has already taken, drifting across the islands.
		var ashFade = new Gradient();
		ashFade.SetColor(0, new Color(0.3f, 0.26f, 0.34f, 0f));
		ashFade.SetColor(1, new Color(0.3f, 0.26f, 0.34f, 0f));
		ashFade.AddPoint(0.2f, new Color(0.34f, 0.3f, 0.38f, 0.85f));
		ashFade.AddPoint(0.8f, new Color(0.22f, 0.16f, 0.28f, 0.6f));
		Camera.Camera.AddChild(new CpuParticles2D
		{
			Amount = 34,
			Lifetime = 11f,
			Preprocess = 11f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(300, 180),
			Direction = new Vector2(0.8f, 0.6f),
			Spread = 25f,
			InitialVelocityMin = 6f,
			InitialVelocityMax = 14f,
			Gravity = new Vector2(1f, 2f),
			AngularVelocityMin = -60f,
			AngularVelocityMax = 60f,
			ScaleAmountMin = 1f,
			ScaleAmountMax = 2f,
			ColorRamp = ashFade,
			LocalCoords = false,
			ZIndex = 44,
		});
	}

	private void SpawnStaticPlacements()
	{
		foreach (Placement placement in Blueprint.Placements)
		{
			Vector2 position = WorldGrid.TileCenter(placement.Tile.X, placement.Tile.Y);
			float surface = Grid.SurfaceZAt(position);
			int hash = Art.Hash(placement.Tile.X, placement.Tile.Y, 3);
			Biome biome = Grid.ArtBiomeAt(placement.Tile.X, placement.Tile.Y);
			if (biome != Biome.Verdant && TrySpawnBiomeObject(placement.Kind, biome, position, surface, hash))
				continue;
			switch (placement.Kind)
			{
				case PlacementKind.PlayerStart:
					// Back from the Void, Kio steps out of the Fracture he went in by.
					Vector2 start = position;
					if (InVoid)
						voidArrival = position;
					else if (fractureReturn.HasValue)
					{
						start = fractureReturn.Value;
						fractureReturn = null;
					}
					Player = new Player { Name = "Kio", Position = start };
					Actors.AddChild(Player);
					break;
				case PlacementKind.RendAnvil:
					anvilPosition = position;
					Forge = GD.Load<PackedScene>(FORGE_SCENE).Instantiate<Forge>();
					Forge.Setup(this, position);
					Actors.AddChild(Forge);
					break;
				case PlacementKind.Npc:
					var npc = new Npc { Name = "Npc_" + placement.Id };
					npc.Setup(this, position, placement.Id);
					Actors.AddChild(npc);
					break;
				case PlacementKind.Bonfire:
					var bonfire = new Bonfire { Name = "Bonfire" };
					bonfire.Setup(this, position);
					Actors.AddChild(bonfire);
					break;
				case PlacementKind.VoidHeart:
					if (SaveManager.World.DestroyedVoidHearts.Contains(placement.Id))
						break;
					var heart = new VoidHeart { Name = "VoidHeart", Position = position, HeartId = placement.Id, Waves = VoidHeart.WavesFor(biome) };
					voidHearts.Add(heart);
					Actors.AddChild(heart);
					break;
				case PlacementKind.ShortcutGate:
					var gate = new ShortcutGate { Name = "ShortcutGate" };
					gate.Setup(this, position, placement.Id, placement.Side);
					Actors.AddChild(gate);
					break;
				case PlacementKind.Fracture:
				case PlacementKind.SealedFracture:
					var fracture = new Fracture { Name = "Fracture" };
					fracture.Setup(this, position, placement.Id ?? "Azurean", placement.Kind == PlacementKind.SealedFracture);
					Actors.AddChild(fracture);
					break;
				case PlacementKind.GnawerNest:
					// The nest is a bone heap with no footprint, so the pack can burst out of it.
					SpriteRegion heap = biome == Biome.Undead ? BiomeArt.SkullPile : BiomeArt.Rock(Biome.Undead, hash);
					AddRuinDecoration(heap, position, surface, BiomeArt.ObjectTint(Biome.Undead), false, 0f);
					break;
				case PlacementKind.VoidRift:
					break;
				case PlacementKind.SkeletonKing:
				case PlacementKind.SkullPile:
				case PlacementKind.Tower:
				case PlacementKind.Fountain:
				case PlacementKind.Altar:
				case PlacementKind.SnowCave:
					SpawnLandmark(placement.Kind, position, surface);
					break;
				case PlacementKind.Crate:
				case PlacementKind.Barrel:
				case PlacementKind.Vase:
				case PlacementKind.Pot:
				case PlacementKind.SoulCache:
					SpawnProp(placement.Kind, position);
					break;
				case PlacementKind.Tree:
					if (RuinScatter.DistanceToPit(Grid, placement.Tile.X, placement.Tile.Y, 2) <= 2)
						AddRuinDecoration(Art.DeadTrees[hash % Art.DeadTrees.Length], position, surface, RuinScatter.DeadWoodTint, true, 5f);
					else
						AddDecoration(Art.Trees[hash % Art.Trees.Length], position, surface, true, true, 5f, 300f);
					break;
				case PlacementKind.DeadTree:
					AddRuinDecoration(Art.DeadTrees[hash % Art.DeadTrees.Length], position, surface, RuinScatter.DeadWoodTint, true, 5f);
					break;
				case PlacementKind.RuinWall:
					AddRuinDecoration(Art.WallRuin, position, surface, RuinScatter.StoneTint, true, 0f);
					for (int segment = -1; segment <= 1; segment++)
						Grid.AddObstacle(position + new Vector2(segment * 30f, -4f), 14f, surface, 300f);
					break;
				case PlacementKind.Idol:
					AddRuinDecoration(hash % 3 == 0 ? Art.IdolWhole : Art.Relics[hash % 3], position, surface, RuinScatter.StoneTint, false, 10f);
					break;
				case PlacementKind.Obelisk:
					AddRuinDecoration(hash % 2 == 0 ? Art.ObeliskTall : Art.ObeliskShort, position, surface, RuinScatter.StoneTint, true, 10f);
					break;
				case PlacementKind.Column:
					AddRuinDecoration(Art.Column, position, surface, RuinScatter.StoneTint, true, 10f);
					break;
				case PlacementKind.Bush:
					AddDecoration(Art.Bushes[hash % Art.Bushes.Length], position + new Vector2(0, 6), surface, false, true, 0f, 0f);
					break;
				case PlacementKind.Statue:
					AddDecoration(Art.Statue, position, surface, false, false, 10f, 300f);
					break;
				case PlacementKind.Pillar:
					AddDecoration(Art.Pillar, position, surface, true, false, 11f, 300f);
					break;
				case PlacementKind.BrokenPillar:
					AddDecoration(Art.BrokenPillar, position, surface, false, false, 11f, 300f);
					break;
				case PlacementKind.Rock:
					AddDecoration(Art.Rock, position, surface, false, false, 14f, 22f);
					break;
				case PlacementKind.Brazier:
					AddLight(WorldLightKind.Brazier, position);
					break;
			}
		}

		PlantBoundaryForest();
		PlaceVoidGlows();
		RuinScatter.Populate(this);
		RuinScatter.AttachCrumble(this, this);

		// Each rift feeds the nearest heart's waves.
		foreach (Placement placement in Blueprint.Placements)
		{
			if (placement.Kind != PlacementKind.VoidRift || voidHearts.Count == 0)
				continue;
			Vector2 rift = WorldGrid.TileCenter(placement.Tile.X, placement.Tile.Y);
			VoidHeart nearest = voidHearts[0];
			foreach (VoidHeart heart in voidHearts)
				if (heart.Position.DistanceTo(rift) < nearest.Position.DistanceTo(rift))
					nearest = heart;
			nearest.RiftPositions.Add(rift);
		}
	}

	// Imported-pack scenery: each biome swaps the sprite behind the same placement kinds and adds its own landmarks.
	private bool TrySpawnBiomeObject(PlacementKind kind, Biome biome, Vector2 position, float surface, int hash)
	{
		Color tint = BiomeArt.ObjectTint(biome);
		switch (kind)
		{
			case PlacementKind.Tree:
				AddRuinDecoration(BiomeArt.Tree(biome, hash), position, surface, tint, true, 5f);
				return true;
			case PlacementKind.DeadTree:
				AddRuinDecoration(BiomeArt.DeadTree(biome, hash), position, surface, tint, true, 5f);
				return true;
			case PlacementKind.Bush:
				AddRuinDecoration(BiomeArt.Bush(biome, hash), position + new Vector2(0, 6), surface, tint, false, 0f);
				return true;
			case PlacementKind.Rock:
				AddRuinDecoration(BiomeArt.Rock(biome, hash), position, surface, tint, false, 14f);
				return true;
			case PlacementKind.Statue:
			case PlacementKind.Pillar:
			case PlacementKind.BrokenPillar:
			case PlacementKind.Idol:
			case PlacementKind.Obelisk:
			case PlacementKind.Column:
				if (biome == Biome.Snow)
					return false;
				AddRuinDecoration(BiomeArt.Monument(biome, kind, hash), position, surface, tint, true, 10f);
				return true;
			case PlacementKind.RuinWall:
				if (biome == Biome.Snow)
					return false;
				AddRuinDecoration(BiomeArt.Monument(biome, kind, hash), position, surface, tint, true, 0f);
				for (int segment = -1; segment <= 1; segment++)
					Grid.AddObstacle(position + new Vector2(segment * 28f, -4f), 14f, surface, 300f);
				return true;
		}
		return false;
	}

	private void SpawnLandmark(PlacementKind kind, Vector2 position, float surface)
	{
		switch (kind)
		{
			case PlacementKind.SkeletonKing:
				AddRuinDecoration(BiomeArt.SkeletonKing, position, surface, BiomeArt.ObjectTint(Biome.Undead), false, 0f);
				foreach (float offset in new[] { -44f, -22f, 0f, 22f, 44f })
					Grid.AddObstacle(position + new Vector2(offset, -18f - Mathf.Abs(offset) * 0.15f), 14f, surface, 300f);
				break;
			case PlacementKind.SkullPile:
				AddRuinDecoration(BiomeArt.SkullPile, position, surface, BiomeArt.ObjectTint(Biome.Undead), false, 16f);
				break;
			case PlacementKind.Tower:
				AddRuinDecoration(BiomeArt.ForestTower, position, surface, BiomeArt.ObjectTint(Biome.BrightForest), true, 0f);
				foreach (float offset in new[] { -36f, -12f, 12f, 36f })
					Grid.AddObstacle(position + new Vector2(offset, -6f), 14f, surface, 300f);
				break;
			case PlacementKind.Fountain:
				AddRuinDecoration(BiomeArt.ForestFountain, position, surface, BiomeArt.ObjectTint(Biome.BrightForest), false, 0f);
				Grid.AddObstacle(position + new Vector2(0, -10f), 26f, surface, 24f);
				Grid.AddObstacle(position + new Vector2(-34f, -10f), 14f, surface, 24f);
				Grid.AddObstacle(position + new Vector2(34f, -10f), 14f, surface, 24f);
				break;
			case PlacementKind.Altar:
				AddRuinDecoration(BiomeArt.ForestAltar, position, surface, BiomeArt.ObjectTint(Biome.BrightForest), false, 18f);
				break;
			case PlacementKind.SnowCave:
				AddRuinDecoration(BiomeArt.SnowCave, position, surface, BiomeArt.ObjectTint(Biome.Snow), false, 0f);
				foreach (float offset in new[] { -52f, -26f, 0f, 26f, 52f })
					Grid.AddObstacle(position + new Vector2(offset, -12f), 15f, surface, 300f);
				break;
		}
	}

	private void PlantBoundaryForest()
	{
		for (int y = 0; y < Grid.Height; y++)
		{
			for (int x = 0; x < Grid.Width; x++)
			{
				if (Grid.HeightAt(x, y) != WorldGrid.WALL_HEIGHT || Art.Hash(x, y, 77) % 100 > 30)
					continue;
				bool bordersPlay = false;
				for (int dy = -1; dy <= 2 && !bordersPlay; dy++)
					for (int dx = -1; dx <= 1 && !bordersPlay; dx++)
					{
						int neighbour = Grid.HeightAt(x + dx, y + dy);
						bordersPlay = neighbour >= 0 && neighbour < WorldGrid.WALL_HEIGHT;
					}
				if (!bordersPlay)
					continue;
				int hash = Art.Hash(x, y, 78);
				Vector2 position = WorldGrid.TileCenter(x, y) + new Vector2(hash % 12 - 6, 0);
				bool withered = Art.Hash(x, y, 79) % 100 < 35;
				Biome biome = Grid.ArtBiomeAt(x, y);
				SpriteRegion region = biome != Biome.Verdant
					? (withered ? BiomeArt.DeadTree(biome, hash) : BiomeArt.Tree(biome, hash))
					: withered ? Art.DeadTrees[hash % Art.DeadTrees.Length] : Art.Trees[hash % Art.Trees.Length];
				Color tint = biome != Biome.Verdant
					? BiomeArt.ObjectTint(biome).Darkened(0.2f)
					: withered ? RuinScatter.DeadWoodTint.Darkened(0.15f) : new Color(0.62f, 0.78f, 0.72f);
				var tree = new Decoration();
				tree.Setup(this, region, position, WorldGrid.WALL_HEIGHT * WorldGrid.LEVEL_HEIGHT, true, !withered && biome == Biome.Verdant, tint);
				Actors.AddChild(tree);
			}
		}
	}

	private void AddLight(WorldLightKind kind, Vector2 position)
	{
		var light = new WorldLight { Name = kind.ToString() };
		light.Setup(this, kind, position);
		Actors.AddChild(light);
	}

	// The Void lights the islands from below: sparse magenta glows sit on pit tiles that border walkable ground.
	private void PlaceVoidGlows()
	{
		const int maximum = 70;
		var candidates = new List<Vector2I>();
		for (int y = 0; y < Grid.Height; y++)
		{
			for (int x = 0; x < Grid.Width; x++)
			{
				if (!Grid.IsPit(x, y))
					continue;
				bool bordersGround = false;
				for (int dy = -1; dy <= 1 && !bordersGround; dy++)
					for (int dx = -1; dx <= 1 && !bordersGround; dx++)
					{
						int neighbour = Grid.HeightAt(x + dx, y + dy);
						bordersGround = neighbour >= 0 && neighbour < WorldGrid.WALL_HEIGHT;
					}
				if (bordersGround)
					candidates.Add(new Vector2I(x, y));
			}
		}
		float stride = Mathf.Max(1f, candidates.Count / (float)maximum);
		for (float i = Art.Hash(3, 7, 91) % 3; i < candidates.Count; i += stride)
		{
			Vector2I tile = candidates[(int)i];
			var glow = new WorldLight { Name = "VoidGlow" };
			glow.Setup(this, WorldLightKind.VoidGlow, WorldGrid.TileCenter(tile.X, tile.Y));
			AddChild(glow);
		}
	}

	private void AddDecoration(SpriteRegion region, Vector2 position, float surface, bool fade, bool sway, float obstacleRadius, float obstacleHeight)
	{
		float elevation = float.IsNegativeInfinity(surface) ? 0f : surface;
		var decoration = new Decoration();
		decoration.Setup(this, region, position, elevation, fade, sway, (region.TexturePath == Art.VEGETATION ? TerrainPainter.GrassTint : Colors.White) * SceneryTint);
		Actors.AddChild(decoration);
		if (obstacleRadius > 0f)
			Grid.AddObstacle(position, obstacleRadius, elevation, obstacleHeight);
	}

	public void AddRuinDecoration(SpriteRegion region, Vector2 position, float surface, Color tint, bool fade, float obstacleRadius)
	{
		float elevation = float.IsNegativeInfinity(surface) ? 0f : surface;
		var decoration = new Decoration();
		decoration.Setup(this, region, position, elevation, fade, false, tint * SceneryTint);
		Actors.AddChild(decoration);
		if (obstacleRadius > 0f)
			Grid.AddObstacle(position, obstacleRadius, elevation, 300f);
	}

	private void SpawnProp(PlacementKind kind, Vector2 position)
	{
		PropKind propKind = kind switch
		{
			PlacementKind.Barrel => PropKind.Barrel,
			PlacementKind.Vase => PropKind.Vase,
			PlacementKind.Pot => PropKind.Pot,
			PlacementKind.SoulCache => PropKind.SoulCache,
			_ => PropKind.Crate,
		};
		Actors.AddChild(new Prop { Kind = propKind, Position = position });
	}

	// Placed enemies are not spawned up front: the roster records each one and streams it in and out around the screen.
	private void SpawnEnemies()
	{
		Roster.Clear();
		foreach (Placement placement in Blueprint.Placements)
		{
			Vector2 position = WorldGrid.TileCenter(placement.Tile.X, placement.Tile.Y);
			if (placement.Kind == PlacementKind.GnawerNest)
			{
				foreach (Vector2 offset in new[] { new Vector2(-6, -3), new Vector2(6, -2), new Vector2(0, 5) })
					Roster.Add(PlacementKind.GnawerNest, position + offset);
			}
			else if (IsEnemy(placement.Kind))
				Roster.Add(placement.Kind, position);
		}
	}

	private static bool IsEnemy(PlacementKind kind) => kind is PlacementKind.Rotling or PlacementKind.Hound or PlacementKind.Wasp or PlacementKind.Brute
		or PlacementKind.TargetDummy or PlacementKind.Gnawer or PlacementKind.BloatedGnawer or PlacementKind.Bonewalker or PlacementKind.Rimebound
		or PlacementKind.Rimeslime or PlacementKind.Bogslime or PlacementKind.Slimelet or PlacementKind.GloomBat or PlacementKind.Shadeflit;

	public Enemy SpawnEnemy(PlacementKind kind, Vector2 position)
	{
		Enemy enemy = CreateEnemy(kind, false, 0, null);
		enemy.Position = position;
		Actors.AddChild(enemy);
		return enemy;
	}

	// A stirred ambusher (a nest rat that burst out, a bat that woke) comes back already awake.
	public static Enemy CreateEnemy(PlacementKind kind, bool stirred, int level, Vector2? home) => CreateEnemy(kind, stirred, level, home, false);

	private static Enemy CreateEnemy(PlacementKind kind, bool stirred, int level, Vector2? home, bool fromRift)
	{
		return kind switch
		{
			PlacementKind.Hound => new Hound { PresetLevel = level, Home = home },
			PlacementKind.Wasp => new Wasp { PresetLevel = level, Home = home },
			PlacementKind.Brute => new Brute { PresetLevel = level, Home = home },
			PlacementKind.TargetDummy => new TargetDummy { PresetLevel = level, Home = home },
			PlacementKind.Gnawer => new Gnawer { PresetLevel = level, Home = home },
			PlacementKind.BloatedGnawer => new Gnawer { Bloated = true, PresetLevel = level, Home = home },
			PlacementKind.GnawerNest => new Gnawer { Nested = !stirred, PresetLevel = level, Home = home },
			PlacementKind.Bonewalker => new Bonewalker { PresetLevel = level, Home = home },
			PlacementKind.Rimebound => new Bonewalker { Rimebound = true, PresetLevel = level, Home = home },
			PlacementKind.Rimeslime => new Slime { Kind = SlimeKind.Rime, PresetLevel = level, Home = home },
			PlacementKind.Bogslime => new Slime { Kind = SlimeKind.Bog, PresetLevel = level, Home = home },
			PlacementKind.Slimelet => new Slime { Kind = SlimeKind.Slimelet, PresetLevel = level, Home = home },
			// Out of the rifts a bat comes already awake.
			PlacementKind.GloomBat => new GloomBat { Sleeping = !fromRift && !stirred, PresetLevel = level, Home = home },
			PlacementKind.Shadeflit => new Shadeflit { PresetLevel = level, Home = home },
			_ => new Rotling { PresetLevel = level, Home = home },
		};
	}

	public Enemy SpawnFromRift(PlacementKind kind, Vector2 position, float delay)
	{
		float surface = Grid.SurfaceZAt(position);
		Vector2 screen = position + new Vector2(0, -surface);
		Vfx.Ring(Effects, screen, 18f, 2f, new Color(0.85f, 0.25f, 0.8f), 0.5f, 3f, 0.5f);
		Vfx.Burst(Effects, screen + new Vector2(0, -8f), 18, new Color(0.8f, 0.2f, 0.75f), 80f, Vector2.Up, 60f, 0.6f, -30f, 2f, 40f, true);
		Sfx.Play("void_rupture", -14f, 1.25f);
		Enemy enemy = CreateEnemy(kind, false, 0, null, true);
		enemy.Position = position;
		enemy.SpawnedByEncounter = true;
		Actors.AddChild(enemy);
		enemy.Flash(0.4f, new Color(0.9f, 0.3f, 0.85f));
		enemy.Aggro();
		return enemy;
	}

	public void RegisterEntity(Entity entity)
	{
		if (!Entities.Contains(entity))
			Entities.Add(entity);
	}

	public void UnregisterEntity(Entity entity) => Entities.Remove(entity);

	public void RegisterHittable(IHittable hittable) => extraHittables.Add(hittable);

	public void UnregisterHittable(IHittable hittable) => extraHittables.Remove(hittable);

	public void RegisterInteractable(IInteractable interactable)
	{
		if (!Interactables.Contains(interactable))
			Interactables.Add(interactable);
	}

	public void UnregisterInteractable(IInteractable interactable) => Interactables.Remove(interactable);

	public List<IHittable> CollectPlayerTargets()
	{
		targetBuffer.Clear();
		foreach (Entity entity in Entities)
			if (entity.CanReceivePlayerHits)
				targetBuffer.Add(entity);
		foreach (IHittable hittable in extraHittables)
			if (hittable.CanReceivePlayerHits)
				targetBuffer.Add(hittable);
		return new List<IHittable>(targetBuffer);
	}

	public void SpawnSoulOrbs(Vector2 position, float z, int souls)
	{
		int orbs = Mathf.Clamp(souls / 4, 1, 14);
		int remaining = souls;
		for (int i = 0; i < orbs; i++)
		{
			int value = i == orbs - 1 ? remaining : souls / orbs;
			remaining -= value;
			var orb = new SoulOrb();
			orb.Setup(this, position, z, value);
			Actors.AddChild(orb);
		}
	}

	public void DropSoulRemnant(Vector2 position, int souls)
	{
		soulRemnant?.QueueFree();
		soulRemnant = new SoulRemnant { Name = "SoulRemnant" };
		soulRemnant.Setup(this, position, souls);
		CallDeferred(MethodName.AddRemnant);
	}

	private void AddRemnant() => Actors.AddChild(soulRemnant);

	public void ClearSoulRemnant(SoulRemnant remnant)
	{
		if (soulRemnant == remnant)
			soulRemnant = null;
		remnant.QueueFree();
	}

	public void Speak(string id, string line)
	{
		if (SaveManager.Progression.NarrativeFlags.Contains(id) || sessionFlags.Contains(id))
			return;
		sessionFlags.Add(id);
		SaveManager.Progression.NarrativeFlags.Add(id);
		Sfx.Play("voice", -6f);
		Narration.Say(Speaker.VOICE, line);
	}

	// The Void announces itself through its arrival trigger instead.
	private void StartIntro()
	{
		if (InVoid)
			return;
		GetTree().CreateTimer(0.7f).Timeout += () => WorldSignals.EmitRegionEntered("VERDANYA", "Hollow Reach  ·  A.V.");
		GetTree().CreateTimer(4.2f).Timeout += () =>
			Speak("voice_intro", "Wake, Hunter. Akasa decays, and you are the blade it has been waiting for. Every Fracture out of the Reach leads to a heart.");
	}

	public override void _Process(double delta)
	{
		Director.Update(this);
		Roster.Update((float)delta);
		CheckTriggers();
		if (respawning)
		{
			respawnTimer -= (float)delta;
			if (respawnTimer <= 0f)
				CompleteRespawn();
		}
	}

	private void CheckTriggers()
	{
		if (Player.IsDead)
			return;
		Vector2I tile = WorldGrid.ToTile(Player.GlobalPosition);
		foreach (NarrativeTrigger trigger in Blueprint.Triggers)
		{
			if (sessionFlags.Contains(trigger.Id) || !trigger.Area.HasPoint(tile))
				continue;
			sessionFlags.Add(trigger.Id);
			if (trigger.RegionTitle != null)
				WorldSignals.EmitRegionEntered(trigger.RegionTitle, trigger.RegionSubtitle);
			if (trigger.Line != null)
				Speak(trigger.Id + "_line", trigger.Line);
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		GameInput.TrackDevice(inputEvent);
		if (inputEvent.IsActionPressed(GameInput.PAUSE) && !IsInputBlocked)
		{
			GetViewport().SetInputAsHandled();
			statusMenu.Open(false);
		}
		else if (inputEvent.IsActionPressed(GameInput.MAP) && !IsInputBlocked && !Player.IsDead)
		{
			GetViewport().SetInputAsHandled();
			worldMap.Open();
		}
		else if (inputEvent.IsActionPressed(GameInput.CAMERA_UNLOCK) && !IsInputBlocked && !Player.IsDead)
		{
			GetViewport().SetInputAsHandled();
			Camera.SetUnlocked(!Camera.Unlocked);
		}
		else if (inputEvent.IsActionPressed(GameInput.DEBUG_GOD_MODE) && OS.IsDebugBuild() && !IsInputBlocked)
		{
			GetViewport().SetInputAsHandled();
			Player.ToggleGodMode();
		}
	}

	public override void _Input(InputEvent inputEvent) => GameInput.TrackDevice(inputEvent);

	public void OpenWorldMap() => worldMap.Open();

	public void TeleportViaMap(Vector2I tile) => worldMap.TeleportTo(tile);

	public void OpenBonfireForTest() => OpenBonfireMenu();

	public void OpenPauseForTest(bool options)
	{
		statusMenu.Open(false);
		if (options)
			statusMenu.OpenOptionsForTest();
	}

	public void CloseMenusForTest()
	{
		statusMenu.CloseAll();
		if (anvilMenu.IsOpen)
			anvilMenu.Close();
		if (worldMap.IsOpen)
			worldMap.Close();
	}

	public void ShowDeathForTest(bool show)
	{
		if (show)
			deathOverlay.ShowDeath();
		else
			deathOverlay.HideDeath();
	}

	public void OpenAnvilMenu()
	{
		anvilMenu.Open();
	}

	// A bonfire remembers Kio: death returns there instead of to Maldor's forge. It does not rest or refill.
	public void AttuneBonfire(Vector2 position)
	{
		if (attunedBonfire == position)
			return;
		attunedBonfire = position;
		Hud.Toast("The bonfire will call you back here if you fall.");
	}

	public void OpenBonfireMenu()
	{
		Sfx.Play("ui_confirm", -4f, 1.1f);
		statusMenu.Open(true);
	}

	public void RestAtBonfire()
	{
		deathOverlay.FadeThrough(0.9f);
		Player.RestoreAtRest();
		RespawnEnemies();
		SaveManager.Progression.HeldSouls = Player.BankedSouls;
		SaveManager.Save(SavePoint.Maldor);
		Hud.Toast("Rested. The world remembers your progress.");
		Sfx.Play("heal", -4f, 0.7f);
	}

	private void OnPlayerDied()
	{
		respawning = true;
		respawnTimer = RESPAWN_DELAY;
		deathOverlay.ShowDeath();
	}

	private void CompleteRespawn()
	{
		respawning = false;
		deathOverlay.HideDeath();
		RespawnEnemies();
		Player.Respawn(RespawnPosition());
		Camera.SnapToPlayer();
		Player.BroadcastAll();
	}

	// The Void has no forge: a fall there returns Kio to the Wound he arrived in, unless a bonfire there has attuned him.
	private Vector2 RespawnPosition() => attunedBonfire.HasValue ? attunedBonfire.Value + new Vector2(0, 26)
		: InVoid ? voidArrival : anvilPosition + new Vector2(20, 18);

	// Stepping through a Fracture carries Kio between Akasa and the Void. The realm is rebuilt from scratch, so the souls he
	// holds are banked first; everything else he carries (health, flasks) is restored, as on any arrival.
	public void TravelThroughFracture(Vector2 fracturePosition)
	{
		if (traveling || Player.IsDead)
			return;
		traveling = true;
		if (!InVoid)
			fractureReturn = fracturePosition + new Vector2(0, 30);
		Realm = InVoid ? WorldRealm.Akasa : WorldRealm.Void;
		SaveManager.Progression.HeldSouls = Player.BankedSouls;
		SaveManager.Save(SavePoint.None);

		Sfx.Play("void_rupture", -2f, 0.8f);
		PostFx.Flash(new Color(0.95f, 0.3f, 0.85f), 0.8f);
		PostFx.KickAberration(0.8f);
		Camera.Feedback.AddTrauma(0.5f);
		Vfx.Burst(Effects, Player.VisualCenter, 40, new Color(0.9f, 0.3f, 0.85f), 160f, Vector2.Up, 180f, 0.8f, 0f, 2.5f, 40f, true);
		deathOverlay.FadeThrough(TRAVEL_DELAY * 2f);
		GetTree().CreateTimer(TRAVEL_DELAY).Timeout += () => GetTree().ReloadCurrentScene();
	}

	// A fresh save starts in Akasa.
	public static void ResetRealm()
	{
		Realm = WorldRealm.Akasa;
		fractureReturn = null;
	}

	private void RespawnEnemies()
	{
		foreach (Entity entity in Entities.ToArray())
			if (entity is Enemy)
			{
				Entities.Remove(entity);
				entity.QueueFree();
			}
		foreach (VoidHeart heart in voidHearts)
			heart.ResetEncounter();
		SpawnEnemies();
		Roster.Stream();
	}

	public void RuptureVoidHeart(VoidHeart heart)
	{
		Vector2 center = heart.VisualCenter;
		TimeControl.SlowMotion(1.8f, 0.18f);
		PostFx.Flash(Colors.White, 1f);
		PostFx.KickAberration(1f);
		Camera.Feedback.AddTrauma(1f);
		Camera.Feedback.Punch(0.12f);
		Sfx.Play("void_rupture", 2f);
		for (int i = 0; i < 4; i++)
			Vfx.Ring(Effects, center, 6f + i * 8f, 120f + i * 50f, new Color(0.95f, 0.3f, 0.85f), 0.8f + i * 0.25f, 6f - i, 0.7f);
		Vfx.Burst(Effects, center, 60, new Color(0.95f, 0.3f, 0.85f), 260f, Vector2.Up, 180f, 1.2f, 0f, 3f, 60f, true);
		Vfx.Burst(Effects, center, 30, new Color(0.15f, 0.0f, 0.2f), 140f, Vector2.Up, 180f, 1.4f, -20f, 4f, 30f);
		SpawnSoulOrbs(heart.GlobalPosition, heart.Z + 20f, 220);

		foreach (Entity entity in Entities.ToArray())
			if (entity is Enemy enemy && enemy.SpawnedByEncounter && !enemy.IsDead)
				enemy.ReceiveHit(new HitInfo { Damage = 9999f, Direction = (enemy.GlobalPosition - heart.GlobalPosition).Normalized(), Knockback = 300f, Source = HitSource.Environment });

		Player.AddCorruption(Player.CORRUPTION_MAXIMUM);
		SaveManager.World.DestroyedVoidHearts.Add(heart.HeartId);
		DifficultyScalingSystem.RegisterVoidHeartDestroyed();
		SaveManager.Progression.HeldSouls = Player.BankedSouls;
		SaveManager.Save(SavePoint.None);
		WorldSignals.EmitVoidHeartDestroyed(heart.HeartId);
		voidHearts.Remove(heart);
		Map.RemoveHeartMarker(heart.HeartId);

		GetTree().CreateTimer(1.2f).Timeout += () =>
		{
			WorldSignals.EmitRegionEntered("VOID HEART CLEANSED", "The land breathes easier.");
			heart.QueueFree();
		};
		GetTree().CreateTimer(5.5f).Timeout += () =>
			Speak("voice_heart_destroyed", "Well done, Hunter. Feel how much stronger you are? Rest now. There are more.");
	}
}
