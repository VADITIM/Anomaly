public interface IEntityBehavior
{
    void OnReady(Entity owner);
    void OnProcess(double delta);
    void OnPhysicsProcess(double delta);
    void OnExitTree();
}
