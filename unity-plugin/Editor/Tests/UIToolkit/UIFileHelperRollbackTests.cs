using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIFileHelperRollbackTests : UnityMcpTestBase
    {
        private const string TempDir = "Assets/TestsTemp/UIFileHelperRollback";

        private static string Abs(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        [SetUp]
        public void SetUp()
        {
            TrackOwnedAsset(TempDir);
            Directory.CreateDirectory(Abs(TempDir));
        }

        [Test]
        [BiomeWorkerOnly("failed new UXML import must leave no file or asset")]
        public void WriteNewUxml_PostImportValidationFails_RemovesNewAsset()
        {
            var assetPath = TempDir + "/FailedNew.uxml";
            var result = UIFileHelper.WriteUIFile(
                assetPath,
                "create_uxml",
                null,
                _ => null,
                path => AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(path));

            StringAssert.Contains("auto-reverted", result, result);
            Assert.IsFalse(File.Exists(Abs(assetPath)), "failed new UXML file must be deleted");
            Assert.IsFalse(File.Exists(Abs(assetPath) + ".meta"), "failed new UXML meta must be deleted");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(assetPath),
                "failed new UXML must not remain in AssetDatabase");
        }

        [Test]
        [BiomeWorkerOnly("failed new USS import must leave no file or asset")]
        public void WriteNewUss_PostImportValidationFails_RemovesNewAsset()
        {
            var assetPath = TempDir + "/FailedNew.uss";
            var result = UIFileHelper.WriteUIFile(
                assetPath,
                "create_uss",
                ".root { color: red; }",
                path => AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path),
                _ => null);

            StringAssert.Contains("auto-reverted", result, result);
            Assert.IsFalse(File.Exists(Abs(assetPath)), "failed new USS file must be deleted");
            Assert.IsFalse(File.Exists(Abs(assetPath) + ".meta"), "failed new USS meta must be deleted");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(assetPath),
                "failed new USS must not remain in AssetDatabase");
        }

        // On Unity 6000.0.83f1, VisualTreeAsset.CloneTree() does NOT return null for a UXML that
        // names an unregistered element type (see UIFileHelperSpikeTests.BrokenUxml_CloneTreeOrLoadReturnsNull) —
        // it logs an Error and returns a tree with a placeholder Label child instead. The prior
        // `vta.CloneTree() == null` gate in ValidateImportedUxml never fires for this real-content
        // case, so these three tests exercise the real WriteUIFile entry point (the default
        // AssetDatabase loader, not an injected fake) to pin the fixed behaviour against real engine
        // output, not a simulated one.
        private const string UnregisteredElementUxml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
            "<ui:NOTEXISTENT_ELEMENT_12345 />" +
            "</ui:UXML>";

        private static readonly Regex UnregisteredElementLog = new Regex(
            "Element 'UnityEngine\\.UIElements\\.NOTEXISTENT_ELEMENT_12345' is missing a UxmlElementAttribute");

        [Test]
        [BiomeWorkerOnly("real UXML import with an unregistered element type must auto-revert")]
        public void WriteNewUxml_UnregisteredElementType_RemovesNewAssetAndFile()
        {
            var assetPath = TempDir + "/UnregisteredNew.uxml";

            // CloneTree() logs this error for the unregistered element type; declare it so UTF's
            // default "unhandled error log fails the test" gate does not fail this test for an
            // error the test itself provoked on purpose (AI/testing.md).
            LogAssert.Expect(LogType.Error, UnregisteredElementLog);
            var result = UIFileHelper.WriteUIFile(assetPath, "create_uxml", UnregisteredElementUxml);

            StringAssert.Contains("auto-reverted", result, result);
            Assert.IsFalse(File.Exists(Abs(assetPath)), "unregistered-element UXML file must be deleted");
            Assert.IsFalse(File.Exists(Abs(assetPath) + ".meta"), "unregistered-element UXML meta must be deleted");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(assetPath),
                "unregistered-element UXML must not remain in AssetDatabase");
        }

        [Test]
        [BiomeWorkerOnly("real UXML import with an unregistered element type must restore prior content")]
        public void WriteExistingUxml_UnregisteredElementType_RestoresPreviousContent()
        {
            var assetPath = TempDir + "/UnregisteredExisting.uxml";
            const string original =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
                "    <ui:VisualElement name=\"original-root\" />\n" +
                "</ui:UXML>\n";
            var firstWrite = UIFileHelper.WriteUIFile(assetPath, "write", original);
            StringAssert.Contains("ok: write", firstWrite, firstWrite);

            LogAssert.Expect(LogType.Error, UnregisteredElementLog);
            var result = UIFileHelper.WriteUIFile(assetPath, "write", UnregisteredElementUxml);

            StringAssert.Contains("auto-reverted", result, result);
            var restored = File.ReadAllText(Abs(assetPath), System.Text.Encoding.UTF8);
            StringAssert.Contains("original-root", restored, "the file must be restored to its pre-write content");
            StringAssert.DoesNotContain("NOTEXISTENT_ELEMENT_12345", restored,
                "the broken content must never remain on disk");
        }

        [Test]
        [BiomeWorkerOnly("real UXML import must stay registered for a well-formed, valid document")]
        public void WriteNewUxml_ValidContent_IsAccepted()
        {
            var assetPath = TempDir + "/ValidNew.uxml";
            const string valid =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
                "    <ui:VisualElement name=\"root\" />\n" +
                "</ui:UXML>\n";

            var result = UIFileHelper.WriteUIFile(assetPath, "create_uxml", valid);

            StringAssert.Contains("ok: create_uxml", result, result);
            StringAssert.Contains("validated: CloneTree OK", result, result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath));
        }

        [Test]
        [BiomeWorkerOnly("writing a new UXML over an existing file touches the imported asset")]
        public void WriteExistingUxml_MalformedXml_LeavesPreviousFileUntouched()
        {
            var assetPath = TempDir + "/MalformedExisting.uxml";
            const string original =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
                "    <ui:VisualElement name=\"original-root\" />\n" +
                "</ui:UXML>\n";
            var firstWrite = UIFileHelper.WriteUIFile(assetPath, "write", original);
            StringAssert.Contains("ok: write", firstWrite, firstWrite);

            const string malformed = "<ui:UXML><unclosed>";
            var result = UIFileHelper.WriteUIFile(assetPath, "write", malformed);

            StringAssert.Contains("invalid XML", result, result);
            var unchanged = File.ReadAllText(Abs(assetPath), System.Text.Encoding.UTF8);
            StringAssert.Contains("original-root", unchanged, "malformed XML must never reach disk");
        }
    }
}
