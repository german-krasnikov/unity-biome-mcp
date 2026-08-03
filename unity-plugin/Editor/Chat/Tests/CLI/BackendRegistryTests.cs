using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "BackendRegistryTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true);
        }

        private string MakeAgentsDir(string subdir)
        {
            var dir = Path.Combine(_tmpDir, subdir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteAgent(string dir, string filename, string name) =>
            File.WriteAllText(Path.Combine(dir, filename),
                $"---\nname: {name}\ndescription: test\n---\nBody.");

        // ── First entry is always Claude (enabled) ────────────────────────────

        [Test]
        public void Discover_ClaudeIsFirst()
        {
            var result = BackendRegistry.Discover(new string[0]);
            Assert.AreEqual("Claude", result[0].DisplayName);
            Assert.IsNull(result[0].AgentName);
            Assert.IsTrue(result[0].Enabled);
        }

        // ── Last entry is always Kimi (enabled) ──────────────────────────────

        [Test]
        public void Discover_OpenCodeIsLast()
        {
            var result = BackendRegistry.Discover(new string[0]);
            Assert.AreEqual(5, result.Count); // Claude + Codex + Antigravity + Kimi + OpenCode
            var last = result[result.Count - 1];
            Assert.AreEqual("OpenCode",             last.DisplayName);
            Assert.AreEqual(BackendKind.OpenCode,   last.Kind);
            Assert.IsTrue(last.Enabled);
        }

        [Test]
        public void Discover_KimiIsSecondToLast()
        {
            var result = BackendRegistry.Discover(new string[0]);
            var kimi = result[result.Count - 2];
            Assert.AreEqual("Kimi",          kimi.DisplayName);
            Assert.AreEqual(BackendKind.Kimi, kimi.Kind);
            Assert.IsTrue(kimi.Enabled);
        }

        [Test]
        public void Discover_AntigravityIsThirdToLast()
        {
            var result = BackendRegistry.Discover(new string[0]);
            var agy = result[result.Count - 3];
            Assert.AreEqual("Antigravity",           agy.DisplayName);
            Assert.AreEqual(BackendKind.Antigravity, agy.Kind);
            Assert.IsTrue(agy.Enabled);
        }

        // ── Agent files are discovered ────────────────────────────────────────

        [Test]
        public void Discover_ProjectAgentFile_Yields_EnabledSpec()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "code-reviewer.md", "code-reviewer");

            var result = BackendRegistry.Discover(new[] { projDir });

            Assert.AreEqual(6, result.Count); // Claude + code-reviewer + Codex + Antigravity + Kimi + OpenCode
            Assert.AreEqual("code-reviewer", result[1].DisplayName);
            Assert.AreEqual("code-reviewer", result[1].AgentName);
            Assert.IsTrue(result[1].Enabled);
        }

        [Test]
        public void Discover_UserAgentFile_Yields_EnabledSpec()
        {
            var userDir = MakeAgentsDir("user");
            WriteAgent(userDir, "doc-keeper.md", "doc-keeper");

            var result = BackendRegistry.Discover(new[] { userDir });

            Assert.AreEqual(6, result.Count); // Claude + doc-keeper + Codex + Gemini + Kimi + OpenCode
            Assert.AreEqual("doc-keeper", result[1].AgentName);
        }

        // ── Project name shadows same-named user file (dedup) ─────────────────

        [Test]
        public void Discover_ProjectShadowsUserDuplicate()
        {
            var projDir = MakeAgentsDir("proj");
            var userDir = MakeAgentsDir("user");
            WriteAgent(projDir, "reviewer.md", "reviewer");
            WriteAgent(userDir, "reviewer.md", "reviewer"); // same name

            var result = BackendRegistry.Discover(new[] { projDir, userDir }); // nearest-first

            // Only one "reviewer" entry
            var reviewers = result.FindAll(b => b.AgentName == "reviewer");
            Assert.AreEqual(1, reviewers.Count);
        }

        // ── Non-existent dirs don't throw ─────────────────────────────────────

        [Test]
        public void Discover_NonExistentDirs_ReturnsBuiltInBackends()
        {
            var result = BackendRegistry.Discover(new[] { "/nonexistent/path1", "/nonexistent/path2" });

            Assert.AreEqual(5, result.Count);
            Assert.AreEqual("Claude",   result[0].DisplayName);
            Assert.AreEqual("Codex",    result[1].DisplayName);
            Assert.AreEqual("Antigravity", result[2].DisplayName);
            Assert.AreEqual("Kimi",     result[3].DisplayName);
            Assert.AreEqual("OpenCode", result[4].DisplayName);
        }

        // ── Agent named "Claude" is skipped (collision guard) ─────────────────

        [Test]
        public void Discover_AgentNamedClaude_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "claude.md", "Claude");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Claude + Codex + Gemini + Kimi + OpenCode, no extra "Claude" entry
            Assert.AreEqual(5, result.Count);
        }

        // ── Agent named "Codex" is skipped (collision guard) ─────────────────

        [Test]
        public void Discover_AgentNamedCodex_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "codex.md", "Codex");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Exactly one "Codex" entry, and it is the built-in enabled Codex
            var codexEntries = result.FindAll(b => b.DisplayName == "Codex");
            Assert.AreEqual(1, codexEntries.Count);
            Assert.IsTrue(codexEntries[0].Enabled);
        }

        // ── Issue 28: collision guard must cover ALL BackendKind names, not just Claude/Codex —
        // otherwise a custom agent named e.g. "Kimi" could collide with MCPChatWindow's stable
        // persistence id (BackendKind.ToString()) and restore the wrong backend. ──

        [TestCase("Antigravity")]
        [TestCase("Kimi")]
        [TestCase("OpenCode")]
        public void Discover_AgentNamedBuiltInKind_IsSkipped(string builtInName)
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "custom.md", builtInName);

            var result = BackendRegistry.Discover(new[] { projDir });

            var matches = result.FindAll(b => b.DisplayName == builtInName);
            Assert.AreEqual(1, matches.Count, $"exactly one '{builtInName}' entry — the built-in, not a custom collision");
            Assert.IsNull(matches[0].AgentName, "built-in entry must not carry a custom AgentName");
        }

        // ── Issue 28: rename-survival regression guard. MCPChatWindow persists custom agents by
        // AgentName (parsed from frontmatter `name:`), not by the .md filename or DisplayName.
        // Renaming the underlying file (stem changes) while the frontmatter name: is unchanged
        // must keep yielding the SAME AgentName — this is the stable id EditorPrefs restore relies on. ──

        [Test]
        public void Discover_AgentFileRenamed_SameFrontmatterName_AgentNameStable()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "old-filename.md", "code-reviewer");
            var before     = BackendRegistry.Discover(new[] { projDir });
            var beforeSpec = before.Find(b => b.AgentName == "code-reviewer");

            File.Delete(Path.Combine(projDir, "old-filename.md"));
            WriteAgent(projDir, "new-filename.md", "code-reviewer"); // file renamed, frontmatter name: unchanged

            var after     = BackendRegistry.Discover(new[] { projDir });
            var afterSpec = after.Find(b => b.AgentName == "code-reviewer");

            Assert.AreEqual(beforeSpec.AgentName, afterSpec.AgentName,
                "AgentName (stable persistence key) must survive a file rename");
            Assert.AreEqual(beforeSpec.Kind, afterSpec.Kind);
        }
    }
}
