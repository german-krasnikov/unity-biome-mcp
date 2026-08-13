namespace UnityMCP.Editor.Chat
{
    internal static class ModelContextWindows
    {
        private static volatile System.Collections.Generic.Dictionary<string, int> _overrides;

        internal static void SetOverrides(System.Collections.Generic.Dictionary<string, int> byModelId)
            => _overrides = byModelId;

        // Returns 0 for unknown backends → caller hides progress bar.
        internal static int GetContextWindow(string modelId, BackendKind backend)
        {
            if (!string.IsNullOrEmpty(modelId))
            {
                var overrides = _overrides;
                if (overrides != null
                    && overrides.TryGetValue(modelId.ToLowerInvariant(), out var cw)
                    && cw > 0)
                    return cw;

                var m = modelId.ToLowerInvariant();

                // Claude 4.6+ — 1M
                if (m.Contains("fable") || m.Contains("mythos"))        return 1_000_000;
                if (m.Contains("opus-5") || m.Contains("sonnet-5"))     return 1_000_000;
                if (m.Contains("opus-4-8") || m.Contains("opus-4-7"))   return 1_000_000;
                if (m.Contains("opus-4-6") || m.Contains("sonnet-4-6")) return 1_000_000;

                // Claude ≤4.5 — 200k
                if (m.Contains("opus") || m.Contains("sonnet") || m.Contains("haiku")) return 200_000;

                // GPT-5.4+ — 1.05M
                if (m.Contains("gpt-5.4") || m.Contains("gpt-5.5") || m.Contains("gpt-5.6")) return 1_050_000;

                // GPT-5.0–5.3 — 400k
                if (m.Contains("gpt-5")) return 400_000;

                // GPT-4.1 — 1M
                if (m.Contains("gpt-4.1")) return 1_000_000;

                // GPT-4o / GPT-4 — 128k
                if (m.Contains("gpt-4")) return 128_000;

                // o3 / o4 reasoning — 200k
                if (m.StartsWith("o3") || m.StartsWith("o4")) return 200_000;

                // Gemini — 1M
                if (m.Contains("gemini")) return 1_000_000;

                // Kimi / Moonshot — 128k
                if (m.Contains("kimi") || m.Contains("moonshot")) return 128_000;

                // Codex CLI model — 192k
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
