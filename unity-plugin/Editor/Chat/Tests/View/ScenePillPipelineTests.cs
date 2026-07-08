// TDD — F15b: scene objects in LLM responses rendered as pills (full pipeline).
// Verifies SceneObjects delegate → FreezeAssistantBubble → pill in assistant bubble.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ScenePillPipelineTests
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            // Ensure kill-switch is off — EditorPrefs persists across sessions.
            UnityEditor.EditorPrefs.DeleteKey("MCPChat.DisableSceneNameNorm");
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            ChipPillFactory.AddToContextAction = null;
            UnityEditor.EditorPrefs.DeleteKey("MCPChat.DisableSceneNameNorm");
        }

        // F15b-C1: scene object name in LLM response → rendered as pill in assistant bubble
        [Test]
        public void SceneObjectName_RenderedAsPill()
        {
            _transcript.SceneObjects = () => new Dictionary<string, string> { { "EnemyShip", "/EnemyShip" } };
            _transcript.AppendOrExtendAssistant("The EnemyShip is broken");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
            var pill = bubble.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "Scene object 'EnemyShip' should be rendered as pill");
        }

        // F15b-C2: unknown name (not in SceneObjects) → no pill created
        [Test]
        public void UnknownName_NoPill()
        {
            _transcript.SceneObjects = () => new Dictionary<string, string> { { "EnemyShip", "/EnemyShip" } };
            _transcript.AppendOrExtendAssistant("The Floor is fine");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
            var pill = bubble.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "Unknown name 'Floor' must not produce a pill");
        }

        // F15b-C3: scene object name inside code block → not turned into pill
        [Test]
        public void SceneObjectInCodeBlock_NoPill()
        {
            _transcript.SceneObjects = () => new Dictionary<string, string> { { "EnemyShip", "/EnemyShip" } };
            _transcript.AppendOrExtendAssistant("```\nEnemyShip.Destroy();\n```");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
            var pill = bubble.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "Name inside code block must not become a pill");
        }

        // F15b-C4: already-tagged ref → one pill only (not double-pilled)
        [Test]
        public void AlreadyTagged_OnePillOnly()
        {
            _transcript.SceneObjects = () => new Dictionary<string, string> { { "EnemyShip", "/EnemyShip" } };
            _transcript.AppendOrExtendAssistant("check [hierarchy:/EnemyShip] now");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
            var pills = bubble.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(1, pills.Count, "Should have exactly one pill, not double-pilled");
        }

        // F15b-C5: SceneObjects null → no crash, text rendered normally
        [Test]
        public void SceneObjectsNull_NoException()
        {
            _transcript.SceneObjects = null;
            _transcript.AppendOrExtendAssistant("just some text");
            Assert.DoesNotThrow(() => _transcript.FinalizeAssistant());

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
        }
    }
}
