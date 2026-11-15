namespace UnityMCP.Editor
{
    public interface IPlaytestMonitor
    {
        string Name { get; }
        void Start();
        void Stop();
        string Report();
    }
}
