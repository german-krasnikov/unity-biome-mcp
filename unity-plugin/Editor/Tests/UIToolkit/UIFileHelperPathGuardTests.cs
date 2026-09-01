// ARC-16 T1: UIFileHelper.ToAbsPath cross-platform prefix-check regression guard.
// Pre-fix this is RED only on the windows-2022 CI leg (Path.GetFullPath returns
// backslash-separated output there, compared against forward-slash-only
// Application.dataPath). macOS/Linux already agree on separator style.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIFileHelperPathGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TestCase("Assets/Foo.uxml")]
        [TestCase("Assets/Nested/Deep/Widget.uss")]
        [TestCase("Assets/With Space/File.uxml")]
        [TestCase("Assets/Widget.UXML")]
        public void ToAbsPath_LegitimateNestedAssetsPath_NeverReturnsEscapesAssets(string assetPath)
        {
            var abs = UIFileHelper.ToAbsPath(assetPath, out var err);
            Assert.IsNull(err, err);
            Assert.IsNotNull(abs);
        }
    }
}
