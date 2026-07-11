using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class SceneRefResolverTests
    {
        [SetUp]
        public void FreshScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void CleanScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void ResolveOne_LiteralPath_ExistingObject_ReturnsOK()
        {
            var go = new GameObject("TestResObj");
            var result = SceneRefResolver.ResolveOne("/TestResObj", System.Array.Empty<string>());
            Assert.AreEqual("OK", result.Status);
            Assert.IsTrue(result.Path.EndsWith("TestResObj"));
        }

        [Test]
        public void ResolveOne_LiteralPath_MissingObject_ReturnsMISS()
        {
            var result = SceneRefResolver.ResolveOne("/DoesNotExist_XYZ999", System.Array.Empty<string>());
            Assert.AreEqual("MISS", result.Status);
            Assert.AreEqual("not found", result.Reason);
        }

        [Test]
        public void ResolveOne_TypeQuery_SingleMatch_ReturnsOK()
        {
            var go = new GameObject("AudioObj");
            go.AddComponent<AudioSource>();
            var result = SceneRefResolver.ResolveOne("t:AudioSource", System.Array.Empty<string>());
            Assert.AreEqual("OK", result.Status);
        }

        [Test]
        public void ResolveOne_TypeQuery_MultipleMatches_ReturnsAMB()
        {
            new GameObject("AudioA").AddComponent<AudioSource>();
            new GameObject("AudioB").AddComponent<AudioSource>();
            var result = SceneRefResolver.ResolveOne("t:AudioSource", System.Array.Empty<string>());
            Assert.AreEqual("AMB", result.Status);
            StringAssert.Contains("2 matches", result.Reason);
        }

        [Test]
        public void ResolveOne_AliasToken_Expanded()
        {
            var go = new GameObject("AliasTarget");
            AliasExpander._tableOverride = new Dictionary<string, string> { ["myres"] = "/AliasTarget" };
            try
            {
                var result = SceneRefResolver.ResolveOne("$myres", System.Array.Empty<string>());
                Assert.AreEqual("OK", result.Status);
            }
            finally
            {
                AliasExpander._tableOverride = null;
            }
        }

        [Test]
        public void ResolveOne_FieldCheck_ExistingField_ReturnsFieldOK()
        {
            new GameObject("AudioC").AddComponent<AudioSource>();
            var result = SceneRefResolver.ResolveOne("t:AudioSource", new[] { "volume" });
            Assert.AreEqual("OK", result.Status);
            Assert.IsNotNull(result.Fields);
            Assert.AreEqual(1, result.Fields.Count);
            Assert.AreEqual("volume", result.Fields[0].field);
            Assert.AreEqual("OK", result.Fields[0].status);
        }

        [Test]
        public void ResolveOne_FieldCheck_MissingField_ReturnsFieldMISS()
        {
            new GameObject("AudioD").AddComponent<AudioSource>();
            var result = SceneRefResolver.ResolveOne("t:AudioSource", new[] { "nonExistentField_xyz99" });
            Assert.AreEqual("OK", result.Status);
            Assert.AreEqual("MISS", result.Fields[0].status);
        }

        [Test]
        public void FormatResults_MultipleRefs_OneLineEach()
        {
            var results = new List<SceneRefResolver.RefResult>
            {
                new SceneRefResolver.RefResult { Input = "/A", Status = "OK", Path = "/A", Active = true, InstanceId = 1, SceneName = "Test" },
                new SceneRefResolver.RefResult { Input = "/B", Status = "MISS", Reason = "not found" }
            };
            var formatted = SceneRefResolver.FormatResults(results);
            var lines = formatted.Split('\n');
            Assert.AreEqual(2, lines.Length, "One line per result");
            StringAssert.StartsWith("OK", lines[0]);
            StringAssert.StartsWith("MISS", lines[1]);
        }
    }
}
