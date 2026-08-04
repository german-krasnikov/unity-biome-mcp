using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Command
{
    public class CommandRouterWorkflowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(CommandRegistry.InitDefaults);
            CommandRouter.RegisterAll();
            _go = TrackOwnedObject(new GameObject("QFTestObj"));
            _go.AddComponent<Rigidbody>();
        }

        // --- Feature 1: Dry-Run ---

        [Test]
        public void SetProperty_DryRun_DoesNotChange()
        {
            var rb = _go.GetComponent<Rigidbody>();
            rb.mass = 1f;

            var json = $"{{\"id\":\"dr1\",\"cmd\":\"set_property\",\"args\":{{\"path\":\"QFTestObj\",\"component\":\"Rigidbody\",\"prop\":\"mass\",\"value\":\"99\",\"dry_run\":\"true\"}}}}";
            var result = CommandRouter.Process(json);

            StringAssert.Contains("\"ok\":true", result);
            Assert.That(rb.mass, Is.EqualTo(1f).Within(0.001f), "dry_run must not change the value");
        }

        [Test]
        public void SetProperty_DryRun_ShowsPreview()
        {
            var json = $"{{\"id\":\"dr2\",\"cmd\":\"set_property\",\"args\":{{\"path\":\"QFTestObj\",\"component\":\"Rigidbody\",\"prop\":\"mass\",\"value\":\"42\",\"dry_run\":\"true\"}}}}";
            var result = CommandRouter.Process(json);

            StringAssert.Contains("DRY-RUN", result);
        }

        // --- Feature 2: Next-tool suggestion ---

        [Test]
        public void SuggestNext_SetProperty_SuggestsConsole()
        {
            // Normal set_property (not dry_run) should contain next suggestion
            var json = $"{{\"id\":\"sn1\",\"cmd\":\"set_property\",\"args\":{{\"path\":\"QFTestObj\",\"component\":\"Rigidbody\",\"prop\":\"mass\",\"value\":\"2\"}}}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("[next:", result);
            StringAssert.Contains("get_console", result);
        }

        [Test]
        public void SuggestNext_ReadCmd_ReturnsNull()
        {
            // Read commands (ping) must NOT contain [next:] hint
            var json = "{\"id\":\"sn2\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.DoesNotContain("[next:", result);
        }

        // --- Feature 3: Smart checkpoint naming ---

        [Test]
        public void Checkpoint_AutoName_IncludesRecentCmds()
        {
            // Run a few commands to populate recent history
            CommandRouter.Process("{\"id\":\"cp1\",\"cmd\":\"ping\",\"args\":{}}");
            CommandRouter.Process($"{{\"id\":\"cp2\",\"cmd\":\"set_property\",\"args\":{{\"path\":\"QFTestObj\",\"component\":\"Rigidbody\",\"prop\":\"mass\",\"value\":\"3\"}}}}");

            // Checkpoint without label → auto-generate
            var json = "{\"id\":\"cp3\",\"cmd\":\"checkpoint\",\"args\":{}}";
            var result = CommandRouter.Process(json);

            StringAssert.Contains("\"ok\":true", result);
            // Should contain "before_" prefix from auto-gen
            StringAssert.Contains("before_", result);
        }

        // --- Phase 3: Mutation Tracking + Undo Fixes ---

        // MCP-011/050/055/071/080: get_changes must record inline MCP mutations.
        // Before fix: ChangeWatcher only subscribed to deferred events (hierarchyChanged etc.)
        // which don't fire synchronously during Process(). After fix: RecordMutation called inline.
        [Test]
        public void GetChanges_AfterCreateObject_RecordsMutation()
        {
            ChangeWatcher.GetChanges(clear: true); // flush any prior changes
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"t1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"ChangeTrackTest\"}}");
                var changes = ChangeWatcher.GetChanges(clear: true);
                StringAssert.Contains("MCP_CREATE_OBJECT", changes);
            }
            finally
            {
                CommandRouter.Process(
                    "{\"id\":\"t2\",\"cmd\":\"delete_object\",\"args\":{\"path\":\"/ChangeTrackTest\",\"force\":\"true\"}}");
            }
        }

        // MCP-012: undo_last must revert MCP mutations.
        // Before fix: SetCommandFallback never called IncrementCurrentGroup, so UndoGroupStack was
        // never populated → RevertLast returned "nothing to undo".
        [Test]
        public void UndoLast_AfterSetProperty_Reverts()
        {
            UndoGroupStack.Clear(); // ensure clean stack
            var go = new GameObject("UndoTest");
            go.AddComponent<BoxCollider>();
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"t1\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/UndoTest\",\"component\":\"BoxCollider\",\"prop\":\"m_Size\",\"value\":\"(5,5,5)\"}}");
                var undoResult = CommandRouter.Process(
                    "{\"id\":\"t2\",\"cmd\":\"undo_last\",\"args\":{\"turns\":\"1\"}}");
                Assert.IsFalse(undoResult.Contains("nothing to undo"), $"undo_last failed: {undoResult}");
                var bc = go.GetComponent<BoxCollider>();
                Assert.That(bc.size.x, Is.EqualTo(1f).Within(0.01f), "BoxCollider size was not reverted by undo");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
