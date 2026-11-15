namespace UnityMCP.Editor.Chat
{
    internal static class SessionHandoff
    {
        internal static string GetResumeCommand(BackendKind kind, string sessionId, string projectDir = null)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            var row = BackendTable.Get(kind);
            if (row == null) return null;
            var prefix = row.ResumeCd && !string.IsNullOrEmpty(projectDir)
                ? $"cd {projectDir} && "
                : "";
            return prefix + row.Resume.Replace("{id}", sessionId);
        }

        internal static string GetBinaryName(BackendKind kind) => BackendTable.Get(kind)?.Binary;
    }
}
