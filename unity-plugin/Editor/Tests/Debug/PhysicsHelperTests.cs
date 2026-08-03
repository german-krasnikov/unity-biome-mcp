// TDD RED: PhysicsHelper null guard tests.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PhysicsHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetState_NullGameObject_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => PhysicsHelper.GetState(null));
        }
    }
}
