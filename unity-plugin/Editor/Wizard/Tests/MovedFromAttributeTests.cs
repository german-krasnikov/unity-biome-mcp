using NUnit.Framework;
using UnityEngine.Scripting.APIUpdating;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MovedFromAttributeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void MCPStatusWindow_HasMovedFromAttribute()
        {
            var attrs = typeof(MCPStatusWindow).GetCustomAttributes(
                typeof(MovedFromAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "MCPStatusWindow must have [MovedFrom] attribute");
        }

        [Test]
        public void MCPDiagnoseWindow_HasMovedFromAttribute()
        {
            var attrs = typeof(MCPDiagnoseWindow).GetCustomAttributes(
                typeof(MovedFromAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "MCPDiagnoseWindow must have [MovedFrom] attribute");
        }
    }
}
