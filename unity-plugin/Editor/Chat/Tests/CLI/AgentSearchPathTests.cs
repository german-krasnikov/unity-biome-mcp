using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentSearchPathTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Helper — expected ".claude/agents" path under a base dir
        private static string A(string baseDir) =>
            Path.Combine(baseDir, ".claude", "agents");

        // ── Nested projectRoot yields nearest-first ancestors ─────────────────

        [Test]
        public void Resolve_NestedProject_FirstEntryIsProjectRoot()
        {
            var root   = Path.Combine("grandparent", "parent", "project");
            var home   = Path.Combine("home", "user");
            var result = AgentSearchPath.Resolve(root, home);
            Assert.AreEqual(A(root), result[0]);
        }

        [Test]
        public void Resolve_NestedProject_ContainsParentAndGrandparent()
        {
            var root      = Path.Combine("gp", "parent", "project");
            var parent    = Path.Combine("gp", "parent");
            var gp        = "gp";
            var home      = Path.Combine("home", "user");
            var result    = AgentSearchPath.Resolve(root, home);
            Assert.Contains(A(parent), result);
            Assert.Contains(A(gp),     result);
        }

        [Test]
        public void Resolve_NestedProject_LastEntryIsHome()
        {
            var root   = Path.Combine("gp", "parent", "project");
            var home   = Path.Combine("home", "user");
            var result = AgentSearchPath.Resolve(root, home);
            Assert.AreEqual(A(home), result[result.Count - 1]);
        }

        // ── No duplicates ─────────────────────────────────────────────────────

        [Test]
        public void Resolve_HomeSameAsAncestor_NoDuplicates()
        {
            // If home happens to coincide with an ancestor, no duplicate entry.
            var root   = Path.Combine("home", "user", "project");
            var home   = Path.Combine("home", "user");
            var result = AgentSearchPath.Resolve(root, home);
            var unique = result.Distinct().ToList();
            Assert.AreEqual(unique.Count, result.Count, "Duplicate entries found");
        }

        // ── Null/empty projectRoot ────────────────────────────────────────────

        [Test]
        public void Resolve_NullProjectRoot_ReturnsHomeEntryOnly()
        {
            var home   = Path.Combine("home", "user");
            var result = AgentSearchPath.Resolve(null, home);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(A(home), result[0]);
        }

        [Test]
        public void Resolve_EmptyProjectRoot_ReturnsHomeEntryOnly()
        {
            var home   = Path.Combine("home", "user");
            var result = AgentSearchPath.Resolve("", home);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(A(home), result[0]);
        }

        // ── Null/empty homeDir ────────────────────────────────────────────────

        [Test]
        public void Resolve_NullHomeDir_NoTrailingHomeEntry()
        {
            var root   = Path.Combine("gp", "parent", "project");
            var result = AgentSearchPath.Resolve(root, null);
            Assert.IsFalse(result.Any(p => p.Contains(Path.Combine(".claude", "agents")) &&
                                           p.StartsWith(Path.Combine("home"))));
            // More directly: no entry equals A(home) because home is null
            Assert.AreEqual(A(root), result[0]);
        }

        [Test]
        public void Resolve_EmptyHomeDir_NoTrailingHomeEntry()
        {
            var root   = "project";
            var result = AgentSearchPath.Resolve(root, "");
            Assert.AreEqual(A(root), result[result.Count - 1]);
        }
    }
}
