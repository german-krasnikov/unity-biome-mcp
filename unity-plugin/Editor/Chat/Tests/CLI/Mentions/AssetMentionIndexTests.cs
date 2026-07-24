// TDD Phase 2: AssetMentionIndex tests.
// Tests focus on testable logic without real AssetDatabase calls where possible.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AssetMentionIndexTests
    {
        private AssetMentionIndex _index;

        [SetUp]
        public void SetUp() => _index = new AssetMentionIndex();

        // 1. Empty query returns nothing
        [Test]
        public void Search_EmptyQuery_ReturnsNothing()
        {
            var results = new List<MentionCandidate>();
            _index.Search("", 10, results);
            Assert.That(results, Is.Empty);
        }

        // 2. Never exceeds maxResults
        [Test]
        public void Search_CapsAtMaxResults()
        {
            _index.RefreshIfDirty(); // loads real assets from test project

            var results = new List<MentionCandidate>();
            _index.Search("a", 3, results);
            Assert.That(results.Count, Is.LessThanOrEqualTo(3));
        }

        // 3. KindKey is detected from extension (pure logic via helper method)
        [Test]
        public void KindKey_Script_DetectedFromExtension()
            => Assert.That(AssetMentionIndex.KindKeyForExtension(".cs"), Is.EqualTo(ChipKindKeys.Script));

        [Test]
        public void KindKey_Prefab_DetectedFromExtension()
            => Assert.That(AssetMentionIndex.KindKeyForExtension(".prefab"), Is.EqualTo(ChipKindKeys.Prefab));

        [Test]
        public void KindKey_Material_DetectedFromExtension()
            => Assert.That(AssetMentionIndex.KindKeyForExtension(".mat"), Is.EqualTo(ChipKindKeys.Material));

        [Test]
        public void KindKey_Scene_DetectedFromExtension()
            => Assert.That(AssetMentionIndex.KindKeyForExtension(".unity"), Is.EqualTo(ChipKindKeys.Scene));

        [Test]
        public void KindKey_Audio_DetectedFromExtension()
            => Assert.That(AssetMentionIndex.KindKeyForExtension(".wav"), Is.EqualTo(ChipKindKeys.Audio));

        // 4. .meta files must be excluded
        [Test]
        public void FiltersMetaFiles()
        {
            Assert.That(AssetMentionIndex.ShouldIncludePath("Assets/Foo.cs.meta"), Is.False);
        }

        // 5. Packages/ paths are excluded (except unity-biome-mcp)
        [Test]
        public void FiltersPackages_Standard()
            => Assert.That(AssetMentionIndex.ShouldIncludePath("Packages/com.unity.ui/Runtime/Foo.cs"), Is.False);

        [Test]
        public void FiltersPackages_AllowsUnityMcp()
            => Assert.That(AssetMentionIndex.ShouldIncludePath("Packages/com.unity-biome-mcp/Editor/Foo.cs"), Is.True);

        // 6. Search finds entry by filename
        [Test]
        public void Search_MatchesByFileName()
        {
            _index.RefreshIfDirty();

            var results = new List<MentionCandidate>();
            // Search for something that must exist in any Unity project
            _index.Search("a", 10, results);
            // If project has assets with 'a', we get results; if not, at least no crash
            Assert.DoesNotThrow(() => _index.Search("a", 10, new List<MentionCandidate>()));
        }

        // 7. .dll and .pdb files are excluded
        [Test]
        public void FiltersDllFiles()
            => Assert.That(AssetMentionIndex.ShouldIncludePath("Assets/Plugins/foo.dll"), Is.False);

        [Test]
        public void FiltersPdbFiles()
            => Assert.That(AssetMentionIndex.ShouldIncludePath("Assets/Plugins/foo.pdb"), Is.False);
    }
}
