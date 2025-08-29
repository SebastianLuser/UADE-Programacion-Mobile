public interface IUpdatable
{
    bool IsActive { get; }
    void OnUpdate(float deltaTime);
}

public interface IFixedUpdatable
{
    bool IsActive { get; }
    void OnFixedUpdate(float fixedDeltaTime);
}

public interface ILateUpdatable
{
    bool IsActive { get; }
    void OnLateUpdate(float deltaTime);
}