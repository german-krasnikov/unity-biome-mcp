using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ModelContextWindowsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Claude 4.6+ — 1M
        [TestCase("claude-fable-5",             BackendKind.Claude,      1_000_000)]
        [TestCase("claude-mythos-5",            BackendKind.Claude,      1_000_000)]
        [TestCase("claude-opus-5",              BackendKind.Claude,      1_000_000)]
        [TestCase("claude-sonnet-5",            BackendKind.Claude,      1_000_000)]
        [TestCase("claude-opus-4-8",            BackendKind.Claude,      1_000_000)]
        [TestCase("claude-opus-4-7",            BackendKind.Claude,      1_000_000)]
        [TestCase("claude-opus-4-6",            BackendKind.Claude,      1_000_000)]
        [TestCase("claude-opus-4-6[1m]",        BackendKind.Claude,      1_000_000)]
        [TestCase("claude-sonnet-4-6",          BackendKind.Claude,      1_000_000)]
        // Claude ≤4.5 — 200k
        [TestCase("claude-opus-4",              BackendKind.Claude,      200_000)]
        [TestCase("claude-opus-4-5-20251101",   BackendKind.Claude,      200_000)]
        [TestCase("claude-sonnet-4-5-20250929", BackendKind.Claude,      200_000)]
        [TestCase("claude-haiku-4-5-20251001",  BackendKind.Claude,      200_000)]
        [TestCase("claude-haiku-4-5",           BackendKind.Claude,      200_000)]
        // GPT-5.4+ — 1.05M
        [TestCase("gpt-5.6-sol",               BackendKind.Codex,       1_050_000)]
        [TestCase("gpt-5.6-terra",             BackendKind.Codex,       1_050_000)]
        [TestCase("gpt-5.6-luna",              BackendKind.Codex,       1_050_000)]
        [TestCase("gpt-5.5",                   BackendKind.Codex,       1_050_000)]
        [TestCase("gpt-5.4",                   BackendKind.Codex,       1_050_000)]
        [TestCase("gpt-5.4-mini",              BackendKind.Codex,       1_050_000)]
        // GPT-5 base — 400k
        [TestCase("gpt-5",                     BackendKind.Codex,       400_000)]
        [TestCase("gpt-5-mini",                BackendKind.Codex,       400_000)]
        // GPT-4.1 — 1M
        [TestCase("gpt-4.1",                   BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-4.1-mini",              BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-4.1-nano",              BackendKind.Codex,       1_000_000)]
        // GPT-4o — 128k
        [TestCase("gpt-4o",                    BackendKind.Claude,      128_000)]
        [TestCase("gpt-4o-mini",               BackendKind.Claude,      128_000)]
        [TestCase("gpt-4-turbo",               BackendKind.Claude,      128_000)]
        // o3 / o4 — 200k
        [TestCase("o3",                        BackendKind.Codex,       200_000)]
        [TestCase("o3-pro",                    BackendKind.Codex,       200_000)]
        [TestCase("o4-mini",                   BackendKind.Codex,       200_000)]
        // Gemini — 1M
        [TestCase("gemini-2.5-flash",          BackendKind.Antigravity, 1_000_000)]
        // Kimi — 128k
        [TestCase("kimi-k2",                   BackendKind.Kimi,        128_000)]
        [TestCase("moonshot-v1",               BackendKind.Kimi,        128_000)]
        // Codex CLI — 192k
        [TestCase("codex-1",                   BackendKind.Codex,       192_000)]
        // Fallbacks
        [TestCase("unknown-model",             BackendKind.Claude,      200_000)]
        [TestCase("unknown-model",             BackendKind.Codex,       1_000_000)]
        [TestCase("unknown-model",             BackendKind.OpenCode,    0)]
        [TestCase("",                          BackendKind.Claude,      200_000)]
        [TestCase(null,                        BackendKind.Claude,      200_000)]
        public void GetContextWindow_ReturnsExpected(string model, BackendKind kind, int expected)
        {
            Assert.AreEqual(expected, ModelContextWindows.GetContextWindow(model, kind));
        }

        [Test]
        public void GetContextWindow_OverridePresent_ReturnsOverride()
        {
            ModelContextWindows.SetOverrides(new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
                { { "my-custom-model", 999_000 } });
            try
            {
                Assert.AreEqual(999_000, ModelContextWindows.GetContextWindow("my-custom-model", BackendKind.Claude));
            }
            finally { ModelContextWindows.SetOverrides(null); }
        }

        [Test]
        public void GetContextWindow_EmptyOverrides_FallsToPattern()
        {
            ModelContextWindows.SetOverrides(new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase));
            try
            {
                Assert.AreEqual(1_000_000, ModelContextWindows.GetContextWindow("claude-opus-4-6", BackendKind.Claude));
            }
            finally { ModelContextWindows.SetOverrides(null); }
        }

        [Test]
        public void GetContextWindow_OverrideZero_FallsToPattern()
        {
            ModelContextWindows.SetOverrides(new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
                { { "claude-opus-4-6", 0 } });
            try
            {
                Assert.AreEqual(1_000_000, ModelContextWindows.GetContextWindow("claude-opus-4-6", BackendKind.Claude));
            }
            finally { ModelContextWindows.SetOverrides(null); }
        }
    }
}
