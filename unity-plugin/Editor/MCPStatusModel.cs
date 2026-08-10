namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure state model — zero UnityEngine/UnityEditor deps.
    /// Maps (isRunning, isClientConnected[, isChatRunning]) -> display values.
    /// </summary>
    public static class MCPStatusModel
    {
        public enum State { Down, Listen, Up, ChatActive }

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
    }
}
