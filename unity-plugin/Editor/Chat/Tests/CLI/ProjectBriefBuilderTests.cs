using NUnit.Framework;
using UnityMCP.Editor.Chat.Context;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ProjectBriefBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void ResetOverrides()
        {
            ProjectBriefBuilder.CompileProviderOverride = null;
            ProjectBriefBuilder.ConsoleProviderOverride = null;
            ProjectBriefBuilder.SceneProviderOverride = null;
        }

        [TearDown]
        public void ClearOverrides()
        {
            ProjectBriefBuilder.CompileProviderOverride = null;
            ProjectBriefBuilder.ConsoleProviderOverride = null;
            ProjectBriefBuilder.SceneProviderOverride = null;
        }

        [Test]
        public void Build_ContainsCompileSection_WhenErrorsPresent()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "2 compilation error(s):\nFile.cs:10: error CS0001";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => "";

            var result = ProjectBriefBuilder.Build();

            StringAssert.Contains("[Compile]", result);
            StringAssert.Contains("error", result);
        }

        [Test]
        public void Build_ContainsCleanLabel_WhenNoErrors()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "No compilation errors";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => "";

            var result = ProjectBriefBuilder.Build();

            StringAssert.Contains("[Compile]", result);
            StringAssert.Contains("clean", result);
        }

        [Test]
        public void Build_ContainsHierarchySection()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => "SampleScene (5 nodes)\n  Player";

            var result = ProjectBriefBuilder.Build();

            StringAssert.Contains("[Hierarchy]", result);
            StringAssert.Contains("SampleScene", result);
        }

        [Test]
        public void Build_EmptyConsole_OmitsConsoleSection()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "No compilation errors";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => "SampleScene (5 nodes)";

            var result = ProjectBriefBuilder.Build();

            StringAssert.DoesNotContain("[Console]", result);
        }

        [Test]
        public void Build_TruncatesHierarchy_WhenOverBudget()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => new string('X', 20000);

            var result = ProjectBriefBuilder.Build();

            StringAssert.Contains("…(truncated)", result);
        }

        [Test]
        public void Build_IsIdempotent_SameState()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => "No compilation errors";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => "SampleScene (5 nodes)";

            var result1 = ProjectBriefBuilder.Build();
            var result2 = ProjectBriefBuilder.Build();

            Assert.AreEqual(result1, result2);
        }

        [Test]
        public void Build_SceneProviderOverride_UsedInTests()
        {
            const string fakeScene = "FakeScene (99 nodes)";
            ProjectBriefBuilder.CompileProviderOverride = () => "";
            ProjectBriefBuilder.ConsoleProviderOverride = () => "";
            ProjectBriefBuilder.SceneProviderOverride = () => fakeScene;

            var result = ProjectBriefBuilder.Build();

            StringAssert.Contains(fakeScene, result);
        }

        [Test]
        public void Build_SmallBudget_DoesNotThrow()
        {
            ProjectBriefBuilder.CompileProviderOverride = () => new string('E', 60);
            ProjectBriefBuilder.ConsoleProviderOverride = () => "error: NullRef";
            ProjectBriefBuilder.SceneProviderOverride = () => "";

            Assert.DoesNotThrow(() => ProjectBriefBuilder.Build(budgetChars: 50));
        }

        [Test]
        public void SectionHash_DifferentContent_DifferentHash()
        {
            var hash1 = ProjectBriefBuilder.SectionHash("content A");
            var hash2 = ProjectBriefBuilder.SectionHash("content B");

            Assert.AreNotEqual(hash1, hash2);
        }
    }
}
