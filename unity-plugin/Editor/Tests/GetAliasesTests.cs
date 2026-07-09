// TDD Phase 3: C# aliases section — BuildAliasSection + get_aliases command.
// Tests: BuildAliasSection format, NoConfig/EmptyAliases guards, GetAliasesText bare lines,
// allowedDuringCompile flag, ExecGetHierarchy prepend.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetAliasesTests
    {
        private string _configPath;

        [SetUp]
        public void SetUp()
        {
            TestPaths.EnsureFolder();
            _configPath = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_configPath != null)
                AssetDatabase.DeleteAsset(_configPath);
            _configPath = null;
        }

        private void CreateConfig(List<QueryAlias> aliases)
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.aliases = aliases;
            _configPath = TestPaths.TempFolder + "/GetAliasesTest_PlaytestConfig.asset";
            AssetDatabase.CreateAsset(config, _configPath);
            AssetDatabase.SaveAssets();
        }

        // ── BuildAliasSection ───────────────────────────────────────────────

        [Test]
        public void BuildAliasSection_NoConfig_ReturnsNull()
        {
            // Guard: skip if real PlaytestConfig exists in project outside tests
            Assume.That(AssetDatabase.FindAssets("t:PlaytestConfig").Length, Is.Zero,
                "PlaytestConfig exists in project — test inconclusive");
            var result = CommandRouter.BuildAliasSection();
            Assert.IsNull(result);
        }

        [Test]
        public void BuildAliasSection_EmptyAliasesList_ReturnsNull()
        {
            CreateConfig(new List<QueryAlias>());
            var result = CommandRouter.BuildAliasSection();
            Assert.IsNull(result);
        }

        [Test]
        public void BuildAliasSection_SingleAlias_ReturnsFormattedBlock()
        {
            CreateConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", path = "/Player", component = "HP", field = "health" }
            });
            var result = CommandRouter.BuildAliasSection();
            Assert.IsNotNull(result);
            StringAssert.StartsWith("--- ALIASES ---", result);
            StringAssert.Contains("hp=/Player|HP|health", result);
            StringAssert.EndsWith("---", result);
        }

        [Test]
        public void BuildAliasSection_MultipleAliases_AllPresent()
        {
            CreateConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp",    path = "/Player", component = "HP",       field = "health"   },
                new QueryAlias { alias = "speed", path = "/Player", component = "Rigidbody", field = "velocity" }
            });
            var result = CommandRouter.BuildAliasSection();
            StringAssert.Contains("hp=/Player|HP|health", result);
            StringAssert.Contains("speed=/Player|Rigidbody|velocity", result);
        }

        [Test]
        public void BuildAliasSection_EmptyField_IncludesTrailingPipes()
        {
            CreateConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "player", path = "/GridPlayer", component = "GridPlayer", field = "" }
            });
            var result = CommandRouter.BuildAliasSection();
            StringAssert.Contains("player=/GridPlayer|GridPlayer|", result);
        }

        [Test]
        public void BuildAliasSection_Block_HasHeaderAndFooter()
        {
            CreateConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "a", path = "/X", component = "C", field = "f" }
            });
            var result = CommandRouter.BuildAliasSection();
            var lines = result.Split('\n');
            Assert.AreEqual("--- ALIASES ---", lines[0]);
            Assert.AreEqual("---", lines[lines.Length - 1]);
        }

        // ── get_aliases command ─────────────────────────────────────────────

        [Test]
        public void GetAliases_NoConfig_ReturnsNoAliasesMessage()
        {
            Assume.That(AssetDatabase.FindAssets("t:PlaytestConfig").Length, Is.Zero,
                "PlaytestConfig exists — test inconclusive");
            CommandRegistry.Clear();
            CommandRouter.RegisterReadCommands();
            try
            {
                var result = CommandRegistry.Execute("get_aliases", "{}");
                Assert.AreEqual("no aliases", result);
            }
            finally
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            }
        }

        [Test]
        public void GetAliases_WithConfig_ReturnsBareLines()
        {
            CreateConfig(new List<QueryAlias>
            {
                new QueryAlias { alias = "hp", path = "/Player", component = "HP", field = "health" }
            });
            CommandRegistry.Clear();
            CommandRouter.RegisterReadCommands();
            try
            {
                var result = CommandRegistry.Execute("get_aliases", "{}");
                StringAssert.DoesNotContain("--- ALIASES ---", result);
                StringAssert.DoesNotContain("---", result);
                StringAssert.Contains("hp=/Player|HP|health", result);
            }
            finally
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            }
        }

        [Test]
        public void GetAliases_IsRegisteredInReadCommands()
        {
            CommandRegistry.Clear();
            CommandRouter.RegisterReadCommands();
            try
            {
                CollectionAssert.Contains(CommandRegistry.GetAllCommands(), "get_aliases");
            }
            finally
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            }
        }

        [Test]
        public void GetAliases_IsAllowedDuringCompile()
        {
            CommandRegistry.Clear();
            CommandRouter.RegisterReadCommands();
            try
            {
                Assert.IsTrue(CommandRegistry.IsAllowedDuringCompile("get_aliases"));
            }
            finally
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            }
        }
    }
}
