namespace UnityMCP.Editor.Chat
{
    internal static class ModelContextWindows
    {
        // Returns 0 for unknown backends → caller hides progress bar.
        internal static int GetContextWindow(string modelId, BackendKind backend)
        {
            if (!string.IsNullOrEmpty(modelId))
            {
                var m = modelId.ToLowerInvariant();
                if (m.Contains("fable"))  return 1_000_000;
                if (m.Contains("gpt-5"))  return 1_000_000;
                if (m.Contains("gpt-4.1")) return 1_000_000;
                if (m.Contains("gpt-4"))  return 128_000;
                if (m.StartsWith("o3") || m.StartsWith("o4")) return 200_000;
                if (m.Contains("opus")  || m.Contains("sonnet") || m.Contains("haiku")) return 200_000;
                if (m.Contains("gemini")) return 1_000_000;
                if (m.Contains("kimi")  || m.Contains("moonshot")) return 128_000;
                if (m.Contains("codex")) return 192_000;
            }
            return FallbackForBackend(backend);
        }

        private static int FallbackForBackend(BackendKind backend) => backend switch
        {
            BackendKind.Claude      => 200_000,
            BackendKind.Codex       => 1_000_000,
            BackendKind.Kimi        => 128_000,
            BackendKind.Antigravity => 1_000_000,
            _                       => 0,
        };
    }
}
