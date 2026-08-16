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
                resultText: "PlayerShip &1"));
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
            // Typical root node from a Unity scene.
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-3", "{}",
                resultText: "Main Camera &1");
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
                resultText: "Directional Light &2");
            card.OnUpdate(chip, rec);
            var label = chip[0].Q<Label>();
            Assert.IsNotNull(label, "Node row must contain a Label");
            Assert.AreEqual("Directional Light", label.text,
                "Label text must match object name (no reference or flags)");
        }

        // ── Inactive object styling ────────────────────────────────────────────

        [Test]
        public void OnUpdate_InactiveNode_HasHierarchyInactiveClass()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-5", "{}",
                resultText: "DisabledEnemy &3 !");
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
                resultText: "[GameLevel]\nPlayerObject &4");
            card.OnUpdate(chip, rec);
            Assert.GreaterOrEqual(chip.childCount, 1, "At least the header row");
            Assert.IsTrue(chip[0].ClassListContains("hierarchy-scene-header"),
                "Scene header row must have 'hierarchy-scene-header' CSS class");
        }

        [Test]
        public void OnUpdate_SingleSceneSummary_RendersRawSummary()
        {
            const string result =
                "SampleScene (1 nodes)\n" +
                "  Player &1\n";
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_hierarchy", "summary-1",
                "{\"summary\":true}", resultText: result);

            card.OnUpdate(chip, rec);

            var summary = chip.Q<Label>(className: "hierarchy-summary");
            Assert.IsNotNull(summary, "A summary response must not render as a blank card");
            Assert.AreEqual(result.TrimEnd(), summary.text);
        }

        [Test]
        public void OnUpdate_MultiSceneSummary_RendersAllScenesRaw()
        {
            const string result =
                "[SceneA] (1 nodes)\n" +
                "  RootA\n" +
                "[SceneB] (1 nodes)\n" +
                "  RootB\n";
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_hierarchy", "summary-2",
                "{\"summary\":\"true\"}", resultText: result);

            card.OnUpdate(chip, rec);

            var summary = chip.Q<Label>(className: "hierarchy-summary");
            Assert.IsNotNull(summary, "A multi-scene summary must not render as a blank card");
            StringAssert.Contains("[SceneA]", summary.text);
            StringAssert.Contains("RootA", summary.text);
            StringAssert.Contains("[SceneB]", summary.text);
            StringAssert.Contains("RootB", summary.text);
        }

        // ── 20-entry truncation ────────────────────────────────────────────────

        [Test]
        public void OnUpdate_TwentyFiveNodes_Shows20RowsPlusShowMore()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 25; i++)
                sb.AppendLine($"Object{i:D2} &{i + 1}");

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
                sb.AppendLine($"Object{i:D2} &{i + 1}");

            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-8", "{}",
                resultText: sb.ToString());
            card.OnUpdate(chip, rec);

            var showMore = chip.Q<Label>(className: "hierarchy-show-more");
            Assert.IsNotNull(showMore, "show-more label must exist");
            StringAssert.Contains("5 more entries", showMore.text,
                "Label must describe the remaining capped hierarchy entries");
        }

        [Test]
        public void OnUpdate_MultiSceneCap_CountsHeadersAndObjectsAsEntries()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[SceneA]");
            for (int i = 0; i < 10; i++)
                sb.AppendLine($"SceneAObject{i:D2} &A{i}");
            sb.AppendLine("[SceneB]");
            for (int i = 0; i < 11; i++)
                sb.AppendLine($"SceneBObject{i:D2} &B{i}");

            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_hierarchy", "id-multi-cap", "{}",
                resultText: sb.ToString());
            card.OnUpdate(chip, rec);

            Assert.AreEqual(2,
                chip.Query(className: "hierarchy-scene-header").ToList().Count,
                "Both scene headers are among the first 20 entries");
            Assert.AreEqual(18, chip.Query(className: "hierarchy-node").ToList().Count,
                "The entry cap includes two headers and eighteen object rows");
            var showMore = chip.Q<Label>(className: "hierarchy-show-more");
            Assert.IsNotNull(showMore);
            StringAssert.Contains("3 more entries", showMore.text,
                "The remaining count includes every hidden hierarchy entry");
        }

        // ── Idempotency ────────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_CalledTwice_ExactlyOneSetOfRows()
        {
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-9", "{}",
                resultText: "RootObject &5");
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
        public void OnUpdate_NodeWithCompactReference_HasNavBinding_NoNoNavClass()
        {
            // Node with a canonical reference → click handler wired, no --no-nav class.
            // RED B: if NavBindingHelper.Attach is removed, the --no-nav class would be added.
            var card = new HierarchyCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_hierarchy", "id-11", "{}",
                resultText: "PlayerCharacter &6");
            card.OnUpdate(chip, rec);
            var row = chip.Q(className: "hierarchy-node");
            Assert.IsNotNull(row, "Node row must exist");
            Assert.IsFalse(row.ClassListContains("hierarchy-node--no-nav"),
                "Node with compact reference must have nav binding (no --no-nav class)");
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
            var input = "Main Camera &1\nDirectional Light &2\n│  └─ Partial";
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
