// TDD T2.2b — HierarchyCard: IToolCardRenderer for get_hierarchy.
//
// Double-red requirement:
//   A — corrupt any Assert → RED
//   B — unregister HierarchyCard → registration test RED
//       OR strip node-rendering code → structural tests RED
//
// T2.5: Inherits ToolCardTestBase for shared registration / OnStart / grouper helpers.
//
// Data: real hierarchy strings from HierarchySerializer format.
// See HierarchyResultParserTests for parser-layer coverage.
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HierarchyCardTests : ToolCardTestBase
    {
        // ── Registration (RED B: fails if [InitializeOnLoad] register removed) ─

        [Test]
        public void HierarchyCard_IsRegisteredForGetHierarchy() =>
            AssertRegistered("get_hierarchy", typeof(HierarchyCard));

        // ── OnStart ────────────────────────────────────────────────────────────

        [Test]
        public void OnStart_DoesNotModifyChip() =>
            AssertOnStartIsNoop(new HierarchyCard(), "get_hierarchy");

        // ── Marker-last correctness ────────────────────────────────────────────
        // RED if marker is set before rendering (premature mark blocks 2nd call)

        [Test]
        public void OnUpdate_NullResult_DoesNotSetMarker_AllowsLaterRender()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();

            // First call: result not arrived
            card.OnUpdate(chip, new ToolCallRecord("get_hierarchy", "id-1", "{}",
                resultText: null));
            Assert.IsFalse(chip.ClassListContains("hierarchy-rendered"),
                "Marker must NOT be set when result is null — blocks re-render if set early");

            // Second call: result arrived — must still render
            card.OnUpdate(chip, new ToolCallRecord("get_hierarchy", "id-1", "{}",
                resultText: "PlayerShip $A1B2C3"));
            Assert.IsTrue(chip.ClassListContains("hierarchy-rendered"),
                "Marker set after render");
            Assert.IsTrue(chip.childCount > 0,
                "Nodes rendered on second call (proves marker was set last, not first)");
        }

        // ── NO_CHANGE result ───────────────────────────────────────────────────

        [Test]
        public void OnUpdate_NoChange_SetsMarkerButNoRows()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-2", "{}",
                resultText: "NO_CHANGE");
            card.OnUpdate(chip, rec);
            Assert.IsTrue(chip.ClassListContains("hierarchy-rendered"),
                "hierarchy-rendered must be set for NO_CHANGE");
            Assert.AreEqual(0, chip.childCount, "NO_CHANGE must not add any rows");
        }

        // ── Single node renders ────────────────────────────────────────────────

        [Test]
        public void OnUpdate_SingleNode_OneRowWithHierarchyNodeClass()
        {
            // "Main Camera $AABBCC" — typical root node from a Unity scene
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-3", "{}",
                resultText: "Main Camera $AABBCC");
            card.OnUpdate(chip, rec);
            Assert.AreEqual(1, chip.childCount, "Exactly one child for single node");
            Assert.IsTrue(chip[0].ClassListContains("hierarchy-node"),
                "Child row must have 'hierarchy-node' CSS class");
        }

        [Test]
        public void OnUpdate_SingleNode_LabelContainsObjectName()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-4", "{}",
                resultText: "Directional Light $D1E2F3");
            card.OnUpdate(chip, rec);
            var label = chip[0].Q<Label>();
            Assert.IsNotNull(label, "Node row must contain a Label");
            Assert.AreEqual("Directional Light", label.text,
                "Label text must match object name (no hex ref or flags)");
        }

        // ── Inactive object styling ────────────────────────────────────────────

        [Test]
        public void OnUpdate_InactiveNode_HasHierarchyInactiveClass()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-5", "{}",
                resultText: "DisabledEnemy $E3F4G5 !");
            card.OnUpdate(chip, rec);
            Assert.IsTrue(chip[0].ClassListContains("hierarchy-inactive"),
                "Inactive object row must have 'hierarchy-inactive' CSS class");
        }

        // ── Scene header styling ───────────────────────────────────────────────

        [Test]
        public void OnUpdate_SceneHeader_HasHierarchySceneHeaderClass()
        {
            // Multi-scene output: header then root object
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-6", "{}",
                resultText: "[GameLevel]\nPlayerObject $F5G6H7");
            card.OnUpdate(chip, rec);
            Assert.GreaterOrEqual(chip.childCount, 1, "At least the header row");
            Assert.IsTrue(chip[0].ClassListContains("hierarchy-scene-header"),
                "Scene header row must have 'hierarchy-scene-header' CSS class");
        }

        // ── 20-node truncation ─────────────────────────────────────────────────

        [Test]
        public void OnUpdate_TwentyFiveNodes_Shows20RowsPlusShowMore()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 25; i++)
                sb.AppendLine($"Object{i:D2} $A{i:D6}");

            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-7", "{}",
                resultText: sb.ToString());
            card.OnUpdate(chip, rec);

            var rows     = chip.Query(className: "hierarchy-node").ToList();
            var showMore = chip.Q(className: "hierarchy-show-more");
            Assert.AreEqual(20, rows.Count,
                "Exactly 20 node rows visible before 'show more'");
            Assert.IsNotNull(showMore,
                "'hierarchy-show-more' label must be present when nodes > 20");
        }

        [Test]
        public void OnUpdate_TwentyFiveNodes_ShowMoreLabelMentionsFiveMore()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 25; i++)
                sb.AppendLine($"Object{i:D2} $A{i:D6}");

            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-8", "{}",
                resultText: sb.ToString());
            card.OnUpdate(chip, rec);

            var showMore = chip.Q<Label>(className: "hierarchy-show-more");
            Assert.IsNotNull(showMore, "show-more label must exist");
            Assert.IsTrue(showMore.text.Contains("5"),
                "Label text must mention '5' (the remaining count)");
        }

        // ── Idempotency ────────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_CalledTwice_ExactlyOneSetOfRows()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-9", "{}",
                resultText: "RootObject $G6H7I8");
            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec); // second call must be no-op
            Assert.AreEqual(1, chip.childCount,
                "Second OnUpdate must not add duplicate rows (idempotency)");
        }

        // ── Empty parse result ─────────────────────────────────────────────────

        [Test]
        public void OnUpdate_BlankLines_RenderedMarkerSetNoRows()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-10", "{}",
                resultText: "\n\n\n");
            card.OnUpdate(chip, rec);
            Assert.IsTrue(chip.ClassListContains("hierarchy-rendered"),
                "Marker must be set even when result parses to zero nodes");
            Assert.AreEqual(0, chip.childCount, "No rows for blank-lines result");
        }

        // ── Nav binding ────────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_NodeWithHexRef_HasNavBinding_NoNoNavClass()
        {
            // Node with valid hex ref → click handler wired, no --no-nav class.
            // RED B: if NavBindingHelper.Attach is removed, the --no-nav class would be added.
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-11", "{}",
                resultText: "PlayerCharacter $A1B2C3");
            card.OnUpdate(chip, rec);
            var row = chip.Q(className: "hierarchy-node");
            Assert.IsNotNull(row, "Node row must exist");
            Assert.IsFalse(row.ClassListContains("hierarchy-node--no-nav"),
                "Node with hex ref must have nav binding (no --no-nav class)");
        }

        // ── Grouper bypass: real HierarchyCard triggers card-chip ─────────────

        [Test]
        public void TwoHierarchyChips_BothVisibleInFeed_NotAbsorbedByGrouper() =>
            AssertGrouperBypass("get_hierarchy", "h-1", "h-2");

        // ── Truncated result (2000-char limit from T0.1) ───────────────────────

        [Test]
        public void OnUpdate_TruncatedResultMidLine_DoesNotCrash_RendersAvailableNodes()
        {
            // Simulate T0.1 truncation cutting mid-line before a valid node
            var input = "Main Camera $AABBCC\nDirectional Light $DDEEFF\n│  └─ Partial";
            var card  = new HierarchyCard();
            var chip  = new VisualElement();
            var rec   = new ToolCallRecord("get_hierarchy", "id-12", "{}",
                resultText: input);
            card.OnUpdate(chip, rec);

            var rows = chip.Query(className: "hierarchy-node").ToList();
            Assert.AreEqual(2, rows.Count,
                "Partial last line must be silently skipped; 2 valid nodes rendered");
            Assert.IsTrue(chip.ClassListContains("hierarchy-rendered"),
                "Card marked as rendered even with partial input");
        }
    }
}
