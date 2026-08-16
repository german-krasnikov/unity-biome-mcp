using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
    }
}
