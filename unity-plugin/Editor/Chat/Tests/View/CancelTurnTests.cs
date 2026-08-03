// TDD — F20: Stop button + Esc hotkey to cancel a running chat turn.
// Tests verify CancelTurn() resets activity state from any active phase.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CancelTurnTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static ChatActivityState GetActivity(MCPChatWindow w)
        {
            var field = typeof(MCPChatWindow).GetField("_activity",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (ChatActivityState)field.GetValue(w);
        }

        [Test]
        public void CancelTurn_WhenIdle_NoOp()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window.CancelTurn();
                Assert.AreEqual(ActivityPhase.Idle, GetActivity(window).Phase);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void CancelTurn_WhenSending_ResetsToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var activity = GetActivity(window);
                activity.Send();
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase);

                window.CancelTurn();

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void CancelTurn_WhenReceiving_ResetsToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var activity = GetActivity(window);
                activity.Send();
                activity.FirstToken();
                Assert.AreEqual(ActivityPhase.Receiving, activity.Phase);

                window.CancelTurn();

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void CancelTurn_Idempotent()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var activity = GetActivity(window);
                activity.Send();
                window.CancelTurn();
                Assert.DoesNotThrow(() => window.CancelTurn());
                Assert.AreEqual(ActivityPhase.Idle, activity.Phase);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
