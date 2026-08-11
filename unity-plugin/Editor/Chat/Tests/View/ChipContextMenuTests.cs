// TDD — Per-kind context menu items (T6, T7, T8).
// Tests that each built-in IChipKindProvider appends the correct menu items.
// DropdownMenu requires UIElements runtime — we use a fake menu that captures action names.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipContextMenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => ChipKindRegistry.ResetForTests();

        [TearDown]
        public void TearDown() => ChipKindRegistry.ResetForTests();

        // T6: AssetChipProvider appends "Ping in Project" and "Open"
        [Test]
        public void AssetProvider_AppendContextMenu_HasPingAndOpen()
        {
            var provider = ChipKindRegistry.ForKey(ChipKindKeys.Asset);
            Assert.IsNotNull(provider);

            var menu   = new DropdownMenu();
            provider.AppendContextMenuItems(menu, "Assets/Foo.mat");
            var items  = GetActionNames(menu);

            Assert.Contains("Ping in Project", items, $"Items: {string.Join(", ", items)}");
            Assert.Contains("Open",            items, $"Items: {string.Join(", ", items)}");
        }

        // T7: HierarchyChipProvider appends "Select in Hierarchy" and "Frame in Scene View"
        [Test]
        public void HierarchyProvider_AppendContextMenu_HasSelectAndFrame()
        {
            var provider = ChipKindRegistry.ForKey(ChipKindKeys.Hierarchy);
            Assert.IsNotNull(provider);

            var menu  = new DropdownMenu();
            provider.AppendContextMenuItems(menu, "/Root/Player");
            var items = GetActionNames(menu);

            Assert.Contains("Select in Hierarchy", items, $"Items: {string.Join(", ", items)}");
            Assert.Contains("Frame in Scene View", items, $"Items: {string.Join(", ", items)}");
        }

        // T8: ScriptChipProvider appends "Ping in Project" and "Open in IDE"
        [Test]
        public void ScriptProvider_AppendContextMenu_HasOpenIDE()
        {
            var provider = ChipKindRegistry.ForKey(ChipKindKeys.Script);
            Assert.IsNotNull(provider);

            var menu  = new DropdownMenu();
            provider.AppendContextMenuItems(menu, "Assets/Scripts/Foo.cs");
            var items = GetActionNames(menu);

            Assert.Contains("Ping in Project", items, $"Items: {string.Join(", ", items)}");
            Assert.Contains("Open in IDE",     items, $"Items: {string.Join(", ", items)}");
        }

        [Test]
        public void FolderProvider_AppendContextMenu_HasOpenInProject()
        {
            var provider = ChipKindRegistry.ForKey(ChipKindKeys.Folder);
            Assert.IsNotNull(provider);

            var menu  = new DropdownMenu();
            provider.AppendContextMenuItems(menu, "Assets/Textures");
            var items = GetActionNames(menu);

            Assert.Contains("Open in Project", items, $"Items: {string.Join(", ", items)}");
        }

        // ── helper: extract action names from DropdownMenu ────────────────────

        private static List<string> GetActionNames(DropdownMenu menu)
        {
            var names = new List<string>();
            // DropdownMenu.MenuItems() returns IList<DropdownMenuItem>
            foreach (var item in menu.MenuItems())
            {
                if (item is DropdownMenuAction action)
                    names.Add(action.name);
            }
            return names;
        }
    }
}
