// TDD — T-4.2: MutationDiffCard tests.
// Canonical pattern: construct directly, query by CSS class, no resolvedStyle.
// Navigate test requires EditorWindow so ClickEvent has a panel to dispatch through.
using System;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MutationDiffCardRenderTests : UnityMcpTestBase
    {
        private MutationDiffCard _card;

        [SetUp]
        public void SetUp() => _card = new MutationDiffCard();

        private const string SetPropertyJson =
            "{\"path\":\"/Hero\",\"component\":\"Health\",\"prop\":\"maxHealth\",\"value\":\"150\"}";

        private static ToolCallRecord MakeRec(
            string toolName, string argsJson, bool withResult = false)
        {
            return withResult
                ? new ToolCallRecord(toolName, "id-1", argsJson,
                    "maxHealth = 150 (was 100)", true)
                : new ToolCallRecord(toolName, "id-1", argsJson);
        }

        // Collects all Label text from element subtree (including the element if it is a Label).
        private static string GetEntryText(VisualElement entry)
        {
            var sb = new StringBuilder();
            entry.Query<Label>().ForEach(l => sb.Append(l.text));
            return sb.ToString();
        }

        // ── Test 1-2: RendersFromArgs (parameterized) ─────────────────────────────

        [TestCase(false)]
        [TestCase(true)]
        public void OnUpdate_SetProperty_RendersFromArgs(bool withResult)
        {
            var chip = new VisualElement();
            var rec  = MakeRec("set_property", SetPropertyJson, withResult);

            _card.OnUpdate(chip, rec);

            var entry = chip.Q(className: "mutation-entry");
            Assert.IsNotNull(entry, "mutation-entry must exist after OnUpdate with set_property");
            var text = GetEntryText(entry);
            StringAssert.Contains("maxHealth", text,
                "Entry text must contain the property name");
            StringAssert.Contains("150", text,
                "Entry text must contain the new value");
        }

        // ── Test 3: was-value populated from result ───────────────────────────────

        [Test]
        public void OnUpdate_SetProperty_WithResult_ShowsWasValue()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("set_property", SetPropertyJson, withResult: true);

            _card.OnUpdate(chip, rec);

            var wasLabel = chip.Q<Label>(className: "mutation-was");
            Assert.IsNotNull(wasLabel, ".mutation-was Label must exist");
            StringAssert.Contains("100", wasLabel.text,
                ".mutation-was text must contain the was-value extracted from '(was 100)'");
        }

        // ── Test 4: placeholder shown when no result ──────────────────────────────

        [Test]
        public void OnUpdate_SetProperty_WithoutResult_ShowsPlaceholder()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("set_property", SetPropertyJson, withResult: false);

            _card.OnUpdate(chip, rec);

            var wasLabel = chip.Q<Label>(className: "mutation-was");
            Assert.IsNotNull(wasLabel, ".mutation-was Label must exist even without result");
            StringAssert.Contains("?", wasLabel.text,
                ".mutation-was must show '?' placeholder when result is not yet available");
        }

        // ── Test 5: create_object renders add row ─────────────────────────────────

        [Test]
        public void OnUpdate_CreateObject_RendersAddRow()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("create_object", "{\"name\":\"Enemy\"}");

            _card.OnUpdate(chip, rec);

            var entry = chip.Q(className: "mutation-entry");
            Assert.IsNotNull(entry, "mutation-entry must exist for create_object");
            var text = GetEntryText(entry);
            StringAssert.Contains("+", text,
                "Create row must use '+' prefix");
            StringAssert.Contains("Enemy", text,
                "Create row must contain the object name");
        }

        // ── Test 6: idempotent — second call must not duplicate entries ───────────

        [Test]
        public void OnUpdate_Idempotent()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("set_property", SetPropertyJson);

            _card.OnUpdate(chip, rec);
            _card.OnUpdate(chip, rec); // second call: simulates ArgsComplete → Result stage

            var count = chip.Query(className: "mutation-entry").ToList().Count;
            Assert.AreEqual(1, count,
                "OnUpdate called twice must not double .mutation-entry elements");
        }

        // ── Test 7: click row → hierarchy navigation ──────────────────────────────
        // ClickEvent requires a panel: attach chip to a real EditorWindow.

        [Test]
        public void Navigate_ClickRow_DispatchesToHierarchyProvider()
        {
            LogAssert.ignoreFailingMessages = true;
            string captured = null;
            // Remove the built-in hierarchy provider (keep-first policy would block our lambda).
            // _chipKindIsolation in UnityMcpTestBase restores the registry at TearDown.
            ChipKindRegistry.Unregister(ChipKindKeys.Hierarchy);
            ChipKindRegistry.Register(
                new LambdaChipProvider(ChipKindKeys.Hierarchy, r => captured = r));

            var window = CreateOwnedEditorWindow<MutationNavTestWindow>();
            window.ShowUtility();

            var chip = new VisualElement();
            window.rootVisualElement.Add(chip);

            var rec = MakeRec("set_property", SetPropertyJson);
            _card.OnUpdate(chip, rec);

            var entry = chip.Q(className: "mutation-entry");
            Assert.IsNotNull(entry, "mutation-entry must exist for navigation test");

            var evt = new ClickEvent { target = entry };
            entry.SendEvent(evt);

            Assert.AreEqual("/Hero", captured,
                "Clicking mutation-entry must dispatch hierarchy navigation with the object path");
        }

        // ── Inner helpers ─────────────────────────────────────────────────────────

        private sealed class MutationNavTestWindow : EditorWindow { }

        // Minimal IChipKindProvider forwarding Navigate to a lambda (same pattern as NavBindingHelperTests).
        private class LambdaChipProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly Action<string> _navigate;

            public LambdaChipProvider(string key, Action<string> navigate)
            {
                _key = key; _navigate = navigate;
            }

            public string   Key              => _key;
            public int      Priority         => 500;
            public string   IconName         => "";
            public string   HexColor         => "#000000";
            public string   DefaultDepth     => "summary";
            public string[] BarePathExtensions => Array.Empty<string>();
            public bool     CanHandle(UnityEngine.Object obj, string path) => false;
            public ChipData Create(UnityEngine.Object obj, string path) => default;
            public string   FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";
            public void     Navigate(string reference) => _navigate?.Invoke(reference);
            public void     Ping(string reference) { }
            public void     AppendContextMenuItems(DropdownMenu menu, string reference) { }
        }
    }
}
