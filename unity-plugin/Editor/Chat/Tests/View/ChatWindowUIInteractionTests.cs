// ChatWindowUIInteractionTests — 25 programmatic-value and callback tests (tests 76-100).
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowUIInteractionTests : RealWindowFixture
    {
        static FieldInfo FM(string n) =>
            typeof(MCPChatWindow).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

        void IfTF(System.Action<UnityEngine.UIElements.TextField> a)
            => a(InputField());

        [Test] public void TextField_SetValue_ValueUpdated()
            => IfTF(tf => { tf.value = "hello"; Assert.AreEqual("hello", tf.value); });

        [Test] public void TextField_SetUnicode_Preserved()
            => IfTF(tf => { tf.value = "こんにちは\U0001F30D"; Assert.AreEqual("こんにちは\U0001F30D", tf.value); });


        [Test] public void SetMode_Agent_SetsAgentModeTrue()
        { SetMode(true); Assert.IsTrue((bool)FM("_agentMode").GetValue(W)); }

        [Test] public void SetMode_Agent_AgentBtnGetsActiveClass()
        { var btn = AgentBtn(); SetMode(true); Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--active")); }

        [Test] public void SetMode_Agent_AskBtnLosesActiveClass()
        { var btn = AskBtn(); SetMode(true); Assert.IsFalse(btn.ClassListContains("mode-toggle-btn--active")); }

        [Test] public void SetMode_Ask_AfterAgent_ResetsToAsk()
        { SetMode(true); SetMode(false); Assert.IsFalse((bool)FM("_agentMode").GetValue(W)); }

        [Test] public void SetMode_Ask_AskBtnHasActiveClass()
        { var btn = AskBtn(); SetMode(true); SetMode(false); Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--active")); }

        [Test] public void SetMode_Ask_10x_StaysInAskMode()
        { for (int i = 0; i < 10; i++) SetMode(false); Assert.IsFalse((bool)FM("_agentMode").GetValue(W)); }

        [Test] public void SetMode_Agent_10x_StaysInAgentMode()
        { for (int i = 0; i < 10; i++) SetMode(true); Assert.IsTrue((bool)FM("_agentMode").GetValue(W)); }

        [Test] public void ResetTokenCounters_ClearsTokenReadout()
        {
            var lbl = TokenLabel();
            FM("_inputTokens").SetValue(W, 999); W.ResetTokenCounters();
            Assert.AreEqual("", lbl.text);
        }

        [Test] public void ResetTokenCounters_10x_NoException()
            => Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) W.ResetTokenCounters(); });

        [Test] public void TextField_SetThenClear_IsEmpty()
            => IfTF(tf => { tf.value = "hello world"; tf.value = ""; Assert.AreEqual("", tf.value); });

        [Test] public void AgentDropdown_HasAtLeastOneChoice()
        { var dd = AgentDrop(); Assert.Greater(dd.choices.Count, 0); }

        [Test] public void AgentDropdown_FirstChoice_NotEmpty()
        { var dd = AgentDrop(); Assert.IsFalse(string.IsNullOrEmpty(dd.choices[0])); }

        [Test] public void AgentDropdown_SetValueToFirst_NoException()
        { var dd = AgentDrop(); Assert.DoesNotThrow(() => dd.SetValueWithoutNotify(dd.choices[0])); }

        [Test] public void AgentDropdown_SetValueToInvalid_NoException()
        { var dd = AgentDrop(); Assert.DoesNotThrow(() => dd.SetValueWithoutNotify("__invalid__does_not_exist")); }
    }
}
