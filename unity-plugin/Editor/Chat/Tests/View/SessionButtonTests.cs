// TDD — F25: Direct Clear Without Submenu.
// Verifies BuildSessionMenuButton returns a button with the correct tooltip
// after removing the GenericMenu dropdown layer.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SessionButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Phase 3: tooltip updated to "Session menu" (now a dropdown with New Session + Resume CLI).
        [Test]
        public void BuildSessionMenuButton_HasCorrectTooltip()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var btn = window.BuildSessionMenuButton();
                Assert.AreEqual("Session menu", btn.tooltip);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BuildSessionMenuButton_IsButton()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var btn = window.BuildSessionMenuButton();
                Assert.IsNotNull(btn);
                Assert.IsInstanceOf<Button>(btn);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BuildSessionMenuButton_HasChatBtnClass()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var btn = window.BuildSessionMenuButton();
                Assert.IsTrue(btn.ClassListContains("chat-btn"),
                    "Button must have 'chat-btn' CSS class");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BuildSessionMenuButton_HasHamburgerText()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var btn = window.BuildSessionMenuButton();
                Assert.AreEqual("☰", btn.text);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
