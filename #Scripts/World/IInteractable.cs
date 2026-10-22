using Godot;

namespace Anomaly;

public interface IInteractable
{
    Vector2 InteractionPosition { get; }
    float InteractionRadius { get; }
    // Just the name; the HUD shows the interact key above it.
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact(Player player);
}
