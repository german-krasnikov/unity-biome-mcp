// TDD tests for HierarchyReference parsing and HierarchyResolver fallback chain.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HierarchyReferenceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Parsing ───────────────────────────────────────────────────────────

        [Test]
        public void Parse_HexPathAndId_ReturnsPathAndId()
        {
            // 12345 decimal = 0x3039 hex
            var href = HierarchyReference.Parse("/Root/Child$3039");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual("$3039", href.ObjectId);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_LargeHexId_PreservesToken()
        {
            // Full 64-bit hex preserved in the token
            var href = HierarchyReference.Parse("/Root/Child$FFFFFFFFFFFFFFFF");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual("$FFFFFFFFFFFFFFFF", href.ObjectId);
        }

        [Test]
        public void Parse_NewGlobalObjectIdFormat_ReturnsAllFields()
        {
            // Craft a valid GlobalObjectId string with a non-zero targetObjectId.
            // Parsing must preserve the GOID independently of the path/instanceID.
            const string goidString = "GlobalObjectId_V1-2-00000000000000000000000000000001-12345-0";
            var raw = $"/Root/Child$3039@{goidString}";  // 12345 = 0x3039
            var href = HierarchyReference.Parse(raw);
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual("$3039", href.ObjectId);
            Assert.AreEqual(12345UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_NoId_ReturnsZeroInstanceId()
        {
            var href = HierarchyReference.Parse("/Root/Child");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual("", href.ObjectId);
        }

        [Test]
        public void Parse_Empty_ReturnsEmptyPath()
        {
            var href = HierarchyReference.Parse("");
            Assert.AreEqual("", href.Path);
            Assert.AreEqual("", href.ObjectId);
        }

        [Test]
        public void Parse_InvalidGlobalObjectIdString_IgnoresInvalidId()
        {
            var href = HierarchyReference.Parse("/Root/Child@not_a_valid_goid");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        // ── $HEX format (Phase 2) ─────────────────────────────────────────────

        [Test]
        public void Parse_DollarHexFormat_ExtractsPathAndId()
        {
            // New format: $HEX attached directly to path, no space
            var href = HierarchyReference.Parse("/Ground$2B678");
            Assert.AreEqual("/Ground", href.Path);
            Assert.AreEqual("$2B678", href.ObjectId);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_DollarHexWithGlobalObjectId_ExtractsBoth()
        {
            const string goidString = "GlobalObjectId_V1-2-00000000000000000000000000000001-12345-0";
            var raw = $"/Ground$2B678@{goidString}";
            var href = HierarchyReference.Parse(raw);
            Assert.AreEqual("/Ground", href.Path);
            Assert.AreEqual("$2B678", href.ObjectId);
            Assert.AreEqual(12345UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_DollarHexNoSpace_NestedPath_ExtractsCorrectly()
        {
            // Nested path: $HEX attached after multi-level path
            var href = HierarchyReference.Parse("/Root/Child/Leaf$FF00");
            Assert.AreEqual("/Root/Child/Leaf", href.Path);
            Assert.AreEqual("$FF00", href.ObjectId);
        }

        [Test]
        public void Parse_DollarHexInvalidHex_NotExtracted()
        {
            // "$g" and similar RefManager slots are not valid hex — should not be extracted
            var href = HierarchyReference.Parse("/Root/Child$gx");
            // "$gx" fails TryParse — whole string is the path
            Assert.AreEqual("/Root/Child$gx", href.Path);
            Assert.AreEqual("", href.ObjectId);
        }

        // ── &ref format (ST5) ────────────────────────────────────────────────

        [Test]
        public void Parse_AmpRef_ExtractsPathAndId()
        {
            var href = HierarchyReference.Parse("/Ground&a");
            Assert.AreEqual("/Ground", href.Path);
            Assert.AreEqual("&a", href.ObjectId);
            Assert.AreEqual(0UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_AmpRef_WithGlobalObjectId_ExtractsBoth()
        {
            const string goidString = "GlobalObjectId_V1-2-00000000000000000000000000000001-12345-0";
            var raw = $"/Ground&a@{goidString}";
            var href = HierarchyReference.Parse(raw);
            Assert.AreEqual("/Ground", href.Path);
            Assert.AreEqual("&a", href.ObjectId);
            Assert.AreEqual(12345UL, href.GlobalObjectId.targetObjectId);
        }

        [Test]
        public void Parse_AmpRef_NestedPath_ExtractsCorrectly()
        {
            var href = HierarchyReference.Parse("/Root/Child&b2");
            Assert.AreEqual("/Root/Child", href.Path);
            Assert.AreEqual("&b2", href.ObjectId);
        }

        [Test]
        public void Parse_AmpRef_InvalidToken_NotExtracted()
        {
            // "&" not followed by alphanumeric — not a valid ref, stays in path
            var href = HierarchyReference.Parse("/Root&");
            Assert.AreEqual("/Root&", href.Path);
            Assert.AreEqual("", href.ObjectId);
        }

        [Test]
        public void Resolver_AmpRef_WhenRefValid_ReturnsGameObject()
        {
            var go = new GameObject("ResolverAmpRef");
            try
            {
                var r = RefManager.Assign(go);
                var resolver = new HierarchyResolver();
                var href = new HierarchyReference("/StalePath", r, default);
                var resolved = resolver.Resolve(href);
                Assert.AreEqual(go, resolved);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
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
                var href = new HierarchyReference("/StalePath", TransientObjectId.GetWireValue(go), default);
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
