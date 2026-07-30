// TDD: search_context TCP command — tab-separated scene GO + asset search.
// Tests 1-10: pure-logic, no scene. Tests 11-15: integration, require Unity.
// ExtToCode/ShouldInclude tested on the actual implementations (Chat.CLI) — no duplication.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SearchContextTests : SceneTestBase
    {
        [SetUp]
        public void InvalidateSearchIndex()
        {
            VersionTracker.BumpForTest();
        }

        // ── Pure-logic: ExtToCode (SearchContextPlugin.ExtToCode) ────────────────

        [Test] public void ExtToCode_Script()   => Assert.That(SearchContextPlugin.ExtToCode(".cs"),     Is.EqualTo("cs"));
        [Test] public void ExtToCode_Prefab()   => Assert.That(SearchContextPlugin.ExtToCode(".prefab"), Is.EqualTo("pfb"));
        [Test] public void ExtToCode_Material() => Assert.That(SearchContextPlugin.ExtToCode(".mat"),    Is.EqualTo("mat"));
        [Test] public void ExtToCode_Anim()     => Assert.That(SearchContextPlugin.ExtToCode(".anim"),   Is.EqualTo("anim"));
        [Test] public void ExtToCode_Unknown()  => Assert.That(SearchContextPlugin.ExtToCode(".xyz"),    Is.EqualTo("asset"));

        // ── Pure-logic: ShouldInclude (AssetMentionIndex.ShouldIncludePath — DRY, no duplication) ──

        [Test] public void ShouldInclude_ExcludesMeta() =>
            Assert.IsFalse(AssetMentionIndex.ShouldIncludePath("Assets/Foo.cs.meta"));

        [Test] public void ShouldInclude_ExcludesDll() =>
            Assert.IsFalse(AssetMentionIndex.ShouldIncludePath("Assets/Plugins/foo.dll"));

        [Test] public void ShouldInclude_ExcludesThirdPartyPackages() =>
            Assert.IsFalse(AssetMentionIndex.ShouldIncludePath("Packages/com.unity.textmeshpro/Foo.cs"));

        [Test] public void ShouldInclude_AllowsOwnPackage() =>
            Assert.IsTrue(AssetMentionIndex.ShouldIncludePath("Packages/com.unity-biome-mcp/Editor/Foo.cs"));

        [Test] public void ShouldInclude_AllowsAssets() =>
            Assert.IsTrue(AssetMentionIndex.ShouldIncludePath("Assets/Scripts/Player.cs"));

        // ── Integration: SearchHelper.SearchContext (delegates to SearchContextPlugin) ──

        [Test]
        public void SearchContext_EmptyQuery_ReturnsCatalog()
        {
            var result = SearchHelper.SearchContext("");
            Assert.IsNotNull(result);
        }

        [Test]
        public void SearchContext_OutputFormat()
        {
            var go = new GameObject("CtxFmtTest");
            try
            {
                var result = SearchHelper.SearchContext("CtxFmtTest", limit: 5);
                Assert.IsNotEmpty(result.Trim());
                foreach (var line in result.Split('\n'))
                    if (line.Length > 0)
                        Assert.That(line.Split('\t').Length, Is.EqualTo(3), $"Line not 3 columns: '{line}'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SearchContext_GOPrefix()
        {
            var go = new GameObject("CtxGoTest");
            try
            {
                var result = SearchHelper.SearchContext("CtxGoTest", limit: 5);
                Assert.That(result, Does.StartWith("go\t"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SearchContext_LimitRespected()
        {
            string prefix = "Limit_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var gos = new GameObject[5];
            for (int i = 0; i < 5; i++)
                gos[i] = new GameObject(prefix + i);
            try
            {
                var result = SearchHelper.SearchContext(prefix, limit: 3);
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Assert.That(lines.Length, Is.EqualTo(3));
            }
            finally
            {
                foreach (var g in gos)
                    UnityEngine.Object.DestroyImmediate(g);
            }
        }

        [Test]
        public void SearchContext_TypesFilter_GoOnly()
        {
            string unique = "GoOnly_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var go = new GameObject(unique);
            try
            {
                var result = SearchHelper.SearchContext(unique, limit: 5, types: "go");
                Assert.IsNotEmpty(result.Trim());
                foreach (var line in result.Split('\n'))
                    if (line.Length > 0)
                        Assert.That(line.Split('\t')[0], Is.EqualTo("go"),
                            $"Non-go line in go-only result: '{line}'");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
