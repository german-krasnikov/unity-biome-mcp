namespace UnityMCP.Editor
{
    public interface IPlaytestSimulator
    {
        string Name { get; }
        void Start(SimulatorArgs args);
        bool Tick(); // return true = done
        string Report();
    }

    public struct SimulatorArgs
    {
        public string CharacterPath;
        public float Duration;
        public float TimeScale;
        public string Target;
        public float Frequency;
    }
}
