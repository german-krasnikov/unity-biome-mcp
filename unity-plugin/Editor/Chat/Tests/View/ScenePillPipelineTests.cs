// TDD — F15b: scene objects in LLM responses rendered as pills (full pipeline).
// Verifies SceneObjects delegate → FreezeAssistantBubble → pill in assistant bubble.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ScenePillPipelineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
            // Ensure kill-switch is off — EditorPrefs persists across sessions.
            DeleteEditorPrefBool(PrefKeys.DisableSceneNameNorm);
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
            ChipPillFactory.AddToContextAction = null;
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

        // T4.2 fix: verify that clicking a scene pill navigates to the correct hierarchy path.
        // Previous tests only checked IsNotNull(pill) — they passed even with navigate
        // registration removed from MixedParagraphRenderer.BuildPill.
        // Pattern mirrors MutationDiffCardRenderTests.Navigate_ClickRow_DispatchesToHierarchyProvider.
        [Test]
        public void SceneObjectPill_Click_NavigatesToHierarchyProvider()
        {
            LogAssert.ignoreFailingMessages = true;
            string captured = null;

            // SetUp called ResetForTests() → built-ins registered including HierarchyChipProvider.
            // Unregister it so our lambda (keep-first policy) is accepted instead.
            ChipKindRegistry.Unregister(ChipKindKeys.Hierarchy);
            ChipKindRegistry.Register(new LambdaChipProvider(ChipKindKeys.Hierarchy, r => captured = r));

            // Attach _container to a live panel so ClickEvent dispatches correctly.
            var window = CreateOwnedEditorWindow<ScenePillNavTestWindow>();
            window.ShowUtility();
            window.rootVisualElement.Add(_container);

            _transcript.SceneObjects = () => new Dictionary<string, string> { { "EnemyShip", "/EnemyShip" } };
            _transcript.AppendOrExtendAssistant("The EnemyShip needs repair");
            _transcript.FinalizeAssistant();

            var bubble = _container.Q(className: "msg-bubble--assistant");
            Assert.IsNotNull(bubble, "Assistant bubble must exist");
            var pill = bubble.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "Scene object pill must exist for navigation test");

            SendClick(pill, 1);

            Assert.AreEqual("/EnemyShip", captured,
                "Clicking scene pill must invoke hierarchy provider Navigate with the object path");
        }

        // ── Inner helpers ─────────────────────────────────────────────────────────

        private sealed class ScenePillNavTestWindow : EditorWindow { }

        private class LambdaChipProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly Action<string> _navigate;
            public LambdaChipProvider(string key, Action<string> navigate) { _key = key; _navigate = navigate; }
            public string   Key                => _key;
            public int      Priority           => 500;
            public string   HexColor           => "#000000";
            public string   IconName           => "";
            public string   DefaultDepth       => "path";
            public string[] BarePathExtensions => Array.Empty<string>();
            public bool     CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath) => default;
            public string   FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";
            public void     Navigate(string reference) => _navigate?.Invoke(reference);
            public void     Ping(string reference) { }
            public void     AppendContextMenuItems(DropdownMenu menu, string reference) { }
        }

        private static void SendClick(VisualElement target, int clickCount)
        {
            var evt = new ClickEvent();
            SetClickCount(evt, clickCount);
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SetClickCount(ClickEvent evt, int count)
        {
            var type = evt.GetType();
            while (type != null && type != typeof(object))
            {
                var field = type.GetField("<clickCount>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) { field.SetValue(evt, count); return; }
                type = type.BaseType;
            }
        }
    }
}
