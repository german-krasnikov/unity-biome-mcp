using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ModelContextWindowsTests
    {
        [TestCase("claude-opus-4",     BackendKind.Claude,      200_000)]
        [TestCase("claude-sonnet-4-6", BackendKind.Claude,      200_000)]
        [TestCase("claude-haiku-4-5",  BackendKind.Claude,      200_000)]
        [TestCase("gpt-4o",            BackendKind.Claude,      128_000)]
        [TestCase("gpt-4-turbo",       BackendKind.Claude,      128_000)]
        [TestCase("gemini-2.5-flash",  BackendKind.Antigravity, 1_000_000)]
        [TestCase("kimi-k2",           BackendKind.Kimi,        128_000)]
        [TestCase("moonshot-v1",       BackendKind.Kimi,        128_000)]
        [TestCase("codex-1",           BackendKind.Codex,       192_000)]
        [TestCase("claude-fable-5",    BackendKind.Claude,      1_000_000)]
        [TestCase("gpt-5.5",           BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-5.4",           BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-5.4-mini",      BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-4.1",           BackendKind.Codex,       1_000_000)]
        [TestCase("gpt-4.1-mini",      BackendKind.Codex,       1_000_000)]
        [TestCase("o3-pro",            BackendKind.Codex,       200_000)]
        [TestCase("o3",                BackendKind.Codex,       200_000)]
        [TestCase("o4-mini",           BackendKind.Codex,       200_000)]
        [TestCase("unknown-model",     BackendKind.Claude,      200_000)]
        [TestCase("unknown-model",     BackendKind.Codex,       1_000_000)]
        [TestCase("unknown-model",     BackendKind.OpenCode,    0)]
        [TestCase("",                  BackendKind.Claude,      200_000)]
        [TestCase(null,                BackendKind.Claude,      200_000)]
        public void GetContextWindow_ReturnsExpected(string model, BackendKind kind, int expected)
        {
            Assert.AreEqual(expected, ModelContextWindows.GetContextWindow(model, kind));
        }
    }
}
