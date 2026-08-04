// TDD: warm_type_cache command — registration, return format, type count > 0.
// EditMode only, no TCP required.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WarmTypeCacheTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void WarmTypeCache_IsRegistered()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("warm_type_cache"),
                "warm_type_cache must be registered in CommandRegistry");
        }

        [Test]
        public void WarmTypeCache_ReturnsOkWithCount()
        {
            var result = CommandRegistry.Execute("warm_type_cache", "");
            Assert.That(result, Does.StartWith("ok:types="),
                "warm_type_cache must return ok:types=<count>");
            var count = int.Parse(result.Substring("ok:types=".Length));
            Assert.Greater(count, 0, "type count must be > 0");
        }

        [Test]
        public void WarmTypeCache_NotAllowedDuringCompile()
        {
            Assert.IsFalse(CommandRouter.IsAllowedDuringCompile("warm_type_cache"),
                "warm_type_cache must not run during compile (TypeCache not safe)");
        }
    }
}
