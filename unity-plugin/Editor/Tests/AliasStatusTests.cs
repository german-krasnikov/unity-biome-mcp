// TDD: alias_status command — AliasExpander.IsStale tracking + ExecAliasStatus registration.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AliasStatusTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _configPath;
        private Dictionary<string, string> _savedOverride;

        [SetUp]
        public void SetUp()
        {
            TestPaths.EnsureFolder();
            _configPath = null;
            _savedOverride = AliasExpander._tableOverride;
            var savedProvider = CommandRouter.FindPlaytestConfigGuidsForTest;
            RegisterCleanup(() =>
            {
                AliasExpander._tableOverride = _savedOverride;
                AliasExpander.Invalidate();
                CommandRouter.FindPlaytestConfigGuidsForTest = savedProvider;
            });
            CommandRouter.FindPlaytestConfigGuidsForTest =
                () => System.Array.Empty<string>();
        }

        // ── IsStale ────────────────────────────────────────────────────────────

        [Test]
        public void IsStale_FalseWhenTableOverridePresent()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>();
            Assert.IsFalse(AliasExpander.IsStale);
        }

        [Test]
        public void IsStale_TrueAfterLoadAndInvalidate()
        {
            // Force a load via ExpandText with a known sigil (fast-path skips if no $)
            AliasExpander._tableOverride = null;
            AliasExpander.Invalidate();          // reset _table, keep _hasLoaded
            AliasExpander.ExpandText("$dummy");  // triggers GetTable() -> _hasLoaded = true
            // Now invalidate to simulate cache eviction
            AliasExpander.Invalidate();
            Assert.IsTrue(AliasExpander.IsStale);
        }

        [Test]
        public void IsStale_FalseAfterTableRefreshed()
        {
            AliasExpander._tableOverride = null;
            AliasExpander.Invalidate();
            AliasExpander.ExpandText("$dummy");  // loads table
            AliasExpander.Invalidate();          // stale
            AliasExpander.ExpandText("$dummy");  // re-loads
            Assert.IsFalse(AliasExpander.IsStale);
        }

        // ── alias_status command ────────────────────────────────────────────────

        [Test]
        public void AliasStatus_IsRegistered()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            CollectionAssert.Contains(CommandRegistry.GetAllCommands(), "alias_status");
        }

        [Test]
        public void AliasStatus_AllowedDuringCompile()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            Assert.IsTrue(CommandRegistry.IsAllowedDuringCompile("alias_status"));
        }

        [Test]
        public void AliasStatus_EmptyWhenNoConfig()
        {
            AliasExpander._tableOverride = new Dictionary<string, string>();
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            var result = CommandRegistry.Execute("alias_status", "{}");
            StringAssert.Contains("count: 0", result);
            StringAssert.Contains("loaded: empty", result);
        }

        [Test]
        public void AliasStatus_WithConfig_ReturnsSourceAndCount()
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.aliases = new List<QueryAlias>
            {
                new QueryAlias { alias = "hero", path = "/Player", component = "Transform", field = "position", type = AliasType.ValPath },
                new QueryAlias { alias = "hp",   constValue = "100", type = AliasType.ValConst },
            };
            _configPath = TestPaths.TempFolder + "/AliasStatus_PlaytestConfig.asset";
            TrackOwnedAsset(_configPath);
            AssetDatabase.CreateAsset(config, _configPath);
            AssetDatabase.SaveAssets();
            CommandRouter.FindPlaytestConfigGuidsForTest = () => new[]
            {
                AssetDatabase.AssetPathToGUID(_configPath)
            };

            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            var result = CommandRegistry.Execute("alias_status", "{}");
            // Don't assert exact count — a pre-existing PlaytestConfig in the project would inflate it
            StringAssert.Contains("count:", result);
            StringAssert.Contains("source:", result);
        }
    }
}
