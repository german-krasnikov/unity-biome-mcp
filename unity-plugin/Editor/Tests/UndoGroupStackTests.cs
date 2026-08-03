// TDD: UndoGroupStack — Push/Clear/RevertLast without Unity Undo API (delegate injection).
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UndoGroupStackTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private List<int> _reverted;

        [SetUp]
        public void SetUp()
        {
            UndoGroupStack.Clear();
            _reverted = new List<int>();
            UndoGroupStack.RevertAction = id => _reverted.Add(id);
        }

        [TearDown]
        public void TearDown()
        {
            UndoGroupStack.Clear();
            // Restore default action so other tests are unaffected.
            UndoGroupStack.RevertAction = UndoGroupHelper.RevertToBeforeGroup;
        }

        [Test]
        public void Push_and_RevertLast_removes_entry()
        {
            UndoGroupStack.Push(42);
            Assert.AreEqual(1, UndoGroupStack.Count);

            var msg = UndoGroupStack.RevertLast(1);

            Assert.AreEqual(0, UndoGroupStack.Count);
            CollectionAssert.Contains(_reverted, 42);
            StringAssert.Contains("reverted 1", msg);
        }

        [Test]
        public void RevertLast_with_empty_stack_returns_nothing()
        {
            var msg = UndoGroupStack.RevertLast(1);

            Assert.AreEqual("nothing to undo", msg);
            Assert.AreEqual(0, _reverted.Count);
        }

        [Test]
        public void Clear_empties_stack()
        {
            UndoGroupStack.Push(1);
            UndoGroupStack.Push(2);

            UndoGroupStack.Clear();

            Assert.AreEqual(0, UndoGroupStack.Count);
        }

        [Test]
        public void RevertLast_reverts_in_reverse_order()
        {
            UndoGroupStack.Push(10);
            UndoGroupStack.Push(20);
            UndoGroupStack.Push(30);

            UndoGroupStack.RevertLast(2);

            Assert.AreEqual(2, _reverted.Count);
            Assert.AreEqual(30, _reverted[0], "last pushed reverted first");
            Assert.AreEqual(20, _reverted[1]);
            Assert.AreEqual(1, UndoGroupStack.Count, "first entry remains");
        }

        [Test]
        public void RevertLast_clamps_to_stack_size()
        {
            UndoGroupStack.Push(5);

            var msg = UndoGroupStack.RevertLast(99);

            Assert.AreEqual(1, _reverted.Count);
            StringAssert.Contains("reverted 1", msg);
            Assert.AreEqual(0, UndoGroupStack.Count);
        }
    }
}
