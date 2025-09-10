public interface IGameService
{
    void Initialize();
    void Shutdown();
    bool IsInitialized { get; }
}