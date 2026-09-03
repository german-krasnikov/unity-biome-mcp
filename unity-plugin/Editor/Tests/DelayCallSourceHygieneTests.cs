// TDD (TICK-DISPATCHER Part C): delayCall is reserved for GUI paths behind an open
// window or a click — everywhere else must marshal through MainThreadDispatcher,
// which reliably drains in a backgrounded Editor (RELAY-FIX, commit 1bcc90b7).
// This is a single allowlist gate replacing per-file StringAssert checks for new
// call sites; the per-file guards that already exist stay as cheap, documented
// call-site-specific regression tests.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DelayCallSourceHygieneTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Mirrors the Python hygiene script's _NON_CODE pattern: strips comments and
        // string/char literals before matching, so prose mentioning "delayCall" in a
        // rationale comment (e.g. RuntimeHelper.cs, PlayModeEpochTracker.cs) is not a
        // false positive — only a real EditorApplication.delayCall reference counts.
        private static readonly Regex NonCode = new Regex(
            "//[^\r\n]*" +
            @"|/\*.*?\*/" +
            "|(?:\\$@|@\\$|@)\"(?:\"\"|[^\"])*\"" +
            "|\\$?\"(?:\\\\.|[^\"\\\\])*\"" +
            "|'(?:\\\\.|[^'\\\\])*'",
            RegexOptions.Singleline);

        private static readonly Regex DelayCallUsage = new Regex(@"EditorApplication\s*\.\s*delayCall");

        // Exact production files allowed to use EditorApplication.delayCall: GUI windows,
        // menu actions, and panels reachable only behind an open window or a click.
        private static readonly HashSet<string> Allowlist = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "PlaytestLaunchWindow.cs",
            "MCPActions.cs",
            "MCPStatusBarWidget.cs",
            "MCPServer.cs", // static ctor defers StartAsync — SynchronizationContext dead in static ctor
            "Wizard/MCPDiagnosePanel.cs",
            "Wizard/Screens/ConfigureScreen.cs",
            "Wizard/Screens/InstallSkillsScreen.cs",
            "Wizard/Screens/PickBackendScreen.cs",
            "Chat/CLI/ReloadGuard.cs",
            "Chat/CLI/RelayWarmup.cs",
        };

        private static bool IsAllowed(string relativePath) =>
            Allowlist.Contains(relativePath) || relativePath.StartsWith("Chat/View/", System.StringComparison.Ordinal);

        private static IEnumerable<string> ProductionEditorSourceRelativePaths(string editorRoot)
        {
            foreach (var file in Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(editorRoot, file).Replace('\\', '/');
                if (relative.Split('/').Contains("Tests")) continue;
                yield return relative;
            }
        }

        [Test]
        public void ProductionEditorSources_DelayCallUsage_LimitedToAllowlistedGuiPaths()
        {
            var package = PackageInfo.FindForAssembly(typeof(MCPServer).Assembly);
            Assert.That(package, Is.Not.Null, "UPM package not found for UnityMCP.Editor assembly.");
            var editorRoot = Path.Combine(Path.GetFullPath(package.resolvedPath), "Editor");
            Assert.That(Directory.Exists(editorRoot), Is.True, $"Editor root does not exist: {editorRoot}");

            var offenders = new List<string>();
            foreach (var relative in ProductionEditorSourceRelativePaths(editorRoot))
            {
                if (IsAllowed(relative)) continue;
                var code = NonCode.Replace(File.ReadAllText(Path.Combine(editorRoot, relative)), "");
                if (DelayCallUsage.IsMatch(code)) offenders.Add(relative);
            }

            Assert.That(offenders, Is.Empty,
                "delayCall is reserved for GUI paths behind an open window or a click; found " +
                "unexpected EditorApplication.delayCall usage outside the allowlist in: " +
                string.Join(", ", offenders));
        }
    }
}
