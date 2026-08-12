// TDD T2.4 — ComponentReadCard: IToolCardRenderer for get_component / inspect /
//             get_components_list.
//
// Double-red requirement:
//   A — corrupt any Assert → test RED
//   B — remove [InitializeOnLoad] registration → registration tests RED,
//       grouper-bypass test RED (chips absorbed by grouper, Count drops below 2)
//       remove "comp-read-props-populated" guard → enrichment-duplication test RED
//       remove "comp-read-result-populated" guard → components-list duplication RED
//
// Data: real hierarchy paths, transient IDs, Cyrillic, long values, partial results.
// No synthetic "a"/"b" strings — each case uses an actual field value from the project.
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ComponentReadCardTests : ToolCardTestBase
    {
        // ── Registration (RED B: fails when InitializeOnLoad registration removed) ─

        [Test]
        public void ComponentReadCard_IsRegisteredForGetComponent() =>
            AssertRegistered("get_component", typeof(ComponentReadCard));

        [Test]
        public void ComponentReadCard_IsRegisteredForInspect() =>
            AssertRegistered("inspect", typeof(ComponentReadCard));

        [Test]
        public void ComponentReadCard_IsRegisteredForGetComponentsList() =>
            AssertRegistered("get_components_list", typeof(ComponentReadCard));

        // ── OnStart ─────────────────────────────────────────────────────────────

        [Test]
        public void OnStart_DoesNotModifyChip() =>
            AssertOnStartIsNoop(new ComponentReadCard(), "get_component");

        // ── get_component: path label + type badge ───────────────────────────────

        [Test]
        public void OnUpdate_GetComponent_RendersPathLabel()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            // Real call: reading Transform on the main camera
            var rec = new ToolCallRecord("get_component", "id-1",
                "{\"path\":\"/Main Camera\",\"type\":\"Transform\"}");
            card.OnUpdate(chip, rec);

            var pathLabel = chip.Q<Label>(className: "comp-read-path");
            Assert.IsNotNull(pathLabel, "Path label must be present after OnUpdate");
            Assert.IsTrue(pathLabel.text.Contains("Main Camera"),
                "Path label must show the object path");
        }

        [Test]
        public void OnUpdate_GetComponent_RendersTypeBadge()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_component", "id-2",
                "{\"path\":\"/Player\",\"type\":\"Rigidbody\"}");
            card.OnUpdate(chip, rec);

            var typeLabel = chip.Q<Label>(className: "comp-read-type");
            Assert.IsNotNull(typeLabel, "Type badge must be present");
            Assert.AreEqual("Rigidbody", typeLabel.text, "Type badge must show component type");
        }

        [Test]
        public void OnUpdate_GetComponent_RenderedMarkerSet()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_component", "id-3",
                "{\"path\":\"/Enemy\",\"type\":\"NavMeshAgent\"}");
            card.OnUpdate(chip, rec);

            Assert.IsTrue(chip.ClassListContains("comp-read-rendered"),
                "Rendered marker must be set after primary content is built");
        }

        // ── get_component: properties from result ────────────────────────────────

        [Test]
        public void OnUpdate_GetComponent_WithResult_ShowsPropertyLabels()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            // Real Transform result from ComponentSerializer.SerializeComponent
            var result = "m_LocalPosition: (0, 0, 0)\nm_LocalRotation: (0, 0, 0, 1)\nm_LocalScale: (1, 1, 1)";
            var rec = new ToolCallRecord("get_component", "id-4",
                "{\"path\":\"/Hero\",\"type\":\"Transform\"}",
                resultText: result, isOk: true);

            card.OnUpdate(chip, rec);

            var propLabels = chip.Query<Label>(className: "comp-read-prop").ToList();
            Assert.Greater(propLabels.Count, 0,
                "Property labels must be rendered when result is present");
            Assert.IsTrue(propLabels.Any(l => l.text.Contains("m_LocalPosition")),
                "Property label must contain actual property key from result");
        }

        [Test]
        public void OnUpdate_GetComponent_NoResult_NoPropertyLabels()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            // Result not yet available — card must still render path + type from args
            var rec = new ToolCallRecord("get_component", "id-5",
                "{\"path\":\"/Player\",\"type\":\"AudioSource\"}",
                resultText: null);

            card.OnUpdate(chip, rec);

            var propLabels = chip.Query<Label>(className: "comp-read-prop").ToList();
            Assert.AreEqual(0, propLabels.Count,
                "No property labels when result not yet available");

            // But the primary content (path + type) must be there
            Assert.IsNotNull(chip.Q<Label>(className: "comp-read-path"),
                "Path label must be present even without result");
        }

        [Test]
        public void OnUpdate_GetComponent_PropertiesNotDuplicatedOnSecondCall()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var result = "mass: 1\ndrag: 0\nangularDrag: 0.05\nuseGravity: True";
            var rec = new ToolCallRecord("get_component", "id-6",
                "{\"path\":\"/Enemy\",\"type\":\"Rigidbody\"}",
                resultText: result, isOk: true);

            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec); // second call must not duplicate properties

            var propLabels = chip.Query<Label>(className: "comp-read-prop").ToList();
            Assert.AreEqual(4, propLabels.Count,
                "Second OnUpdate must not duplicate property labels. " +
                "Bug: 'comp-read-props-populated' guard missing.");
        }

        [Test]
        public void OnUpdate_GetComponent_TwentyFivePropLines_ShowsMoreButton()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            // Simulate a component with 25 properties (e.g. AudioSource has many fields)
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 25; i++)
                sb.AppendLine($"m_Field{i:D2}: {i * 1.5f}");
            var rec = new ToolCallRecord("get_component", "id-7",
                "{\"path\":\"/Player\",\"type\":\"AudioSource\"}",
                resultText: sb.ToString(), isOk: true);

            card.OnUpdate(chip, rec);

            var propLabels = chip.Query<Label>(className: "comp-read-prop").ToList();
            var showMore   = chip.Q<Label>(className: "comp-read-show-more");
            Assert.AreEqual(20, propLabels.Count,
                "Exactly 20 property lines visible before show-more");
            Assert.IsNotNull(showMore,
                "'comp-read-show-more' button must appear when result has >20 properties");
        }

        // ── get_component: Cyrillic in path ──────────────────────────────────────

        [Test]
        public void OnUpdate_GetComponent_CyrillicPath_Rendered()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec = new ToolCallRecord("get_component", "id-8",
                "{\"path\":\"/Игрок/Тело\",\"type\":\"SkinnedMeshRenderer\"}");

            card.OnUpdate(chip, rec);

            var pathLabel = chip.Q<Label>(className: "comp-read-path");
            Assert.IsNotNull(pathLabel, "Path label must render with Cyrillic path");
            Assert.IsTrue(pathLabel.text.Contains("Игрок") || pathLabel.text.Contains("Тело"),
                "Cyrillic characters must appear in the path label");
        }

        // ── get_component: null argsJson — no crash, no render ──────────────────

        [Test]
        public void OnUpdate_NullArgs_NoRender()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_component", "id-null", null);

            card.OnUpdate(chip, rec);

            Assert.AreEqual(0, chip.childCount,
                "Null argsJson must produce no content — card waits for args");
            Assert.IsFalse(chip.ClassListContains("comp-read-rendered"),
                "Rendered marker must NOT be set when argsJson is null");
        }

        // ── Idempotency ───────────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_CalledTwice_PrimaryContentNotDuplicated()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_component", "id-idem",
                "{\"path\":\"/Manager\",\"type\":\"GameManager\"}");

            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec);

            var entries = chip.Query(className: "comp-read-entry").ToList();
            Assert.AreEqual(1, entries.Count,
                "Second OnUpdate must not duplicate the comp-read-entry row (idempotency)");
        }

        // ── inspect: single path ─────────────────────────────────────────────────

        [Test]
        public void OnUpdate_Inspect_SinglePath_RendersPill()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("inspect", "id-insp-1",
                "{\"paths\":\"/Player\"}");

            card.OnUpdate(chip, rec);

            var pathLabels = chip.Query<Label>(className: "comp-read-path").ToList();
            Assert.AreEqual(1, pathLabels.Count, "Single path must produce one path pill");
            Assert.AreEqual("/Player", pathLabels[0].text);
        }

        [Test]
        public void OnUpdate_Inspect_ThreePaths_AllPillsRendered()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("inspect", "id-insp-2",
                "{\"paths\":\"/Player,/Enemy,/NPC_Guard\"}");

            card.OnUpdate(chip, rec);

            var pathLabels = chip.Query<Label>(className: "comp-read-path").ToList();
            Assert.AreEqual(3, pathLabels.Count,
                "Three paths must produce three clickable path pills");
        }

        [Test]
        public void OnUpdate_Inspect_FourPaths_RendersCountNotPills()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            // Four paths → threshold exceeded → show "4 objects" label
            var rec = new ToolCallRecord("inspect", "id-insp-3",
                "{\"paths\":\"/Player,/Enemy,/NPC_Guard,/Boss\"}");

            card.OnUpdate(chip, rec);

            var pathLabels  = chip.Query<Label>(className: "comp-read-path").ToList();
            var countLabels = chip.Query<Label>(className: "comp-read-count").ToList();
            Assert.AreEqual(0, pathLabels.Count,
                "4+ paths must NOT produce individual path pills");
            Assert.AreEqual(1, countLabels.Count,
                "4+ paths must produce one count label");
            Assert.IsTrue(countLabels[0].text.Contains("4"),
                "Count label must mention the number of objects");
        }

        [Test]
        public void OnUpdate_Inspect_WithComponentFilter_ShowsFilterBadge()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("inspect", "id-insp-4",
                "{\"paths\":\"/Player\",\"components\":\"Rigidbody,BoxCollider\"}");

            card.OnUpdate(chip, rec);

            var typeBadge = chip.Q<Label>(className: "comp-read-type");
            Assert.IsNotNull(typeBadge, "Component filter must render as a type badge");
            Assert.IsTrue(typeBadge.text.Contains("Rigidbody"),
                "Type badge must include component names from filter");
        }

        // ── get_components_list ───────────────────────────────────────────────────

        [Test]
        public void OnUpdate_GetComponentsList_RendersObjectId()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_components_list", "id-gcl-1",
                "{\"id\":\"$3E8\"}");

            card.OnUpdate(chip, rec);

            var idLabel = chip.Q<Label>(className: "comp-read-path");
            Assert.IsNotNull(idLabel, "Object ID must be shown as a label");
            Assert.AreEqual("$3E8", idLabel.text);
        }

        [Test]
        public void OnUpdate_GetComponentsList_HexId_IsClickable()
        {
            // $HEX IDs can be resolved via TransientObjectId → make them navigable
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_components_list", "id-gcl-nav",
                "{\"id\":\"$2B678\"}");

            card.OnUpdate(chip, rec);

            var idLabel = chip.Q<Label>(className: "comp-read-path");
            Assert.IsNotNull(idLabel);
            // Navigation is wired via click handler — verify the label exists and has text
            // (we can't simulate a click in headless; verify the label is the nav element)
            Assert.AreEqual("$2B678", idLabel.text,
                "Hex ID label must show the full $HEX value");
        }

        [Test]
        public void OnUpdate_GetComponentsList_DecimalId_RendersPlainLabel()
        {
            // #123 decimal IDs cannot navigate via HierarchyReference → plain label
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_components_list", "id-gcl-dec",
                "{\"id\":\"#123\"}");

            card.OnUpdate(chip, rec);

            var idLabel = chip.Q<Label>(className: "comp-read-path");
            Assert.IsNotNull(idLabel, "Decimal ID must still render as a label");
            Assert.AreEqual("#123", idLabel.text);
        }

        [Test]
        public void OnUpdate_GetComponentsList_EnrichesWithResultOnSecondCall()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();

            // First call: args only, no result
            var recNoResult = new ToolCallRecord("get_components_list", "id-gcl-2",
                "{\"id\":\"$3E8\"}", resultText: null);
            card.OnUpdate(chip, recNoResult);
            Assert.IsNull(chip.Q<Label>(className: "comp-read-result"),
                "No result label before result arrives");

            // Second call: result available — Rigidbody and MeshRenderer on the object
            var recWithResult = new ToolCallRecord("get_components_list", "id-gcl-2",
                "{\"id\":\"$3E8\"}",
                resultText: "Rigidbody\nMeshRenderer\nBoxCollider", isOk: true);
            card.OnUpdate(chip, recWithResult);
            Assert.IsNotNull(chip.Q<Label>(className: "comp-read-result"),
                "Result label must appear when result arrives");
        }

        [Test]
        public void OnUpdate_GetComponentsList_ResultNotDuplicatedOnThirdCall()
        {
            var card = new ComponentReadCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("get_components_list", "id-gcl-3",
                "{\"id\":\"$3E8\"}",
                resultText: "Rigidbody\nMeshRenderer", isOk: true);

            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec); // third call must not add another result label

            var resultLabels = chip.Query<Label>(className: "comp-read-result").ToList();
            Assert.AreEqual(1, resultLabels.Count,
                "Second OnUpdate with result must not duplicate the result label. " +
                "Bug: 'comp-read-result-populated' guard missing.");
        }

        // ── Grouper bypass ────────────────────────────────────────────────────────

        [Test]
        public void TwoGetComponentChips_BypassGrouper() =>
            AssertGrouperBypass("get_component", "gc-a1", "gc-a2");

        [Test]
        public void TwoInspectChips_BypassGrouper() =>
            AssertGrouperBypass("inspect", "insp-b1", "insp-b2");

        [Test]
        public void TwoGetComponentsListChips_BypassGrouper() =>
            AssertGrouperBypass("get_components_list", "gcl-c1", "gcl-c2");
    }
}
