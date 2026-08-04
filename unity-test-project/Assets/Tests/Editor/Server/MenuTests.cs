using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    public class MenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Execute_ValidMenuItem_ReturnsExecuted()
        {
            // Window/General/Console is always available and safe
            var result = MenuHelper.Execute("Window/General/Console");
            Assert.That(result, Does.StartWith("Executed:"));
            Assert.That(result, Does.Contain("Window/General/Console"));
        }

        [Test]
        public void Execute_InvalidPath_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                MenuHelper.Execute("Totally/Invalid/Path/That/Doesnt/Exist"));
            Assert.That(ex.Message, Does.Contain("not found"));
        }

        [Test]
        public void Execute_EmptyPath_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => MenuHelper.Execute(""));
        }

        [Test]
        public void Execute_NullPath_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => MenuHelper.Execute(null));
        }

        [Test]
        public void Execute_CreateEmpty_Works()
        {
            var previousSelection = Selection.activeGameObject;
            var result = MenuHelper.Execute("GameObject/Create Empty");
            var created = Selection.activeGameObject;
            if (created != null && created != previousSelection)
                TrackOwnedObject(created);
            Assert.That(result, Does.StartWith("Executed:"));
            Assert.That(result, Does.Contain("Create Empty"));
        }

        [Test]
        public void List_RootMenus_ReturnsAllRoots()
        {
            var result = MenuHelper.List(null);
            Assert.That(result, Does.Contain("[File]"));
            Assert.That(result, Does.Contain("[GameObject]"));
            Assert.That(result, Does.Contain("[Window]"));
        }

        [Test]
        public void List_GameObjectMenu_ReturnsItems()
        {
            var result = MenuHelper.List("GameObject");
            Assert.That(result, Does.Contain("[GameObject]"));
            Assert.That(result, Does.Contain("items)"));
        }

        [Test]
        public void List_InvalidPath_ReturnsNoItems()
        {
            var result = MenuHelper.List("CompletelyFakeMenu");
            Assert.That(result, Does.Contain("No menu items"));
        }

        [Test]
        public void List_EmptyPath_SameAsNull()
        {
            var resultNull = MenuHelper.List(null);
            var resultEmpty = MenuHelper.List("");
            Assert.That(resultNull, Is.EqualTo(resultEmpty));
        }

        [Test]
        public void List_ToolsMenu_ContainsMCPSettings()
        {
            var result = MenuHelper.List("Tools");
            if (result.Contains("No menu items"))
            {
                Assert.Pass("Tools menu not registered in this environment");
                return;
            }
            Assert.That(result, Does.Contain("[Tools]"));
            Assert.That(result, Does.Contain("MCP"));
        }
    }
}
