// ARC-16 T1: UIFileHelper.ToAbsPath cross-platform prefix-check regression guard.
// Pre-fix this is RED only on the windows-2022 CI leg (Path.GetFullPath returns
// backslash-separated output there, compared against forward-slash-only
// Application.dataPath). macOS/Linux already agree on separator style.
using System.IO;
using NUnit.Framework;
using UnityEngine;

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

        // DEV-65 (B2-P9): every prior uitk_file read test (CommandRouterReadOnlyTests,
        // SessionAuthorizationTests) only ever pointed at Assets/DoesNotExist/... --
        // the ToAbsPath-ok / File.Exists-false branch. The full consumer happy path
        // (ToAbsPath ok -> File.Exists true -> File.ReadAllText -> verbatim content)
        // had zero coverage. Router-level, like CommandRouterReadOnlyTests, so this
        // exercises the real uitk_file dispatch a consumer goes through.
        private const string ReadHappyPathAsset = "Assets/TestsTemp/UIFileHelperPathGuard/ReadHappyPath.uxml";

        private static string Abs(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        [Test]
        public void ReadUIFile_ExistingFile_ReturnsContentVerbatim()
        {
            const string content =
                "<ui:UXML xmlns:ui='UnityEngine.UIElements'><ui:VisualElement name='root'/></ui:UXML>";
            TrackOwnedAsset(ReadHappyPathAsset);
            var abs = Abs(ReadHappyPathAsset);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, content, System.Text.Encoding.UTF8);

            var read = CommandRouter.Process(
                $"{{\"id\":\"read-happy\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{ReadHappyPathAsset}\",\"action\":\"read\"}}}}");

            // ReadUIFile returns the file's text verbatim on success -- no "ok:"
            // wrapper (UIFileHelper.cs ReadUIFile; uitk.py docstring: "action=read:
            // return the file's UTF-8 text verbatim").
            StringAssert.Contains(content, read, read);

            // Double-red (ARC-0a S2.2 arm B): deleting the file must diverge to the
            // file-not-found branch, never the unrelated escapes-Assets branch --
            // proves the assertion above discriminates instead of accepting any outcome.
            File.Delete(abs);
            var afterDelete = CommandRouter.Process(
                $"{{\"id\":\"read-after-delete\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{ReadHappyPathAsset}\",\"action\":\"read\"}}}}");
            StringAssert.Contains("file not found", afterDelete, afterDelete);
            StringAssert.DoesNotContain("escapes Assets", afterDelete, afterDelete);
        }
    }
}
