using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Every placed enemy has a slot that outlives its node. An enemy loads as its spot comes on screen and unloads once
// it is well off screen and not hunting, and the slot keeps what matters: where it stood, its wounds, its level,
// whether its ambush already sprang, and whether it died. Only a rest or a death clears the slate.
// Building or freeing an enemy is not free, so the regular scan only queues them: one loads and one unloads per frame,
// nearest first, and the load ring sits far enough past the screen edge that the queue has time to drain.
public sealed class EnemyRoster
{
    // Loading just past the screen edge lets an enemy be in place before it scrolls into view.
    private const float LOAD_MARGIN = 128f;
    // Unloading waits for a wider ring, so an enemy on the seam is not loaded and dropped every few steps.
    private const float UNLOAD_MARGIN = 220f;
    // A hunting enemy follows Kio across sections; it only drops once the chase has left it far behind.
    private const float HUNT_UNLOAD_DISTANCE = 900f;
    private const float STREAM_INTERVAL = 0.1f;

    public sealed class Slot
    {
        public PlacementKind Kind;
        public Vector2 Spawn;
        public Vector2 Position;
        public float Health = -1f;
        public int Level;
        public bool Stirred;
        public bool Dead;
        public Enemy Live;
    }

    private readonly GameWorld world;
    private readonly List<Slot> slots = new();
    private readonly List<Slot> toLoad = new();
    private readonly List<Slot> toUnload = new();
    private float timer;

    public EnemyRoster(GameWorld gameWorld) => world = gameWorld;

    public IReadOnlyList<Slot> Slots => slots;

    // Enemies built or freed in the last frame; for profiling.
    public int ChangesLastFrame { get; private set; }

    public void Clear()
    {
        slots.Clear();
        toLoad.Clear();
        toUnload.Clear();
    }

    public void Add(PlacementKind kind, Vector2 position) => slots.Add(new Slot { Kind = kind, Spawn = position, Position = position });

    public void Update(float delta)
    {
        ChangesLastFrame = 0;
        timer -= delta;
        if (timer <= 0f)
        {
            timer = STREAM_INTERVAL;
            Scan();
        }
        if (toLoad.Count > 0)
        {
            Slot slot = toLoad[0];
            toLoad.RemoveAt(0);
            if (slot.Live == null && !slot.Dead)
            {
                Load(slot);
                ChangesLastFrame++;
            }
        }
        if (toUnload.Count > 0)
        {
            Slot slot = toUnload[0];
            toUnload.RemoveAt(0);
            if (slot.Live != null && CanDrop(slot, world.Camera.VisibleWorldRect().Grow(UNLOAD_MARGIN)))
            {
                Unload(slot);
                ChangesLastFrame += 10;
            }
        }
    }

    // Streams everything at once, for a cut (a teleport, a rest) where nobody sees the frame it costs.
    public void Stream()
    {
        Scan();
        foreach (Slot slot in toLoad)
            if (slot.Live == null && !slot.Dead)
                Load(slot);
        foreach (Slot slot in toUnload)
            if (slot.Live != null)
                Unload(slot);
        toLoad.Clear();
        toUnload.Clear();
    }

    private void Scan()
    {
        toLoad.Clear();
        toUnload.Clear();
        if (world.Camera == null)
            return;
        Rect2 view = world.Camera.VisibleWorldRect();
        Rect2 load = view.Grow(LOAD_MARGIN);
        Rect2 keep = view.Grow(UNLOAD_MARGIN);
        foreach (Slot slot in slots)
        {
            if (slot.Live != null)
            {
                if (Track(slot) && CanDrop(slot, keep))
                    toUnload.Add(slot);
            }
            else if (!slot.Dead && load.HasPoint(slot.Position))
                toLoad.Add(slot);
        }
        Vector2 kio = world.Player.GlobalPosition;
        toLoad.Sort((a, b) => a.Position.DistanceSquaredTo(kio).CompareTo(b.Position.DistanceSquaredTo(kio)));
        toUnload.Sort((a, b) => b.Position.DistanceSquaredTo(kio).CompareTo(a.Position.DistanceSquaredTo(kio)));
    }

    // Follows a live enemy; false once it died.
    private bool Track(Slot slot)
    {
        Enemy enemy = slot.Live;
        if (!GodotObject.IsInstanceValid(enemy) || enemy.IsDead || enemy.IsQueuedForDeletion())
        {
            slot.Dead = true;
            slot.Live = null;
            return false;
        }
        slot.Position = enemy.GlobalPosition;
        return true;
    }

    private bool CanDrop(Slot slot, Rect2 keep)
    {
        Enemy enemy = slot.Live;
        if (!GodotObject.IsInstanceValid(enemy) || enemy.IsDead || enemy.IsQueuedForDeletion())
            return false;
        bool farFromHunt = !enemy.IsAggroed || enemy.GlobalPosition.DistanceTo(world.Player.GlobalPosition) > HUNT_UNLOAD_DISTANCE;
        return !keep.HasPoint(enemy.GlobalPosition) && farFromHunt && enemy.CanUnload;
    }

    private void Unload(Slot slot)
    {
        Enemy enemy = slot.Live;
        slot.Position = enemy.GlobalPosition;
        slot.Health = enemy.Health;
        slot.Level = enemy.EnemyLevel;
        slot.Stirred |= enemy.HasStirred;
        slot.Live = null;
        world.Actors.RemoveChild(enemy);
        enemy.QueueFree();
    }

    private void Load(Slot slot)
    {
        Enemy enemy = GameWorld.CreateEnemy(slot.Kind, slot.Stirred, slot.Level, slot.Spawn);
        enemy.Position = slot.Position;
        world.Actors.AddChild(enemy);
        slot.Level = enemy.EnemyLevel;
        if (slot.Health >= 0f)
            enemy.RestoreWounds(slot.Health);
        slot.Live = enemy;
    }
}
