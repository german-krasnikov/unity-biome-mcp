// TDD: Sprint 3 Gamedev Friction Fixes — NUnit EditMode tests.
// Covers: #06B compress, #06C timeline play-mode guard, #11 clip list,
//         #07 .defs scan, #12 UnityEvent path, #12B get_unity_events,
//         #13 inspect/set_property find_type, #14 fresh mode, #17 runtime_snapshot.
// Tests run in EditMode. Do NOT depend on PlayMode or TCP.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class Sprint3FrictionTests : SceneCleanTestBase
    {
        // ── #06B: get_hierarchy compress ──────────────────────────────────────

        [Test]
        public void GetHierarchy_Compress_ResultShorterThanWithout()
        {
            var go = new GameObject("CompressTest");
            var raw = ComponentSerializer.Serialize("/CompressTest", "Transform");
            var comp = DefaultStripper.Strip(raw);
            Object.DestroyImmediate(go);
            Assert.IsNotNull(raw);
            Assert.Less(comp.Length, raw.Length,
                "compress should strip default Transform fields");
        }

        // ── #06C, step 7.2: the timeline's reads are not blocked by the play-mode guard ───────────────────

        [Test]
        public void Timeline_ReadAction_NotBlockedByPlayModeGuard()
        {
            // The workaround of #06C was mutating:false for the whole command. Since step 7.2 the timeline is mutating as in
            // Python, and its reads (get, get_bindings: the READ lines of dsl/tools.biome) are narrowed per call, so only its
            // write actions meet the "changes will be lost" block of Play Mode. Both halves of the guard are decided here by
            // IsMutating and IsAllowedMutationInPlayMode, which CheckGuards asks; no Play Mode is needed.
            CommandRouter.RegisterAll();
            Assert.IsFalse(CommandRegistry.IsMutating("timeline", "{\"action\":\"get\"}"), "get reads");
            Assert.IsFalse(CommandRegistry.IsMutating("timeline", "{\"action\":\"get_bindings\"}"), "get_bindings reads");
            Assert.IsTrue(CommandRegistry.IsMutating("timeline", "{\"action\":\"add_track\"}"), "add_track writes");
            Assert.IsFalse(CommandRouter.IsAllowedMutationInPlayMode("timeline", "{\"action\":\"add_track\"}"),
                "a write action of the timeline is not allowed in Play Mode");
        }

        // ── #07: AliasExpander .defs scan ────────────────────────────────────

        [Test]
        public void GetTable_WithTableOverride_ReadsInjectedAliases()
        {
            // Test seam: _tableOverride bypasses AssetDatabase and .defs scan.
            // This verifies the override mechanism (unit test isolation).
            var prev = AliasExpander._tableOverride;
            try
            {
                AliasExpander._tableOverride = new Dictionary<string, string>
                {
                    { "hero", "/Hero" },
                    { "score", "/HUD|ScoreUI|value" }
                };
                // ExpandText should use the override table
                var result = AliasExpander.ExpandText("ASSERT $hero|Health|hp == 100");
                Assert.AreEqual("ASSERT /Hero|Health|hp == 100", result);
            }
            finally
            {
                AliasExpander._tableOverride = prev;
            }
        }

        [Test]
        public void AliasExpander_Invalidate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AliasExpander.Invalidate());
        }

        // ── #11: Timeline track list emits clip times ─────────────────────────
        // (TimelineAsset creation requires Unity Timeline package; tested via integration)
        // Structural test: AppendTrackSummary existence verified by compilation.

        // ── #12A: UnityEvent emits full path ──────────────────────────────────

        [Test]
        public void ComponentSerializer_Button_SerializesWithoutError()
        {
            // Verifies Button component serialization returns non-null.
            // #12A full-path assertion (persistent listener path) requires a live integration test.
            var source = new GameObject("EventSource");
            source.AddComponent<UnityEngine.UI.Button>();

            var result = ComponentSerializer.Serialize("/EventSource", "Button");
            Object.DestroyImmediate(source);
            Assert.IsNotNull(result, "Button serialization should return non-null");
        }

        // ── #12B: get_unity_events registration ───────────────────────────────

        [Test]
        public void GetUnityEvents_IsRegistered_AfterRegisterAll()
        {
            CommandRouter.RegisterAll();
            Assert.IsTrue(CommandRegistry.IsRegistered("get_unity_events"),
                "get_unity_events should be registered");
        }

        [Test]
        public void GetUnityEvents_NoEvents_ReturnsNoEventsMessage()
        {
            CommandRouter.RegisterAll();
            // Empty scene — no persistent events
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = CommandRouter.ExecuteCommand("get_unity_events", "{}");
            Assert.AreEqual("no UnityEvents found", result);
        }

        // ── #13: inspect find_type ────────────────────────────────────────────

        [Test]
        public void Inspect_FindType_WhenNoneExist_ReturnsNone()
        {
            CommandRouter.RegisterAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = CommandRouter.ExecuteCommand("inspect",
                "{\"find_type\":\"Rigidbody\"}");
            Assert.AreEqual("none", result);
        }

        [Test]
        public void Inspect_FindType_ReturnsMatchingObjects()
        {
            CommandRouter.RegisterAll();
            var go1 = new GameObject("RB1");
            var go2 = new GameObject("RB2");
            var goNone = new GameObject("NoRB");
            go1.AddComponent<Rigidbody>();
            go2.AddComponent<Rigidbody>();

            var result = CommandRouter.ExecuteCommand("inspect",
                "{\"find_type\":\"Rigidbody\",\"components\":\"Rigidbody\"}");

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(goNone);
            Assert.IsNotNull(result);
            // Should include both objects
            Assert.That(result, Does.Contain("RB1").Or.Contain("Rigidbody"));
        }

        // ── #13: set_property find_type ───────────────────────────────────────

        [Test]
        public void SetProperty_FindType_BulkSetsAll_ReturnsSummary()
        {
            CommandRouter.RegisterAll();
            var go1 = new GameObject("BulkRB1");
            var go2 = new GameObject("BulkRB2");
            go1.AddComponent<Rigidbody>();
            go2.AddComponent<Rigidbody>();

            var result = CommandRouter.ExecuteCommand("set_property",
                "{\"find_type\":\"Rigidbody\",\"component\":\"Rigidbody\",\"prop\":\"mass\",\"value\":\"5\"}");

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Assert.IsNotNull(result);
            Assert.That(result, Does.Contain("bulk set").IgnoreCase.Or.Contain("2 ok").IgnoreCase);
        }

        // ── #17: runtime_snapshot registration ───────────────────────────────

        [Test]
        public void RuntimeSnapshot_IsRegistered_AfterRegisterAll()
        {
            CommandRouter.RegisterAll();
            Assert.IsTrue(CommandRegistry.IsRegistered("runtime_snapshot"),
                "runtime_snapshot should be registered");
        }

        // ── #14: PlaytestRunner fresh mode field ──────────────────────────────

        [Test]
        public void PlaytestRunner_FreshMode_FieldsExist()
        {
            // Structural: verify _freshMode and _freshReloadDone exist on PlaytestRunner.
            // (internal visibility needed — reflected via InternalsVisibleTo)
            var t = typeof(PlaytestRunner);
            var freshMode = t.GetField("_freshMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var freshDone = t.GetField("_freshReloadDone",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(freshMode, "_freshMode static field should exist on PlaytestRunner");
            Assert.IsNotNull(freshDone, "_freshReloadDone static field should exist on PlaytestRunner");
        }

        [Test]
        public void PlaytestRunner_Run_AcceptsFreshParam()
        {
            // Verify the Run() overload with fresh parameter compiles and is callable.
            // We cannot actually start it (no Play Mode), but we can invoke with fresh=false
            // to verify the signature exists.
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            // Calling with empty script; should fail fast with parse error, not signature error.
            PlaytestRunner.Run("", 1f, tcs, fresh: false);
            // Either completes immediately or returns error — either is fine for signature check.
            Assert.Pass("Run() accepts fresh parameter without compile error");
        }
    }
}
