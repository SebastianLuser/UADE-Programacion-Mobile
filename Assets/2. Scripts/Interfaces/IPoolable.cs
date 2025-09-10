public interface IPoolable
{
    void OnPoolGet();
    void OnPoolReturn();
    void OnPoolDestroy();
}