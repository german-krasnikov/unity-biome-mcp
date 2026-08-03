using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationHistoryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private AnnotationHistory _history;
        private IAnnotationCommand _cmd;

        [SetUp]
        public void SetUp()
        {
            _history = new AnnotationHistory();
            _cmd = new ArrowCommand(new Color32(255, 0, 0, 255), 2f,
                new Vector2(0f, 0f), new Vector2(1f, 1f));
        }

        [Test]
        public void Empty_CannotUndo()
        {
            Assert.IsFalse(_history.CanUndo);
        }

        [Test]
        public void Empty_CannotRedo()
        {
            Assert.IsFalse(_history.CanRedo);
        }

        [Test]
        public void Add_IncrementsCount()
        {
            _history.Add(_cmd);
            Assert.AreEqual(1, _history.Count);
        }

        [Test]
        public void Undo_DecrementsCount()
        {
            _history.Add(_cmd);
            _history.Undo();
            Assert.AreEqual(0, _history.Count);
        }

        [Test]
        public void Redo_IncrementsCountAfterUndo()
        {
            _history.Add(_cmd);
            _history.Undo();
            _history.Redo();
            Assert.AreEqual(1, _history.Count);
        }

        [Test]
        public void Add_AfterUndo_TruncatesRedoBranch()
        {
            var cmd2 = new LineCommand(new Color32(0, 0, 255, 255), 1f,
                Vector2.zero, Vector2.one);

            _history.Add(_cmd);
            _history.Undo();
            _history.Add(cmd2);

            Assert.IsFalse(_history.CanRedo, "redo branch must be truncated after new Add");
            Assert.AreEqual(1, _history.Count);
            Assert.AreSame(cmd2, _history.Active[0]);
        }

        [Test]
        public void Active_ReturnsOnlyUpToCursor()
        {
            var cmd2 = new LineCommand(new Color32(0, 0, 255, 255), 1f,
                Vector2.zero, Vector2.one);

            _history.Add(_cmd);
            _history.Add(cmd2);
            _history.Undo(); // cursor at 1

            var active = _history.Active;
            Assert.AreEqual(1, active.Count);
            Assert.AreSame(_cmd, active[0]);
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            _history.Add(_cmd);
            _history.Clear();

            Assert.AreEqual(0, _history.Count);
            Assert.IsFalse(_history.CanUndo);
            Assert.IsFalse(_history.CanRedo);
        }

        [Test]
        public void MultipleUndos_ClampAtZero()
        {
            _history.Add(_cmd);
            _history.Undo();
            _history.Undo(); // extra — must not go negative
            _history.Undo();

            Assert.AreEqual(0, _history.Count);
            Assert.IsFalse(_history.CanUndo);
        }

        [Test]
        public void MultipleRedos_ClampAtCount()
        {
            _history.Add(_cmd);
            _history.Undo();
            _history.Redo();
            _history.Redo(); // extra — must not exceed
            _history.Redo();

            Assert.AreEqual(1, _history.Count);
            Assert.IsFalse(_history.CanRedo);
        }
    }
}
