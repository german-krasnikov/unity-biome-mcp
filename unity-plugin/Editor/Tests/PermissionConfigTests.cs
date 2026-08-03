using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>Tests for PermissionConfig — deny-set, EditorPrefs-backed.</summary>
    [TestFixture]
    public class PermissionConfigTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // 2 categories, 4 tools — injected via testable ctor.
        private static readonly Dictionary<string, string[]> FakeCatalog =
            new Dictionary<string, string[]>
            {
                { "CAT_A", new[] { "tool_a1", "tool_a2" } },
                { "CAT_B", new[] { "tool_b1", "tool_b2" } },
            };

        private PermissionConfig Make(Dictionary<string, string[]> catalog = null)
        {
            var prefix = "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_";
            var effectiveCatalog = catalog ?? FakeCatalog;
            ProtectPermissionPrefs(prefix, effectiveCatalog);
            return new PermissionConfig(prefix, () => effectiveCatalog);
        }

        private void ProtectPermissionPrefs(
            string prefix,
            Dictionary<string, string[]> catalog)
        {
            foreach (var tool in catalog.Values.SelectMany(values => values).Distinct())
                ProtectEditorPrefBool(prefix + tool);
        }

        [Test]
        public void GetAllowedToolIds_NoDenials_ReturnsNull()
        {
            // null → caller emits compact MCP_BLANKET arg instead of ~88 ids.
            Assert.IsNull(Make().GetAllowedToolIds());
        }

        [Test]
        public void GetAllowedToolIds_OneDenied_ExcludesDeniedAndIncludesRest()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            var allowed = cfg.GetAllowedToolIds();
            Assert.IsNotNull(allowed);
            CollectionAssert.DoesNotContain(allowed, "tool_a1");
            CollectionAssert.Contains(allowed, "tool_a2");
            CollectionAssert.Contains(allowed, "tool_b1");
            CollectionAssert.Contains(allowed, "tool_b2");
        }

        [Test]
        public void SetToolAllowed_False_PersistsAcrossNewInstance()
        {
            var prefix = "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_";
            ProtectPermissionPrefs(prefix, FakeCatalog);
            var cfg1 = new PermissionConfig(prefix, () => FakeCatalog);
            var cfg2 = new PermissionConfig(prefix, () => FakeCatalog);
            cfg1.SetToolAllowed("tool_b2", false);
            CollectionAssert.DoesNotContain(cfg2.GetAllowedToolIds(), "tool_b2");
        }

        [Test]
        public void SetCategoryAllowed_False_DeniesAllToolsInCategory()
        {
            var cfg = Make();
            cfg.SetCategoryAllowed("CAT_B", false);
            var allowed = cfg.GetAllowedToolIds();
            CollectionAssert.DoesNotContain(allowed, "tool_b1");
            CollectionAssert.DoesNotContain(allowed, "tool_b2");
            CollectionAssert.Contains(allowed, "tool_a1");
        }

        [Test]
        public void AllowAll_AfterDenies_ReturnsNull()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            cfg.SetToolAllowed("tool_b1", false);
            cfg.AllowAll();
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        [Test]
        public void GetToolStates_ReturnsAllToolsWithCategoryAndAllowedFlag()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a2", false);
            var states = cfg.GetToolStates();
            Assert.AreEqual(4, states.Count);
            var a2 = states.First(s => s.toolName == "tool_a2");
            Assert.AreEqual("CAT_A", a2.category);
            Assert.IsFalse(a2.allowed);
            var b1 = states.First(s => s.toolName == "tool_b1");
            Assert.IsTrue(b1.allowed);
        }

        [Test]
        public void GetAllowedToolIds_AllDenied_ReturnsEmptyArray()
        {
            var cfg = Make();
            cfg.DenyAll();
            var ids = cfg.GetAllowedToolIds();
            Assert.IsNotNull(ids, "DenyAll → empty array (not null) signals explicit deny-all");
            Assert.AreEqual(0, ids.Length);
        }

        [Test]
        public void GetAllowedToolIds_EmptyCatalog_ReturnsNull()
        {
            var cfg = new PermissionConfig(
                "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_",
                () => new Dictionary<string, string[]>());
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        [Test]
        public void SetCategoryAllowed_UnknownCategory_IsNoOp()
        {
            var cfg = Make();
            cfg.SetCategoryAllowed("NONEXISTENT", false);
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        [Test]
        public void LiveCatalog_DoesNotOverwriteExistingPluginsCategory()
        {
            var catalog = new Dictionary<string, string[]>
            {
                { "CAT_A",   new[] { "tool_a1" } },
                { "PLUGINS", new[] { "existing_plugin" } },
            };
            var prefix = "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_";
            ProtectPermissionPrefs(prefix, catalog);
            var cfg = new PermissionConfig(
                prefix,
                () => catalog);
            var pluginTools = cfg.GetToolStates()
                .Where(s => s.category == "PLUGINS")
                .Select(s => s.toolName)
                .ToArray();
            CollectionAssert.Contains(pluginTools, "existing_plugin");
        }

        [Test]
        public void DefaultPrefix_Constant_MatchesDefaultCtorBehavior()
        {
            // Two explicit-DEFAULT_PREFIX instances share EditorPrefs storage.
            ProtectPermissionPrefs(PermissionConfig.DEFAULT_PREFIX, FakeCatalog);
            var cfg1 = new PermissionConfig(PermissionConfig.DEFAULT_PREFIX, () => FakeCatalog);
            var cfg2 = new PermissionConfig(PermissionConfig.DEFAULT_PREFIX, () => FakeCatalog);
            cfg1.SetToolAllowed("tool_a1", false);
            CollectionAssert.DoesNotContain(cfg2.GetAllowedToolIds(), "tool_a1");
        }

        [Test]
        public void MCP_BLANKET_DerivesFromServerName()
        {
            // Drift guard: MCP_BLANKET must be built FROM SERVER_NAME, not an
            // independently-hardcoded literal that merely happens to agree with it
            // today (it didn't — MCP_BLANKET was "mcp__unity", wrong under the
            // current "unity-biome-mcp" naming scheme). Deriving the
            // expected value from the constant proves the DRY coupling. Mirrors
            // backend_def.py's MCP_BLANKET = f"mcp__{SERVER_NAME}" (NOT
            // .replace('-', '_') — hyphens are legal in MCP tool-name segments and
            // are never touched by Claude's sanitizer).
            Assert.AreEqual("mcp__" + PermissionConfig.SERVER_NAME, PermissionConfig.MCP_BLANKET);
            Assert.AreEqual("mcp__unity-biome-mcp", PermissionConfig.MCP_BLANKET);
        }

        [Test]
        public void DefaultCtor_UsesDefaultPrefix_SharingStorageWithExplicitDefaultPrefix()
        {
            // Fails if parameterless ctor drifts to a different prefix (e.g. "..._v2")
            // while leaving DEFAULT_PREFIX constant unchanged.
            var defaultCfg = new PermissionConfig(); // production path: DEFAULT_PREFIX + LiveCatalog

            var states = defaultCfg.GetToolStates();
            foreach (var state in states)
                ProtectEditorPrefBool(PermissionConfig.DEFAULT_PREFIX + state.toolName);
            var firstTool = states.FirstOrDefault().toolName;
            Assume.That(firstTool, Is.Not.Null.And.Not.Empty, "Live catalog must be non-empty");

            defaultCfg.SetToolAllowed(firstTool, false);

            // Readback via explicit DEFAULT_PREFIX — a different prefix means denial is invisible.
            var readback = new PermissionConfig(
                PermissionConfig.DEFAULT_PREFIX,
                () => new Dictionary<string, string[]> { { "TEST", new[] { firstTool } } });

            // If the ctor used a different prefix, no denial is seen → GetAllowedToolIds returns null.
            // If it correctly used DEFAULT_PREFIX, the denial is visible → returns non-null array
            // that does NOT contain firstTool (it is denied).
            var readIds = readback.GetAllowedToolIds();
            Assert.IsNotNull(readIds,
                "Parameterless ctor must use DEFAULT_PREFIX; null means the denial was not seen");
            CollectionAssert.DoesNotContain(readIds, firstTool,
                "firstTool was denied via the parameterless ctor; readback must not include it");
        }
    }
}
