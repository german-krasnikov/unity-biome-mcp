// TDD — RED first. Tests drive ChatBinaryResolver negative-cache and seam contract.
// Requires UNITY_INCLUDE_TESTS define (Chat.Tests asmdef enforces it).
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatBinaryResolverTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ChatBinaryResolver.WhichOverride = null;
            RegisterCleanup(ChatBinaryResolver.ResetCacheForTests);
            DeleteEditorPrefString(ChatBinaryResolver.PrefKey);
            DeleteEditorPrefString(ChatBinaryResolver.CodexPrefKey);
            DeleteEditorPrefString(ChatBinaryResolver.KimiPrefKey);
            DeleteEditorPrefString(ChatBinaryResolver.OpenCodePrefKey);
            DeleteEditorPrefString(ChatBinaryResolver.GeminiPrefKey);
            ChatBinaryResolver.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
        }

        [Test]
        public void Resolve_PrefOverride_SkipsWhich()
        {
            var invoked = false;
            ChatBinaryResolver.WhichOverride = _ => { invoked = true; return "/usr/local/bin/claude"; };

            SetEditorPrefString(ChatBinaryResolver.PrefKey, "/custom/claude");
            var result = ChatBinaryResolver.Resolve();

            Assert.AreEqual("/custom/claude", result);
            Assert.IsFalse(invoked, "WhichOverride must not be called when EditorPrefs override is set");
        }

        [Test]
        public void Resolve_NegativeCache_DoesNotReprobe()
        {
            var callCount = 0;
            ChatBinaryResolver.WhichOverride = _ => { callCount++; return null; };

            ChatBinaryResolver.Resolve(); // first probe
            ChatBinaryResolver.Resolve(); // should hit cache

            Assert.AreEqual(1, callCount, "WhichOverride must only be called once after a null result");
        }

        [Test]
        public void Resolve_ForceRefresh_Reprobes()
        {
            var callCount = 0;
            ChatBinaryResolver.WhichOverride = _ => { callCount++; return null; };

            ChatBinaryResolver.Resolve();                    // probe → callCount==1
            ChatBinaryResolver.Resolve(forceRefresh: true);  // bust cache → callCount==2

            Assert.AreEqual(2, callCount, "forceRefresh:true must re-invoke WhichOverride");
        }

        // ── Per-backend EditorPrefs override (R1) ────────────────────────────────

        [Test]
        public void Resolve_Uv_GoesToWhichOverride()
        {
            // WhichOverride seam is invoked for arbitrary binary names
            string received = null;
            ChatBinaryResolver.WhichOverride = b => { received = b; return "/usr/local/bin/uv"; };

            var result = ChatBinaryResolver.Resolve("uv");

            Assert.AreEqual("uv", received);
            Assert.AreEqual("/usr/local/bin/uv", result);
        }

        [Test]
        public void Resolve_Codex_EditorPrefOverride_SkipsWhich()
        {
            var invoked = false;
            ChatBinaryResolver.WhichOverride = _ => { invoked = true; return null; };

            SetEditorPrefString(ChatBinaryResolver.CodexPrefKey, "/custom/codex.cmd");
            var result = ChatBinaryResolver.Resolve("codex");

            Assert.AreEqual("/custom/codex.cmd", result);
            Assert.IsFalse(invoked, "WhichOverride must not be called when Codex EditorPrefs override is set");
        }

        [Test]
        public void Resolve_Codex_NoOverride_UsesWhich()
        {
            ChatBinaryResolver.WhichOverride = b => "/found/" + b;

            var result = ChatBinaryResolver.Resolve("codex");

            Assert.AreEqual("/found/codex", result);
        }

        // CH1.arch.3: per-binary negative cache — arbitrary binary must not re-probe (fix test)
        [Test]
        public void Resolve_Codex_NegativeCache_DoesNotReprobe()
        {
            var callCount = 0;
            ChatBinaryResolver.WhichOverride = _ => { callCount++; return null; };

            ChatBinaryResolver.Resolve("codex"); // first probe → callCount==1
            ChatBinaryResolver.Resolve("codex"); // must hit cache → callCount stays 1

            Assert.AreEqual(1, callCount, "WhichOverride must only be called once for the same binary (negative cache)");
        }

        [Test]
        public void Resolve_DifferentBinaries_ProbeEachOnce()
        {
            var calls = new System.Collections.Generic.List<string>();
            ChatBinaryResolver.WhichOverride = b => { calls.Add(b); return null; };

            ChatBinaryResolver.Resolve("codex");
            ChatBinaryResolver.Resolve("codex"); // should not re-probe
            ChatBinaryResolver.Resolve("uv");
            ChatBinaryResolver.Resolve("uv");    // should not re-probe

            Assert.AreEqual(2, calls.Count, "two distinct binaries must each be probed exactly once");
            CollectionAssert.Contains(calls, "codex");
            CollectionAssert.Contains(calls, "uv");
        }

        // ── PickWindowsPath ───────────────────────────────────────────────────

        [Test]
        public void PickWindowsPath_ExeWinsOverCmdAndShim()
        {
            // extensionless shim line must be rejected; .exe beats .cmd
            var input = "codex\nC:\\npm\\codex.cmd\nC:\\npm\\codex.exe\n";
            Assert.AreEqual("C:\\npm\\codex.exe", ChatBinaryResolver.PickWindowsPath(input));
        }

        [Test]
        public void PickWindowsPath_OnlyCmd_ReturnsCmdLine()
        {
            var input = "C:\\npm\\codex.cmd\n";
            Assert.AreEqual("C:\\npm\\codex.cmd", ChatBinaryResolver.PickWindowsPath(input));
        }

        [Test]
        public void PickWindowsPath_OnlyShim_ReturnsNull()
        {
            // extensionless bash-shim line → no .exe or .cmd → null
            var input = "codex\n";
            Assert.IsNull(ChatBinaryResolver.PickWindowsPath(input));
        }

        [Test]
        public void PickWindowsPath_EmptyOutput_ReturnsNull()
        {
            Assert.IsNull(ChatBinaryResolver.PickWindowsPath(""));
        }

        // ── PickLinuxPath ─────────────────────────────────────────────────────

        [Test]
        public void PickLinuxPath_BannerThenPath_ReturnsPath()
        {
            // Interactive .bashrc banner precedes the real path
            var input = "bash: no job control in this shell\n/usr/local/bin/codex\n";
            Assert.AreEqual("/usr/local/bin/codex", ChatBinaryResolver.PickLinuxPath(input));
        }

        [Test]
        public void PickLinuxPath_TrailingBannerAfterPath_StillReturnsSlashLine()
        {
            // Last /‑prefixed line wins even if a banner follows (no / prefix on banner)
            var input = "/usr/local/bin/codex\nsome trailing message\n";
            Assert.AreEqual("/usr/local/bin/codex", ChatBinaryResolver.PickLinuxPath(input));
        }

        [Test]
        public void PickLinuxPath_NoSlashLine_ReturnsNull()
        {
            var input = "bash: no job control\ncommand not found\n";
            Assert.IsNull(ChatBinaryResolver.PickLinuxPath(input));
        }

        [Test]
        public void PickLinuxPath_EmptyOutput_ReturnsNull()
        {
            Assert.IsNull(ChatBinaryResolver.PickLinuxPath(""));
        }

        // ── macOS .zshrc banner handling (via PickLinuxPath) ──────────────────

        [Test]
        public void PickLinuxPath_ZshrcBannerThenKimiPath_ReturnsPath()
        {
            // .zshrc may print a banner before command -v outputs the path
            var input = "Welcome to kimi-code!\n/Users/me/.kimi-code/bin/kimi\n";
            Assert.AreEqual("/Users/me/.kimi-code/bin/kimi", ChatBinaryResolver.PickLinuxPath(input));
        }

        [Test]
        public void PickLinuxPath_ZshrcBannerThenOpencodeePath_ReturnsPath()
        {
            var input = "opencode configured\n/Users/me/.opencode/bin/opencode\n";
            Assert.AreEqual("/Users/me/.opencode/bin/opencode", ChatBinaryResolver.PickLinuxPath(input));
        }

        [Test]
        public void PickLinuxPath_PathWithSpaces_ReturnsCorrectPath()
        {
            var input = "/Users/my user/.kimi-code/bin/kimi\n";
            Assert.AreEqual("/Users/my user/.kimi-code/bin/kimi", ChatBinaryResolver.PickLinuxPath(input));
        }

    }
}
