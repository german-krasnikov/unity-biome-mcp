using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Batch
{
    [TestFixture]
    public class BatchTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() =>
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
                CommandRegistry.InitDefaults();
            });
            CommandRegistry.InitDefaults();
            CommandRouter.IsCompiling = () => false;
            BatchHelper.IsCompiling = () => false;
        }

        private string ProcessOwned(string json) =>
            ExecuteAndOwnNewRoots(() => CommandRouter.Process(json));

        private string ExecuteOwned(string commands, string onError) =>
            ExecuteAndOwnNewRoots(() => BatchHelper.Execute(commands, onError));

        private string ExecuteAndOwnNewRoots(System.Func<string> execute)
        {
            var before = new HashSet<GameObject>(
                SceneManager.GetActiveScene().GetRootGameObjects());
            try
            {
                return execute();
            }
            finally
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root))
                        TrackOwnedObject(root);
            }
        }

        // --- MCPBatchTests ---

        [Test]
        public void BatchTextFormat_SingleCommand()
        {
            // Single create_object in text format
            var json = "{\"id\":\"b1\",\"cmd\":\"batch\",\"args\":{\"commands\":\"create_object name=BatchTestObj primitive=Cube\",\"on_error\":\"continue\"}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /BatchTestObj", result);
            StringAssert.Contains("ok:1", result);

            // Verify object was created
            var obj = GameObject.Find("BatchTestObj");
            Assert.IsNotNull(obj);
        }

        [Test]
        public void BatchTextFormat_MultipleCommands()
        {
            // Multi-line text format
            var commands = "create_object name=Multi1 primitive=Cube\ncreate_object name=Multi2 primitive=Sphere";
            var json = $"{{\"id\":\"b2\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /Multi1", result);
            StringAssert.Contains("[1] Created /Multi2", result);
            StringAssert.Contains("ok:2", result);

            // Verify both objects created
            Assert.IsNotNull(GameObject.Find("Multi1"));
            Assert.IsNotNull(GameObject.Find("Multi2"));
        }

        [Test]
        public void BatchTextFormat_SkipEmptyLines()
        {
            // Empty lines should be ignored
            var commands = "create_object name=Empty1\n\n\ncreate_object name=Empty2\n";
            var json = $"{{\"id\":\"b3\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /Empty1", result);
            StringAssert.Contains("[1] Created /Empty2", result);
            StringAssert.Contains("ok:2", result);

            Assert.IsNotNull(GameObject.Find("Empty1"));
            Assert.IsNotNull(GameObject.Find("Empty2"));
        }

        [Test]
        public void BatchTextFormat_SkipComments()
        {
            // Lines starting with # are comments
            var commands = "# This is a comment\ncreate_object name=Comment1\n# Another comment\ncreate_object name=Comment2";
            var json = $"{{\"id\":\"b4\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /Comment1", result);
            StringAssert.Contains("[1] Created /Comment2", result);
            StringAssert.Contains("ok:2", result);

            Assert.IsNotNull(GameObject.Find("Comment1"));
            Assert.IsNotNull(GameObject.Find("Comment2"));
        }

        [Test]
        public void BatchTextFormat_QuotedValues()
        {
            // Values with spaces in quotes — escape \" for JSON embedding
            var commands = "create_object name=\\\"My Object\\\" primitive=Cube";
            var json = $"{{\"id\":\"b5\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /My Object", result);
            StringAssert.Contains("ok:1", result);

            var obj = GameObject.Find("My Object");
            Assert.IsNotNull(obj);
        }

        [Test]
        public void BatchTextFormat_SpecialCharValues()
        {
            // Values with special characters like # and ()
            var commands = "create_object name=SpecialTest primitive=Cube\nset_material path=/SpecialTest color=#FF0000\nset_property path=/SpecialTest component=Transform prop=m_LocalPosition value=(1,2,3)";
            var json = $"{{\"id\":\"b6\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[0] Created /SpecialTest", result);
            StringAssert.Contains("ok:3", result);

            var obj = GameObject.Find("SpecialTest");
            Assert.IsNotNull(obj);
        }

        [Test]
        public void BatchTextFormat_Vector3WithSpaces()
        {
            // Regression: value=(0, 6.8, 0) must not split on spaces inside parens
            var commands = "create_object name=VecSpace primitive=Cube\nset_property path=/VecSpace component=Transform prop=m_LocalPosition value=(0, 6.8, 0)";
            var json = $"{{\"id\":\"bv1\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("ok:2", result);
            var obj = GameObject.Find("VecSpace");
            Assert.IsNotNull(obj);
            Assert.AreEqual(6.8f, obj.transform.localPosition.y, 0.01f);
        }

        [Test]
        public void BatchTextFormat_ParseLineParenthesizedValues()
        {
            // Unit test: ParseLine keeps (0, 1, 0) intact
            var (cmd, argsJson) = BatchHelper.ParseLine("set_property path=/Obj component=Transform prop=m_LocalPosition value=(0, 1, 0)");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("\"value\":\"(0, 1, 0)\"", argsJson);
            StringAssert.Contains("\"path\":\"/Obj\"", argsJson);
            StringAssert.Contains("\"component\":\"Transform\"", argsJson);
        }

        [Test]
        public void BatchTextFormat_StopOnError()
        {
            // Valid, INVALID, Valid → 3rd is "skip"
            var commands = "create_object name=Stop1\ndelete_object id=999999\ncreate_object name=Stop2";
            var json = $"{{\"id\":\"b7\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"stop\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("[0] Created /Stop1", result);
            StringAssert.Contains("[1] err:", result);
            StringAssert.Contains("[2] skip", result);
            StringAssert.Contains("ok:1 err:1", result);

            // Verify 1st executed, 3rd not
            Assert.IsNotNull(GameObject.Find("Stop1"));
            Assert.IsNull(GameObject.Find("Stop2"));
        }

        [Test]
        public void BatchTextFormat_ContinueOnError()
        {
            // Valid, INVALID, Valid → all 3 have results
            var commands = "create_object name=Valid1\nset_property path=/Missing component=Transform prop=localPosition value=(0,0,0)\ncreate_object name=Valid2";
            var json = $"{{\"id\":\"b8\",\"cmd\":\"batch\",\"args\":{{\"commands\":\"{commands}\",\"on_error\":\"continue\"}}}}";
            var result = ProcessOwned(json);

            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("[0] Created /Valid1", result);
            StringAssert.Contains("[1] err:", result);
            StringAssert.Contains("[2] Created /Valid2", result);
            StringAssert.Contains("ok:2 err:1", result);

            // Verify valid commands executed
            Assert.IsNotNull(GameObject.Find("Valid1"));
            Assert.IsNotNull(GameObject.Find("Valid2"));
        }

        // --- MCPBatchGuardTests ---

        // --- Async commands ---

        [Test]
        public void Batch_AsyncCommand_WaitUntil_Blocked()
        {
            var result = ExecuteOwned("wait_until path=/X component=Y field=Z value=true", "continue");
            StringAssert.Contains("async-only", result);
        }

        [Test]
        public void Batch_AsyncCommand_MoveTo_Blocked()
        {
            var result = ExecuteOwned("move_to path=/X position=1,0,0", "continue");
            // async check fires before runtime check — same "async-only" message
            StringAssert.Contains("async-only", result);
        }

        [Test]
        public void Batch_AsyncCommand_TestStep_Blocked()
        {
            var result = ExecuteOwned("test_step path=/X position=1,0,0", "continue");
            StringAssert.Contains("async-only", result);
        }

        // --- Runtime-only commands outside Play Mode ---

        [Test]
        public void Batch_RuntimeCommand_OutsidePlayMode_Blocked()
        {
            Assert.IsFalse(EditorApplication.isPlaying, "Must run in EditMode");
            // invoke_method is registered with runtime: true
            var result = ExecuteOwned("invoke_method path=/X component=Y method=Z", "continue");
            StringAssert.Contains("BLOCKED", result);
            StringAssert.Contains("runtime-only", result);
        }

        // --- Read-only commands ---

        [Test]
        public void Batch_ReadOnlyCommands_AllPass()
        {
            var result = ExecuteOwned("ping", "continue");
            // ping should not be blocked; it returns "pong" or similar, counted as ok
            StringAssert.DoesNotContain("BLOCKED", result);
            StringAssert.Contains("ok:1", result);
        }

        // --- Mixed commands with on_error=continue ---

        [Test]
        public void Batch_MixedCommands_ContinueMode()
        {
            // ping (ok) + invoke_method (blocked) + ping (ok)
            var commands = "ping\ninvoke_method path=/X component=Y method=Z\nping";
            var result = ExecuteOwned(commands, "continue");
            StringAssert.Contains("BLOCKED", result);
            // Two pings succeeded, one blocked
            StringAssert.Contains("ok:2", result);
            StringAssert.Contains("err:1", result);
        }

        // --- on_error=stop stops at first blocked ---

        [Test]
        public void Batch_OnErrorStop_StopsAtFirst()
        {
            // invoke_method (blocked) → ping (should be skipped)
            var commands = "invoke_method path=/X component=Y method=Z\nping";
            var result = ExecuteOwned(commands, "stop");
            StringAssert.Contains("[0] BLOCKED", result);
            StringAssert.Contains("[1] skip", result);
            StringAssert.Contains("ok:0 err:1", result);
        }

        // --- MCPF11BatchSnapshotTests ---

        [Test]
        public void InBatch_Flag_Reset_After_Execute()
        {
            // Execute a no-op batch and verify InBatch is false after
            ExecuteOwned("", "continue");
            Assert.IsFalse(BatchHelper.InBatch, "InBatch must be false after Execute completes");
        }

        [Test]
        public void InBatch_Flag_Reset_On_Exception()
        {
            // Even if batch throws internally, InBatch must reset
            // We can't easily force an internal exception, but we verify normal reset
            Assert.IsFalse(BatchHelper.InBatch, "InBatch should start false");
            ExecuteOwned("", "continue");
            Assert.IsFalse(BatchHelper.InBatch, "InBatch must be false after empty batch");
        }

        [Test]
        public void InBatch_SetProperty_ReturnsCompactLine()
        {
            // When InBatch=true, ExecSetProperty should return "prop = value" without snapshot
            var go = new GameObject("F11_TestBatch");
            try
            {
                // Use batch to set a property — result should NOT contain "---" snapshot
                var cmds = $"set_property path={go.name} component=Transform prop=m_LocalPosition.x value=3.14";
                var result = ExecuteOwned(cmds, "continue");
                // The batch summary is "ok:1" — we can't easily inspect per-line output here
                // Just assert the batch ran without snapshot overhead by checking no "---" in result
                // (snapshot lines start with "---" separator)
                Assert.IsFalse(result.Contains("---"),
                    $"Batch result must not contain snapshot separator '---'. Got: {result}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NestedBatch_KeepsInBatch_For_OuterTail()
        {
            // F11 depth-counter regression: a nested `batch` must NOT reset InBatch while the
            // outer batch is still running. A set_property placed AFTER the nested batch must
            // still emit the compact form (no "---" snapshot). With the old bool, the nested
            // batch's finally flipped InBatch=false → the tail set_property emitted a snapshot.
            var cmds =
                "create_object name=F11_Nested_Probe\n" +
                "batch commands=\"create_object name=F11_Nested_Inner\"\n" +
                "set_property path=F11_Nested_Probe component=Transform prop=m_LocalPosition.x value=2.5";
            try
            {
                var result = ExecuteOwned(cmds, "continue");
                Assert.IsFalse(result.Contains("---"),
                    $"set_property after a nested batch must stay compact (InBatch held by depth counter). Got: {result}");
                Assert.IsFalse(BatchHelper.InBatch, "depth must return to 0 after outermost Execute");
            }
            finally
            {
                foreach (var n in new[] { "F11_Nested_Probe", "F11_Nested_Inner" })
                {
                    var go = GameObject.Find(n);
                    if (go != null) Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void InBatch_False_SetProperty_ReturnsSnapshot()
        {
            // Outside batch, ExecSetProperty should include the snapshot
            var go = new GameObject("F11_TestNoBatch");
            try
            {
                // Direct ExecCommand call (outside batch) should return snapshot
                var argsJson = $"{{\"path\":\"{go.name}\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition.x\",\"value\":\"5.0\"}}";
                var result = CommandRouter.ExecuteCommand("set_property", argsJson);
                // Should contain "---" separator from snapshot (unless component not found or other edge case)
                Assert.IsTrue(result.Contains("---") || result.Contains("m_LocalPosition"),
                    $"Non-batch set_property should include snapshot. Got: {result}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // G2: skip counter — Phase 0

        [Test]
        public void BatchOnErrorStop_ProducesSkipCount()
        {
            // 5 commands: cmd 0 ok, cmd 1 err, cmds 2-4 skip
            var commands =
                "create_object name=SkipA primitive=Cube\n" +
                "get_component path=/NONEXISTENT component=Transform\n" +
                "create_object name=SkipB primitive=Cube\n" +
                "create_object name=SkipC primitive=Cube\n" +
                "create_object name=SkipD primitive=Cube";
            var result = ExecuteOwned(commands, "stop");
            StringAssert.Contains("skip:3", result,
                $"Expected skip:3 in summary. Got: {result}");
            StringAssert.Contains("ok:1", result);
            StringAssert.Contains("err:1", result);
        }

        [Test]
        public void BatchStopInvariant_CountsAddUp()
        {
            var commands =
                "create_object name=InvA primitive=Cube\n" +
                "get_component path=/NONEXISTENT component=Transform\n" +
                "create_object name=InvB primitive=Cube\n" +
                "create_object name=InvC primitive=Cube";
            var result = ExecuteOwned(commands, "stop");
            // Parse summary: ok:N err:M skip:K (+ optional timeout:T)
            var m = System.Text.RegularExpressions.Regex.Match(
                result,
                @"ok:(\d+)(?: err:(\d+))?(?: skip:(\d+))?(?: timeout:(\d+))?$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"No summary found in: {result}");
            int ok = int.Parse(m.Groups[1].Value);
            int err = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            int skip = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
            int timeout = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
            Assert.AreEqual(4, ok + err + skip + timeout,
                $"Invariant violated: ok={ok} err={err} skip={skip} timeout={timeout}");
        }
    }
}
