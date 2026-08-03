using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public class ToolGroupStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void FirstTool_SetsPending()
        {
            var s = new ToolGroupState();
            Assert.AreEqual(ToolGroupAction.SetPending, s.OnTool(isError: false, pendingAlive: false));
            Assert.IsTrue(s.HasPending); Assert.IsFalse(s.HasGroup);
        }

        [Test] public void SecondTool_PromotesToGroupOfTwo()
        {
            var s = new ToolGroupState();
            s.OnTool(false, false);
            Assert.AreEqual(ToolGroupAction.Promote, s.OnTool(isError: false, pendingAlive: true));
            Assert.AreEqual(2, s.Count); Assert.IsFalse(s.AnyError);
            Assert.IsTrue(s.HasGroup); Assert.IsFalse(s.HasPending);
        }

        [Test] public void SecondTool_DeadPending_PromotesToGroupOfOne()
        {
            var s = new ToolGroupState();
            s.OnTool(false, false);
            Assert.AreEqual(ToolGroupAction.Promote, s.OnTool(isError: false, pendingAlive: false));
            Assert.AreEqual(1, s.Count); // evicted bare chip dropped from count
        }

        [Test] public void ThirdTool_AppendsToOpenGroup()
        {
            var s = new ToolGroupState();
            s.OnTool(false, false); s.OnTool(false, true);
            Assert.AreEqual(ToolGroupAction.Append, s.OnTool(isError: false, pendingAlive: false));
            Assert.AreEqual(3, s.Count);
        }

        [Test] public void PendingError_PropagatesOnPromote()
        {
            var s = new ToolGroupState();
            s.OnTool(isError: true, pendingAlive: false);
            s.OnTool(isError: false, pendingAlive: true);
            Assert.IsTrue(s.AnyError);
        }

        [Test] public void DeadPendingError_DoesNotPropagate()
        {
            var s = new ToolGroupState();
            s.OnTool(isError: true, pendingAlive: false);          // pending was an error...
            s.OnTool(isError: false, pendingAlive: false);         // ...but evicted before promote
            Assert.IsFalse(s.AnyError);                            // only the surviving ok chip counts
            Assert.AreEqual(1, s.Count);
        }

        [Test] public void AppendError_SetsAnyError()
        {
            var s = new ToolGroupState();
            s.OnTool(false, false); s.OnTool(false, true);
            s.OnTool(isError: true, pendingAlive: false);
            Assert.IsTrue(s.AnyError);
        }

        [Test] public void Reset_ClearsEverything()
        {
            var s = new ToolGroupState();
            s.OnTool(false, false); s.OnTool(true, true);
            s.Reset();
            Assert.AreEqual(0, s.Count); Assert.IsFalse(s.AnyError);
            Assert.IsFalse(s.HasPending); Assert.IsFalse(s.HasGroup);
        }
    }
}
