// TDD tests for PackageManagerHelper — EditMode unit tests.
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PackageManagerHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public async Task Execute_UnknownAction_SetsErrResult()
        {
            var inner = new TaskCompletionSource<string>();
            PackageManagerHelper.Execute("bad", null, null, null, inner);
            var result = await inner.Task;
            StringAssert.StartsWith("err:", result);
            StringAssert.Contains("bad", result);
        }

        [Test]
        public async Task Execute_AddAction_MissingName_SetsErrResult()
        {
            var inner = new TaskCompletionSource<string>();
            PackageManagerHelper.Execute("add", null, null, null, inner);
            var result = await inner.Task;
            Assert.AreEqual("err:name required", result);
        }

        [Test]
        public async Task Execute_SearchAction_MissingQuery_SetsErrResult()
        {
            var inner = new TaskCompletionSource<string>();
            PackageManagerHelper.Execute("search", null, null, null, inner);
            var result = await inner.Task;
            Assert.AreEqual("err:query required", result);
        }

        [Test]
        public async Task Execute_RemoveAction_MissingName_SetsErrResult()
        {
            var inner = new TaskCompletionSource<string>();
            PackageManagerHelper.Execute("remove", null, null, null, inner);
            var result = await inner.Task;
            Assert.AreEqual("err:name required", result);
        }
    }
}
