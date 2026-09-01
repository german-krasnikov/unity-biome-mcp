using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchRequestTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        [Test]
        public void TryCreate_ValidInputs_ReturnsTrueAndPopulatesRequest()
        {
            var ok = SourcePatchRequest.TryCreate("Assets/Foo.cs", Bytes("before"), Bytes("after"), out var request);

            Assert.IsTrue(ok);
            Assert.AreEqual("Assets/Foo.cs", request.AssetPath);
            CollectionAssert.AreEqual(Bytes("before"), request.ExpectedBeforeContent);
            CollectionAssert.AreEqual(Bytes("after"), request.NewContent);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryCreate_NullOrBlankPath_ReturnsFalse(string path)
        {
            var ok = SourcePatchRequest.TryCreate(path, Bytes("a"), Bytes("b"), out var request);

            Assert.IsFalse(ok);
            Assert.IsNull(request);
        }

        [Test]
        public void TryCreate_PathNotCsExtension_ReturnsFalse()
        {
            var ok = SourcePatchRequest.TryCreate("Assets/Foo.txt", Bytes("a"), Bytes("b"), out var request);

            Assert.IsFalse(ok);
            Assert.IsNull(request);
        }

        [Test]
        public void TryCreate_NullBeforeContent_ReturnsFalse()
        {
            var ok = SourcePatchRequest.TryCreate("Assets/Foo.cs", null, Bytes("b"), out var request);

            Assert.IsFalse(ok);
            Assert.IsNull(request);
        }

        [Test]
        public void TryCreate_NullNewContent_ReturnsFalse()
        {
            var ok = SourcePatchRequest.TryCreate("Assets/Foo.cs", Bytes("a"), null, out var request);

            Assert.IsFalse(ok);
            Assert.IsNull(request);
        }

        [Test]
        public void TryCreate_CallerMutatesInputArrayAfterward_DoesNotAffectStoredContent()
        {
            var before = Bytes("before");
            var after = Bytes("after");
            SourcePatchRequest.TryCreate("Assets/Foo.cs", before, after, out var request);

            before[0] = 0;
            after[0] = 0;

            CollectionAssert.AreEqual(Bytes("before"), request.ExpectedBeforeContent);
            CollectionAssert.AreEqual(Bytes("after"), request.NewContent);
        }

        [Test]
        public void GetterResult_WhenMutated_DoesNotAffectStoredContent()
        {
            SourcePatchRequest.TryCreate("Assets/Foo.cs", Bytes("before"), Bytes("after"), out var request);

            var beforeSnapshot = request.ExpectedBeforeContent;
            beforeSnapshot[0] = 0;
            var afterSnapshot = request.NewContent;
            afterSnapshot[0] = 0;

            CollectionAssert.AreEqual(Bytes("before"), request.ExpectedBeforeContent);
            CollectionAssert.AreEqual(Bytes("after"), request.NewContent);
        }

        [Test]
        public void PublicSurface_ExposesExactAllowlist()
        {
            var members = typeof(SourcePatchRequest)
                .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.MemberType != MemberTypes.Constructor)
                .Select(m => m.Name)
                .Where(n => !n.StartsWith("get_", System.StringComparison.Ordinal))
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            var expected = new[] { "AssetPath", "ExpectedBeforeContent", "NewContent", "TryCreate" }
                .OrderBy(n => n)
                .ToArray();

            CollectionAssert.AreEqual(expected, members);
        }
    }
}
