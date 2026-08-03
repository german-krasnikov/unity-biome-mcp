// Tests for MdBlock factories. Pure, NUnit-testable.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MdBlockTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Para_Factory_SetsLines()
        {
            var lines = new List<string> { "hello", "world" };
            var b = MdBlock.Para(lines);
            Assert.AreEqual(MdBlockKind.Paragraph, b.Kind);
            Assert.AreEqual(2, b.Lines.Count);
            Assert.AreEqual("hello", b.Lines[0]);
        }

        [Test]
        public void Heading_Factory_SetsLevel()
        {
            var b = MdBlock.Heading(2, "Title");
            Assert.AreEqual(MdBlockKind.Heading, b.Kind);
            Assert.AreEqual(2, b.Level);
            Assert.AreEqual(1, b.Lines.Count);
            Assert.AreEqual("Title", b.Lines[0]);
        }

        [Test]
        public void Code_Factory_SetsLang()
        {
            var body = new List<string> { "int x = 0;" };
            var b = MdBlock.Code("csharp", body);
            Assert.AreEqual(MdBlockKind.CodeFence, b.Kind);
            Assert.AreEqual("csharp", b.Lang);
            Assert.AreEqual(1, b.Lines.Count);
        }

        [Test]
        public void Table_Factory_SetsRows()
        {
            var rows = new List<string[]>
            {
                new[] { "A", "B" },
                new[] { "1", "2" },
            };
            var b = MdBlock.Table(rows);
            Assert.AreEqual(MdBlockKind.Table, b.Kind);
            Assert.AreEqual(2, b.TableRows.Count);
            Assert.AreEqual("A", b.TableRows[0][0]);
        }

        [Test]
        public void Image_Factory_SetsSrcAndAlt()
        {
            var b = MdBlock.Image("path/img.png", "my alt");
            Assert.AreEqual(MdBlockKind.Image, b.Kind);
            Assert.AreEqual("path/img.png", b.Src);
            Assert.AreEqual("my alt", b.Alt);
        }
    }
}
