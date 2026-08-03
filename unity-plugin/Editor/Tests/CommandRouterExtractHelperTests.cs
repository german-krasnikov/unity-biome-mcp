// TDD: CommandRouter.ExtractVector3 (C4, review sprint v0.70). DRYs the zoom/offset/
// fixed_size parsing previously duplicated (byte-identical, only var-name suffixes differed)
// across CommandRouter.ObjectHandlers.cs's multi_view and single_view screenshot branches.
// Never throws -- returns defaultVal on any parse failure, matching the original inline
// silent-fallback behavior (NOT a drop-in for ValueParser.ParseVector3, which throws).
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterExtractHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string Key = "offset";
        private static readonly Vector3 Default = new Vector3(9f, 9f, 9f);

        [Test]
        public void ExtractVector3_ValidCsv_ReturnsParsedVector()
        {
            var args = "{\"offset\":\"1,2,3\"}";
            var result = CommandRouter.ExtractVector3(args, Key, Default);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), result);
        }

        [Test]
        public void ExtractVector3_MissingKey_ReturnsDefault()
        {
            var args = "{\"other\":\"value\"}";
            var result = CommandRouter.ExtractVector3(args, Key, Default);
            Assert.AreEqual(Default, result);
        }

        [Test]
        public void ExtractVector3_MalformedValue_ReturnsDefault()
        {
            // Only 2 components -- must NOT throw, must fall back silently (distinguishes
            // this helper from ValueParser.ParseVector3's throw-on-error contract).
            var args = "{\"offset\":\"1,2\"}";
            Vector3 result = default;
            Assert.DoesNotThrow(() => result = CommandRouter.ExtractVector3(args, Key, Default));
            Assert.AreEqual(Default, result);
        }

        [Test]
        public void ExtractVector3_NonNumericComponent_ReturnsDefault()
        {
            var args = "{\"offset\":\"a,b,c\"}";
            var result = CommandRouter.ExtractVector3(args, Key, Default);
            Assert.AreEqual(Default, result);
        }
    }
}
