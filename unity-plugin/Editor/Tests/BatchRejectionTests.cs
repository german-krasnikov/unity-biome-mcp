// NUnit tests for BatchHelper rejection paths — async dispatch and runtime-only guards.
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchRejectionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Clear();
            // Restore Ready=true so existing tests exercise the normal operating path.
            // The Ready=false scenario is covered by Execute_RejectsCommandsWhenRegistryNotReady.
            CommandRegistry.Ready = true;
        }

        [TearDown]
        public void TearDown()
        {
            // Ensure Ready is restored even if a test sets it to false.
            CommandRegistry.Ready = true;
        }

        [Test]
        public void BatchRejection_AsyncCmd_NotBatchable()
        {
            CommandRegistry.RegisterAsync("test_async_cmd",
                (id, args, tcs) => { }, required: "");

            var result = BatchHelper.Execute("test_async_cmd", "continue");

            StringAssert.Contains("async-only", result);
        }

        [Test]
        public void BatchRejection_SpecialDispatch_NotBatchable()
        {
            CommandRegistry.Register("test_special_cmd", _ => "ok", specialDispatch: true);

            var result = BatchHelper.Execute("test_special_cmd", "continue");

            StringAssert.Contains("async-only", result);
        }

        [Test]
        public void BatchRejection_RuntimeCmd_OutsidePlayMode()
        {
            // EditMode test runner guarantees EditorApplication.isPlaying == false
            CommandRegistry.Register("test_runtime_cmd", _ => "ok", runtime: true, required: "");

            var result = BatchHelper.Execute("test_runtime_cmd", "continue");

            StringAssert.Contains("runtime-only, skipped outside Play Mode", result);
        }

        [Test]
        public void BatchRejection_RuntimeCmd_InBatch_AtomicRollback()
        {
            // First command succeeds; second is runtime-only and triggers atomic rollback.
            CommandRegistry.Register("test_batchable_cmd", _ => "ok", alwaysAllowed: true, required: "");
            CommandRegistry.Register("test_runtime_cmd2", _ => "ok", runtime: true, required: "");

            var result = BatchHelper.Execute("test_batchable_cmd\ntest_runtime_cmd2", "stop", atomic: true);

            StringAssert.Contains("ATOMIC_ROLLBACK", result);
        }

        [Test]
        public void BatchRejection_UnknownCmd_NotBlockedByBatchableCheck()
        {
            // Unregistered commands pass IsBatchable and fall through to Validate().
            // (SetUp ensures Ready=true so the Ready guard is not the blocker here.)
            var result = BatchHelper.Execute("totally_unknown_cmd", "continue");

            StringAssert.Contains("Unknown command", result);
        }

        [Test]
        public void Execute_RejectsCommandsWhenRegistryNotReady()
        {
            CommandRegistry.Register("test_ready_cmd", _ => "ok", required: "");
            CommandRegistry.Ready = false; // simulate mid-reload window

            var result = BatchHelper.Execute("test_ready_cmd", "continue");

            StringAssert.Contains("registry not ready", result);
        }
    }
}
