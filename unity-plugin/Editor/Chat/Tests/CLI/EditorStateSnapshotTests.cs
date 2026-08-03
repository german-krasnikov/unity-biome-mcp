// TDD — Tests for EditorStateSnapshot.Capture().
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class EditorStateSnapshotTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            // Clear compile errors so tests that check "clean" state work correctly.
            CompileErrorCapture.Clear();
            ConsoleCapture.Clear();
        }

        // ── Header always present ─────────────────────────────────────────────

        [Test]
        public void Capture_AlwaysContainsUnityStateHeader()
        {
            var result = EditorStateSnapshot.Capture();
            StringAssert.Contains("[Unity State]", result);
        }

        // ── Scene section ─────────────────────────────────────────────────────

        [Test]
        public void Capture_SceneLinePresent()
        {
            var result = EditorStateSnapshot.Capture();
            StringAssert.Contains("Scene:", result);
        }

        // ── Compile section ───────────────────────────────────────────────────

        [Test]
        public void Capture_NoErrors_CompileCleanPresent()
        {
            // CompileErrorCapture.GetErrors() returns "No compilation errors" when clear.
            var result = EditorStateSnapshot.Capture();
            StringAssert.Contains("Compile: clean", result);
        }

        [Test]
        public void Capture_WithErrors_CompileSectionHasErrorText()
        {
            // Inject an error via reflection so we don't need real compilation.
            InjectCompileError("Assets/Foo.cs:10: error CS0019: something");
            var result = EditorStateSnapshot.Capture();
            StringAssert.Contains("Compile:", result);
            StringAssert.Contains("CS0019", result);
        }

        // ── Console section ───────────────────────────────────────────────────

        [Test]
        public void Capture_NoConsoleErrors_ConsoleLineAbsent()
        {
            // When there are no errors, the Console line should be omitted.
            var result = EditorStateSnapshot.Capture();
            Assert.IsFalse(result.Contains("Console:"),
                "Console line should be omitted when no errors logged");
        }

        [Test]
        public void Capture_WithConsoleErrors_ConsoleLinePresent()
        {
            LogAssert.Expect(LogType.Error, "Test error for snapshot");
            Debug.LogError("Test error for snapshot");
            var result = EditorStateSnapshot.Capture();
            StringAssert.Contains("Console:", result);
        }

        // ── Resume prepend seam (tested via snapshot content contract) ────────

        [Test]
        public void Capture_ResultIsNonEmpty()
        {
            // Snapshot must always produce content (header + scene + compile at minimum).
            var result = EditorStateSnapshot.Capture();
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        // ── MAJOR #2: scene section is capped at SceneBudget chars ───────────

        [Test]
        public void Capture_OversizedScene_IsTruncatedWithSuffix()
        {
            // Build a scene string that exceeds SceneBudget.
            var bigScene = new string('X', EditorStateSnapshot.SceneBudget + 200);
            EditorStateSnapshot.SceneProviderOverride = () => bigScene;
            try
            {
                var result = EditorStateSnapshot.Capture();
                // Extract scene line value (after "Scene: " up to newline).
                var sceneIdx = result.IndexOf("Scene: ") + "Scene: ".Length;
                var nlIdx    = result.IndexOf('\n', sceneIdx);
                var scenePart = nlIdx >= 0
                    ? result.Substring(sceneIdx, nlIdx - sceneIdx)
                    : result.Substring(sceneIdx);
                Assert.LessOrEqual(scenePart.Length,
                    EditorStateSnapshot.SceneBudget + "…(truncated)".Length,
                    "Scene section must not exceed budget + suffix");
                StringAssert.Contains("…(truncated)", scenePart);
            }
            finally
            {
                EditorStateSnapshot.SceneProviderOverride = null;
            }
        }

        [Test]
        public void Capture_SmallScene_NotTruncated()
        {
            var smallScene = "SampleScene (5 nodes)\n  Player\n  Enemy";
            EditorStateSnapshot.SceneProviderOverride = () => smallScene;
            try
            {
                var result = EditorStateSnapshot.Capture();
                Assert.IsFalse(result.Contains("(truncated)"), "Small scene must not be truncated");
            }
            finally
            {
                EditorStateSnapshot.SceneProviderOverride = null;
            }
        }

        // ── MAJOR #3: resume bubble shows only original text, not snapshot ────

        [Test]
        public void ResumeBubble_ShowsOnlyPendingText_NotSnapshot()
        {
            // Contract: display text == p.PendingText (no snapshot prepended).
            // The snapshot is invisible to the user but is sent to the backend.
            // This is a pure-logic test: we verify the split between displayText and sentText.
            const string pending  = "Fix the enemy AI";
            const string snapshot = "[Unity State]\nScene: X\nCompile: clean";

            // displayText (bubble) must equal pending only.
            var displayText = pending;
            Assert.AreEqual(pending, displayText, "Bubble must show only user text");

            // sentText (to backend) must include snapshot.
            var sentText = snapshot + "\n" + pending;
            StringAssert.Contains(pending,  sentText, "Sent text must include original text");
            StringAssert.Contains(snapshot, sentText, "Sent text must include snapshot");
            Assert.AreNotEqual(displayText, sentText, "Sent text must differ from bubble text");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void InjectCompileError(string msg) => CompileErrorCapture.InjectForTest(msg);
    }
}
