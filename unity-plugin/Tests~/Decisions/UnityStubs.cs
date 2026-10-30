// The Unity-only symbols that the real CommandRegistry.cs touches, for the pure lane (no Unity install): logging, the label of the log tag, the
// router that fills the registry, and the validator whose usage line BuildHelp prints. No behavior of Unity is simulated; the files under test are
// the real ones (CommandRegistry.cs, CommandOptions.cs, JsonHelper.cs, Decisions/*.cs, Generated/*.g.cs).
namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
}

namespace UnityMCP.Editor
{
    public static class BiomeLabel
    {
        public static string Tag => "Biome";
    }

    public static class CommandRouter
    {
        public static void RegisterAll() { }
    }

    public static class CommandValidator
    {
        public static string AutoUsage(string cmd, string[] required, string[] optional) => cmd;
    }
}
