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

        // ── T3.1: AbortCurrentTurnIfActive ────────────────────────────────────
        // After switching backend mid-turn, input must be available immediately —
        // not after the 120s ReloadGuard watchdog. The observable proxy is
        // _activity.Phase == Idle; if the phase stays non-Idle the send button
        // stays disabled and the chat is stuck until the watchdog fires.

        [Test]
        public void AbortCurrentTurnIfActive_WhenSending_ResetsPhaseToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var activity = GetActivity(window);
                activity.Send();
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase, "precondition: Sending");

                var method = typeof(MCPChatWindow).GetMethod(
                    "AbortCurrentTurnIfActive",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(method, "AbortCurrentTurnIfActive helper must exist");
                method.Invoke(window, null);

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,
                    "backend switch must abort turn immediately — no 120s watchdog needed");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void AbortCurrentTurnIfActive_WhenIdle_IsNoOp()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var method = typeof(MCPChatWindow).GetMethod(
                    "AbortCurrentTurnIfActive",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(method, "AbortCurrentTurnIfActive helper must exist");
                Assert.DoesNotThrow(() => method.Invoke(window, null));
                Assert.AreEqual(ActivityPhase.Idle, GetActivity(window).Phase);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
