// TDD tests for HierarchyReference parsing and HierarchyResolver fallback chain.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HierarchyReferenceTests
    {
        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        // ── Parsing ───────────────────────────────────────────────────────────

        [Test]
        public void Parse_LegacyPathAndId_ReturnsPathAndId()
        {
            var href = HierarchyReference.Parse("/Root/Child #12345");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual(12345, href.InstanceId);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_NewGlobalObjectIdFormat_ReturnsAllFields()
        {
            // Craft a valid GlobalObjectId string with a non-zero targetObjectId.
            // Parsing must preserve the GOID independently of the path/instanceID.
            const string goidString = "GlobalObjectId_V1-2-00000000000000000000000000000001-12345-0";
            var raw = $"/Root/Child#12345@{goidString}";
            var href = HierarchyReference.Parse(raw);
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual(12345, href.InstanceId);
            Assert.AreEqual(12345UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_NoId_ReturnsZeroInstanceId()
        {
            var href = HierarchyReference.Parse("/Root/Child");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual(0, href.InstanceId);
        }

        [Test]
        public void Parse_Empty_ReturnsEmptyPath()
        {
            var href = HierarchyReference.Parse("");
            Assert.AreEqual("", href.Path);
            Assert.AreEqual(0, href.InstanceId);
        }

        [Test]
        public void Parse_InvalidGlobalObjectIdString_IgnoresInvalidId()
        {
            var href = HierarchyReference.Parse("/Root/Child@not_a_valid_goid");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        // ── Resolver fallback chain ───────────────────────────────────────────

        [Test]
        public void Resolver_ExactPath_ReturnsGameObject()
        {
            var go = new GameObject("ResolverExact");
            try
            {
                var resolver = new HierarchyResolver();
                var href = new HierarchyReference("/ResolverExact", 0, default);
                var resolved = resolver.Resolve(href);
                Assert.AreEqual(go, resolved);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resolver_InstanceId_WhenPathStale_ReturnsGameObject()
        {
            var go = new GameObject("ResolverById");
            try
            {
                var resolver = new HierarchyResolver();
                var href = new HierarchyReference("/StalePath", go.GetInstanceID(), default);
                var resolved = resolver.Resolve(href);
                Assert.AreEqual(go, resolved);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resolver_FuzzyName_WhenPathMissing_ReturnsGameObject()
        {
            var go = new GameObject("ResolverFuzzy");
            try
            {
                var resolver = new HierarchyResolver();
                var href = new HierarchyReference("/NonExistent/ResolverFuzzy", 0, default);
                var resolved = resolver.Resolve(href);
                Assert.AreEqual(go, resolved);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resolver_Unresolvable_ReturnsNull()
        {
            var resolver = new HierarchyResolver();
            var href = new HierarchyReference("/DefinitelyNotThereXYZ", 0, default);
            var resolved = resolver.Resolve(href);
            Assert.IsNull(resolved);
        }
    }
}
