// TDD — RED first. Tests for MentionRowActions static helpers.
// Uses EditorGUIUtility.systemCopyBuffer (main-thread-safe in EditMode).
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionRowActionsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _savedClipboard;

        [SetUp]
        public void SetUp()
        {
            _savedClipboard = EditorGUIUtility.systemCopyBuffer;
        }

        [TearDown]
        public void TearDown()
        {
            EditorGUIUtility.systemCopyBuffer = _savedClipboard;
        }

        private static MentionCandidate MakeHierarchyCandidate(string path, string objectId)
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, path, System.IO.Path.GetFileName(path), objectId);
            return new MentionCandidate(chip, 100, "icon");
        }

        private static MentionCandidate MakeAssetCandidate(string path)
        {
            var chip = new ChipData(ChipKindKeys.Scene, path, System.IO.Path.GetFileName(path), "");
            return new MentionCandidate(chip, 100, "icon");
        }

        // ── CopyRef ───────────────────────────────────────────────────────────

        [Test]
        public void CopyRef_Hierarchy_SetsClipboardWithObjectId()
        {
            // objectId already in $HEX format — FormatChipRef appends directly
            var candidate = MakeHierarchyCandidate("/Player", "$2A");
            MentionRowActions.CopyRef(candidate);
            Assert.AreEqual("[hierarchy:/Player$2A]", EditorGUIUtility.systemCopyBuffer);
        }

        [Test]
        public void CopyRef_Asset_SetsClipboardWithoutObjectId()
        {
            var candidate = MakeAssetCandidate("Assets/Foo.mat");
            MentionRowActions.CopyRef(candidate);
            Assert.AreEqual("[scene:Assets/Foo.mat]", EditorGUIUtility.systemCopyBuffer);
        }

        [Test]
        public void CopyRef_Hierarchy_ZeroObjectId_OmitsHash()
        {
            // objectId = "" → treated as no ID by FormatChipRef
            var candidate = MakeHierarchyCandidate("/Camera", "");
            MentionRowActions.CopyRef(candidate);
            Assert.AreEqual("[hierarchy:/Camera]", EditorGUIUtility.systemCopyBuffer);
        }

        // ── IsHierarchyChip ───────────────────────────────────────────────────

        [Test]
        public void IsHierarchyChip_ReturnsTrueForHierarchyKey()
        {
            var candidate = MakeHierarchyCandidate("/Player", "0");
            Assert.IsTrue(MentionRowActions.IsHierarchyChip(candidate));
        }

        [Test]
        public void IsHierarchyChip_ReturnsFalseForAssetKey()
        {
            var candidate = MakeAssetCandidate("Assets/Foo.mat");
            Assert.IsFalse(MentionRowActions.IsHierarchyChip(candidate));
        }
    }
}
