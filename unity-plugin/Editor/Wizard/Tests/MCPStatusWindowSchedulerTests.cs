// MCPStatusWindow owns only its polling scheduler; visual loops are element-owned
// and pause automatically when their panel detaches.
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPStatusWindowSchedulerTests
    {
        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void MCPStatusWindow_KeepsOnlyRefreshSchedulerField()
        {
            var t = typeof(MCPStatusWindow);
            Assert.IsNotNull(t.GetField("_refreshJob",  NonPublicInstance), "_refreshJob field must exist");
            Assert.IsNull(
                t.GetField("_beatFastJob", NonPublicInstance),
                "legacy stepped beat scheduler must stay removed");
            Assert.IsNull(
                t.GetField("_beatSoftJob", NonPublicInstance),
                "legacy stepped beat scheduler must stay removed");
        }

        [Test]
        public void MCPStatusWindow_HasOnDisableMethod()
        {
            var m = typeof(MCPStatusWindow).GetMethod("OnDisable", NonPublicInstance);
            Assert.IsNotNull(m, "OnDisable method must exist on MCPStatusWindow");
        }

        [Test]
        public void MCPStatusWindow_OnDisable_IsPrivate()
        {
            var m = typeof(MCPStatusWindow).GetMethod("OnDisable", NonPublicInstance);
            Assert.IsNotNull(m);
            Assert.IsTrue(m.IsPrivate, "OnDisable must be private — Unity callback convention");
        }
    }
}
