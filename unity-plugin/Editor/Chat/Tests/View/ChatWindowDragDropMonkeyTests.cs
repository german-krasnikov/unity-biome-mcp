// Monkey tests: ProcessExternalPath edge cases + chip insertion stress + drag-then-send.
// Does NOT duplicate DragDropExternalTests (null, empty, valid file, dir, no-slash,
// folder constant, null obj detect, FolderChipProvider, markdown, json, folder).
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ChatWindowDragDropMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        private (string turnJson, string rawText) Send()
            => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        private static List<(string path, string name)> Collect(string rawPath)
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath(rawPath, (_, p, n) => calls.Add((p, n)));
            return calls;
        }

        // ── ProcessExternalPath edge cases ────────────────────────────────────

        [Test] public void ExternalPath_Unicode_NameExtracted()
            { var r = Collect("/srv/日本語.cs"); Assert.AreEqual(1, r.Count); Assert.AreEqual("日本語.cs", r[0].name); }

        [Test] public void ExternalPath_PathWithSpaces_NameExtracted()
            { var r = Collect("/path/my file.cs"); Assert.AreEqual(1, r.Count); Assert.AreEqual("my file.cs", r[0].name); }

        [Test] public void ExternalPath_NoExtension_NameUsed()
            { var r = Collect("/path/MAKEFILE"); Assert.AreEqual(1, r.Count); Assert.AreEqual("MAKEFILE", r[0].name); }

        [Test] public void ExternalPath_DotFile_NameUsed()
            { var r = Collect("/path/.gitignore"); Assert.AreEqual(1, r.Count); Assert.AreEqual(".gitignore", r[0].name); }

        [Test] public void ExternalPath_DeepPath_OnlyFilenameInName()
            { var r = Collect("/a/b/c/d/e/f.cs"); Assert.AreEqual(1, r.Count); Assert.AreEqual("f.cs", r[0].name); }

        [Test]
        public void ExternalPath_10DifferentPaths_10Callbacks()
        {
            var all = new List<(string, string)>();
            for (int i = 0; i < 10; i++)
                MCPChatWindow.ProcessExternalPath($"/file{i}.cs", (_, p, n) => all.Add((p, n)));
            Assert.AreEqual(10, all.Count);
        }

        [Test]
        public void ExternalPath_SamePathTwice_TwoCallbacks()
        {
            var all = new List<(string, string)>();
            MCPChatWindow.ProcessExternalPath("/File.cs", (_, p, n) => all.Add((p, n)));
            MCPChatWindow.ProcessExternalPath("/File.cs", (_, p, n) => all.Add((p, n)));
            Assert.AreEqual(2, all.Count, "no dedup at ProcessExternalPath level");
        }

        [Test] public void ExternalPath_WindowsStyle_NoException()
        {
            Assert.DoesNotThrow(() => Collect(@"C:\Users\dev\File.cs"));
        }

        [Test] public void ExternalPath_RelativePath_NameExtracted()
            { var r = Collect("relative/path/file.txt"); Assert.AreEqual(1, r.Count); Assert.AreEqual("file.txt", r[0].name); }

        [Test] public void ExternalPath_FullPathPreserved()
            { var r = Collect("/full/path/Script.cs"); Assert.AreEqual("/full/path/Script.cs", r[0].path); }

        [Test] public void ExternalPath_Whitespace_NoException()
            { Assert.DoesNotThrow(() => Collect("   ")); }

        // ── Chip insertion stress ─────────────────────────────────────────────

        [Test] public void InsertChip_100x_NoException()
        {
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++)
                    _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, $"/O{i}", $"O{i}", i));
            });
        }

        [Test] public void InsertChip_SamePath_100x_AllInModel()
        {
            for (int i = 0; i < 100; i++)
                _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1));
            Assert.AreEqual(100, _chipField.Model.Count);
        }

        [Test] public void InsertChip_Unicode_NoException()
            { Assert.DoesNotThrow(() => _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Объект", "Объект", 1))); }

        [Test] public void InsertChip_ThenClear_CountZero()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            _chipField.ClearChips();
            Assert.AreEqual(0, _chipField.Model.Count);
        }

        // ── Drag then send ────────────────────────────────────────────────────

        [Test]
        public void DragThenSend_ChipInTurnJson()
        {
            MCPChatWindow.ProcessExternalPath("/Dev/Script.cs", (_, p, n) =>
                _chipField.AddChip(new ChipData(ChipKindKeys.Script, p, n, 0)));
            _chipField.Text = "check";
            StringAssert.Contains("/Dev/Script.cs", Send().turnJson);
        }

        [Test]
        public void DragThenSend_5Files_AllInTurnJson()
        {
            for (int i = 1; i <= 5; i++)
            {
                int j = i;
                MCPChatWindow.ProcessExternalPath($"/S{j}.cs", (_, p, n) =>
                    _chipField.AddChip(new ChipData(ChipKindKeys.Script, p, n, 0)));
            }
            _chipField.Text = "review";
            var tj = Send().turnJson;
            for (int i = 1; i <= 5; i++) StringAssert.Contains($"S{i}.cs", tj);
        }

        [Test]
        public void DragThenSend_ClearThenDrag_OnlyNewChip()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Script, "/old.cs", "old.cs", 0));
            _chipField.ClearChips();
            MCPChatWindow.ProcessExternalPath("/new.cs", (_, p, n) =>
                _chipField.AddChip(new ChipData(ChipKindKeys.Script, p, n, 0)));
            _chipField.Text = "check";
            var tj = Send().turnJson;
            StringAssert.Contains("new.cs", tj);
            Assert.IsFalse(tj.Contains("[script:/old.cs]"));
        }

        [Test]
        public void DragThenSend_TextAndChips_BothPreserved()
        {
            _chipField.Text = "review changes";
            _chipField.AddChip(new ChipData(ChipKindKeys.Script, "/Config.cs", "Config.cs", 0));
            var tj = Send().turnJson;
            StringAssert.Contains("review changes", tj);
            StringAssert.Contains("Config.cs", tj);
        }
    }
}
