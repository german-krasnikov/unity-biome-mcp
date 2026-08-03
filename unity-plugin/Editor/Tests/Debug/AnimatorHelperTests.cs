// TDD RED: AnimatorHelper null guard tests.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AnimatorHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetState_NullAnimator_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => AnimatorHelper.GetState(null));
        }
    }
}
