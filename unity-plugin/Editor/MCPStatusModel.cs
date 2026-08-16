namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure state model — zero UnityEngine/UnityEditor deps.
    /// Maps (isRunning, isClientConnected[, isChatRunning]) -> display values.
    /// </summary>
    public static class MCPStatusModel
    {
        public enum State { Down, Listen, Up, ChatActive }

        public enum SubState { None, Compiling, PortMismatch, BindFailed, CompileFailed }

        public static SubState GetSubState(bool isCompiling, bool portMismatch, bool bindFailed, bool compileFailed)
        {
            if (bindFailed)    return SubState.BindFailed;
            if (compileFailed) return SubState.CompileFailed;
            if (isCompiling)   return SubState.Compiling;
            if (portMismatch)  return SubState.PortMismatch;
            return SubState.None;
        }

        public static State GetState(bool isRunning, bool isClientConnected)
            => GetState(isRunning, isClientConnected, false);

        public static State GetState(bool isRunning, bool isClientConnected, bool isChatRunning)
        {
            if (!isRunning)                            return State.Down;
            if (isClientConnected)                     return State.Up;
            if (isChatRunning)                         return State.ChatActive;
            return State.Listen;
        }

        public static string GetCssKey(State state) => state switch
        {
            State.Up         => "up",
            State.Listen     => "listen",
            State.ChatActive => "chat",
            _                => "down",
        };

        public static string GetLabel(bool isRunning, bool isClientConnected, int port)
        {
            if (!isRunning)         return "OFFLINE";
            if (!isClientConnected) return "LISTENING";
            return $"ONLINE :{port}";
        }

        public static string GetSub(bool isRunning, bool isClientConnected)
        {
            if (!isRunning)         return "server stopped";
            if (!isClientConnected) return "no client";
            return "client connected";
        }

        public static string GetLabel(State state, int port) => state switch
        {
            State.Up         => $"ONLINE :{port}",
            State.Listen     => "LISTENING",
            State.ChatActive => "CHAT MODE",
            _                => "OFFLINE",
        };

        public static string GetSub(State state) => state switch
        {
            State.Up         => "client connected",
            State.Listen     => "no client",
            State.ChatActive => "chat backend active",
            _                => "server stopped",
        };

        /// <summary>Short pill text for status bar widget.</summary>
        public static string GetPill(State state, int port) => state switch
        {
            State.Up         => $"{BiomeLabel.DisplayName} :{port}",
            State.Listen     => $"{BiomeLabel.DisplayName} ...",
            State.ChatActive => $"{BiomeLabel.DisplayName} Chat",
            _                => $"{BiomeLabel.DisplayName} off",
        };

        // ── SubState-aware overloads ──────────────────────────────────────────

        public static string GetLabel(State state, SubState sub, int port) => sub switch
        {
            SubState.BindFailed    => "BIND FAILED",
            SubState.CompileFailed => "COMPILE ERROR",
            _                      => GetLabel(state, port),
        };

        public static string GetSub(State state, SubState sub, double compileElapsed = 0.0) => sub switch
        {
            SubState.BindFailed    => "bind failed — port in use",
            SubState.CompileFailed => "compile failed",
            SubState.Compiling     => compileElapsed > 0
                                       ? $"compiling — {compileElapsed:F1}s"
                                       : "compiling — clients wait",
            SubState.PortMismatch  => "port fallback — check config",
            _                      => GetSub(state),
        };

        public static string GetPill(State state, SubState sub, int port) => sub switch
        {
            SubState.BindFailed    => $"{BiomeLabel.DisplayName} err",
            SubState.CompileFailed => $"{BiomeLabel.DisplayName} err",
            SubState.Compiling     => $"{BiomeLabel.DisplayName} ⟳",
            SubState.PortMismatch  => $"{BiomeLabel.DisplayName} :{port}",
            _                      => GetPill(state, port),
        };
    }
}
