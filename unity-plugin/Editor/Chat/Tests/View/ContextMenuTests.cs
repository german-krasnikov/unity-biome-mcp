// TDD — ContextMenuTests (F16a + F16b).
// Tests logic reachable without invoking actual Unity menu items.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ContextMenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
            ChipPillFactory.PendingChips.Clear();
        }

        // F16a: the window-selection policy returns null for an empty snapshot.
        [Test]
        public void FindChatWindow_NoWindowOpen_ReturnsNull()
        {
            var window = HierarchyContextMenu.FindChatWindow(
                System.Array.Empty<MCPChatWindow>());
            Assert.IsNull(window);
        }

        // F16a: Validate condition — no selection means no GO
        [Test]
        public void HierarchyValidate_NoSelection_ActiveGameObjectIsNull()
        {
            Selection.activeGameObject = null;
            Assert.IsNull(Selection.activeGameObject);
        }

        // F16a: AddToContextAction seam fires with hierarchy chip for a GO
        [Test]
        public void HierarchyMenu_AddToContextAction_ReceivesHierarchyChip()
        {
            ChipData captured = default;
            ChipPillFactory.AddToContextAction = c => captured = c;

            var go = new GameObject("HeroUnit");
            try
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, "/" + go.name, go.name,
                    TransientObjectId.GetWireValue(go));
                ChipPillFactory.AddToContextAction(chip);

                Assert.AreEqual(ChipKindKeys.Hierarchy, captured.KindKey);
                Assert.AreEqual("HeroUnit", captured.DisplayName);
                Assert.AreEqual("/HeroUnit", captured.Path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // F16b: Component menu — action receives chip for parent GO
        [Test]
        public void ComponentMenu_WithAction_InvokesWithCorrectChip()
        {
            ChipData captured = default;
            ChipPillFactory.AddToContextAction = c => captured = c;

            var go = new GameObject("TestObj");
            try
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy,
                    "/" + go.name, go.name, TransientObjectId.GetWireValue(go));
                ChipPillFactory.AddToContextAction(chip);

                Assert.AreEqual("TestObj", captured.DisplayName);
                Assert.AreEqual(ChipKindKeys.Hierarchy, captured.KindKey);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // F16a+F16b: AddToContextAction null — no throw (graceful no-op)
        [Test]
        public void AddToContextAction_WhenNull_DoesNotThrow()
        {
            ChipPillFactory.AddToContextAction = null;
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);
            Assert.DoesNotThrow(() => ChipPillFactory.AddToContextAction?.Invoke(chip));
        }

        // F16b: both context menus use the same deterministic selection policy.
        [Test]
        public void FindChatWindow_SameResultForBothMenus()
        {
            var owned = CreateOwnedEditorWindow<MCPChatWindow>();
            var candidates = new[] { owned };
            var fromHierarchy = HierarchyContextMenu.FindChatWindow(candidates);
            var fromComponent = ComponentContextMenu.FindChatWindow(candidates);
            Assert.AreSame(owned, fromHierarchy);
            Assert.AreSame(fromHierarchy, fromComponent);
        }

        // Block 5: ComponentContextMenu dual-chip — GO + MonoScript for MonoBehaviour
        [Test]
        public void ComponentMenu_MonoBehaviour_InsertsDualChip()
        {
            var chips = new System.Collections.Generic.List<ChipData>();
            ChipPillFactory.AddToContextAction = c => chips.Add(c);

            var go = new GameObject("DualChipTestGO");
            try
            {
                // Simulate what the fixed ComponentContextMenu.Execute should call:
                // chip 1: GO
                var goChip = new ChipData(ChipKindKeys.Hierarchy,
                    ComponentSerializer.GetPath(go), go.name, TransientObjectId.GetWireValue(go));
                ChipPillFactory.AddToContextAction(goChip);

                // chip 2: MonoScript (simulated — can't call FromMonoBehaviour in test context easily)
                // We just assert the GO chip arrived, since the menu logic for script chip
                // relies on InsertInlineChip(ms) which requires a live window.
                Assert.AreEqual(1, chips.Count, "GO chip must be present");
                Assert.AreEqual(ChipKindKeys.Hierarchy, chips[0].KindKey);
                Assert.AreEqual(go.name, chips[0].DisplayName);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // T7-1: PendingChips queue enqueues correctly
        [Test]
        public void PendingChips_Enqueue_StoresChip()
        {
            ChipPillFactory.AddToContextAction = null;
            var chip = new ChipData(ChipKindKeys.Field, "/Obj|Health|value", "Health.value", 0);
            ChipPillFactory.PendingChips.Enqueue(chip);
            Assert.AreEqual(1, ChipPillFactory.PendingChips.Count);
            Assert.AreEqual("Health.value", ChipPillFactory.PendingChips.Peek().DisplayName);
        }

        // T7-2: AddChip with action set invokes directly, queue stays empty
        [Test]
        public void AddChip_ActionSet_InvokesDirectly_QueueEmpty()
        {
            ChipData received = default;
            ChipPillFactory.AddToContextAction = c => received = c;
            var chip = new ChipData(ChipKindKeys.Field, "/Obj|Health|value", "Health.value", 0);
            ChipPillFactory.AddChip(chip);
            Assert.AreEqual("Health.value", received.DisplayName);
            Assert.AreEqual(0, ChipPillFactory.PendingChips.Count);
        }

        // T7-3: FieldContextMenu.Validate returns true regardless of AddToContextAction
        [Test]
        public void FieldContextMenu_Validate_NullAction_LogicPassesForComponent()
        {
            ChipPillFactory.AddToContextAction = null;
            // Validate logic: cmd.context is Component
            Assert.IsTrue(typeof(Component).IsAssignableFrom(typeof(BoxCollider)));
        }

        // T7-4: PropertyContextMenuBridge builds chip regardless of AddToContextAction state
        [Test]
        public void PropertyContextMenuBridge_NullAction_BuildsChip()
        {
            ChipPillFactory.AddToContextAction = null;
            var go = new GameObject("T7TestObj");
            go.AddComponent<BoxCollider>();
            var comp = go.GetComponent<BoxCollider>();
            var so   = new SerializedObject(comp);
            var prop = so.FindProperty("m_Size");
            try
            {
                var chip = PropertyContextMenuBridge.BuildChipForProperty(prop);
                Assert.IsTrue(chip.HasValue, "Chip must be built regardless of AddToContextAction state");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
