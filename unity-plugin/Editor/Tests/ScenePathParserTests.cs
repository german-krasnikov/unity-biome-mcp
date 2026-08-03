using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScenePathParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Parse_WithScene_ReturnsNameAndPath()
        {
            var result = ScenePathParser.Parse("GameplayScene:/Player/Weapon");
            Assert.AreEqual("GameplayScene", result.SceneName);
            Assert.AreEqual("Player/Weapon", result.LocalPath);
        }

        [Test]
        public void Parse_WithoutScene_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse("/Player/Weapon");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("/Player/Weapon", result.LocalPath);
        }

        [Test]
        public void Parse_ColonAtStart_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse(":/SomeObj");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual(":/SomeObj", result.LocalPath);
        }

        [Test]
        public void Parse_Null_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse(null);
            Assert.IsNull(result.SceneName);
            Assert.IsNull(result.LocalPath);
        }

        [Test]
        public void Parse_Empty_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse("");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("", result.LocalPath);
        }

        [Test]
        public void Parse_LeadingSlashStripped()
        {
            var result = ScenePathParser.Parse("Scene://Root/Child");
            Assert.AreEqual("Scene", result.SceneName);
            Assert.AreEqual("Root/Child", result.LocalPath);
        }

        [Test]
        public void Parse_NoLeadingSlash()
        {
            var result = ScenePathParser.Parse("Scene:/Root");
            Assert.AreEqual("Scene", result.SceneName);
            Assert.AreEqual("Root", result.LocalPath);
        }

        // Bracket-name paths must NOT be misread as scene-qualified.
        // "[GAMEPLAY]/[PLACEMENTS]/Repair" — root starts with '[', no scene prefix.
        [Test]
        public void Parse_BracketRootPath_NullScene()
        {
            var result = ScenePathParser.Parse("[GAMEPLAY]/[PLACEMENTS]/Repair");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("[GAMEPLAY]/[PLACEMENTS]/Repair", result.LocalPath);
        }

        // "[GAMEPLAY:COMBAT]/Child" — has ":/" inside bracket, must NOT extract "GAMEPLAY" as scene.
        [Test]
        public void Parse_BracketRootWithColon_NullScene()
        {
            var result = ScenePathParser.Parse("[GAMEPLAY:COMBAT]/Child");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("[GAMEPLAY:COMBAT]/Child", result.LocalPath);
        }

        // Plain "[PLACEMENTS]" root with no children — still not scene-qualified.
        [Test]
        public void Parse_BracketRootOnly_NullScene()
        {
            var result = ScenePathParser.Parse("[PLACEMENTS]");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("[PLACEMENTS]", result.LocalPath);
        }
    }
}
