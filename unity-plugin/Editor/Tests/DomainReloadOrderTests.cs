// TDD: Domain reload ordering invariants.
// Pins correctness regardless of [InitializeOnLoadMethod] execution order:
//   TestRunner.ResetOnReload() must NOT clear CommandRegistry.
// All tests are EditMode-only, synchronous, no scene creation.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class DomainReloadOrderTests
    {
        private static readonly FieldInfo IsRunningField =
            typeof(TestRunner).GetField("_isRunning",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo ResetOnReloadMethod =
            typeof(TestRunner).GetMethod("ResetOnReload",
                BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            IsRunningField?.SetValue(null, 0);
            SessionState.SetBool(TestRunner.KeyPending, false);
            SessionState.SetString(TestRunner.KeyResults, "");
        }

        [TearDown]
        public void TearDown() => SetUp(); // symmetric — restore full registration

        // T1: get_test_results must be readable during compile (P1 guarantee).
        [Test]
        public void GetTestResults_IsAllowedDuringCompile()
        {
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_test_results"),
                "get_test_results must be allowedDuringCompile (P1: SessionState-only read)");
        }

        // T2: belt-and-suspenders — IsRegistered for the command that reads run_tests output.
        [Test]
        public void GetTestResults_IsRegistered_AfterInitDefaults()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("get_test_results"),
                "get_test_results must be registered after InitDefaults()");
        }

        // T3: core ordering concern — ResetOnReload must NOT clear CommandRegistry.
        [Test]
        public void ResetOnReload_DoesNotClearCommandRegistry()
        {
            ResetOnReloadMethod?.Invoke(null, null);

            Assert.IsTrue(CommandRegistry.IsRegistered("get_test_results"),
                "get_test_results must remain registered after ResetOnReload()");
            Assert.IsTrue(CommandRegistry.IsRegistered("run_tests"),
                "run_tests must remain registered after ResetOnReload()");
            Assert.IsTrue(CommandRegistry.Ready,
                "CommandRegistry.Ready must remain true after ResetOnReload()");
        }

        // T4: a pending test run survives domain reload — KeyPending not cleared by ResetOnReload.
        [Test]
        public void KeyPending_SurvivesResetOnReload_WhenTrue()
        {
            SessionState.SetBool(TestRunner.KeyPending, true);

            ResetOnReloadMethod?.Invoke(null, null);

            Assert.IsTrue(SessionState.GetBool(TestRunner.KeyPending, false),
                "KeyPending must survive ResetOnReload() — test was still in progress");
            Assert.IsTrue(TestRunner.IsRunning,
                "IsRunning must be true when KeyPending survives reload");
        }

        // T5: explicit _isRunning = 0 reset — pins the field assignment in ResetOnReload.
        [Test]
        public void ResetOnReload_Sets_IsRunning_ToZero()
        {
            IsRunningField?.SetValue(null, 1); // simulate active test

            ResetOnReloadMethod?.Invoke(null, null);

            Assert.AreEqual(0, (int)IsRunningField.GetValue(null),
                "_isRunning must be explicitly set to 0 by ResetOnReload()");
        }

        // T6: both [InitializeOnLoadMethod] orders must leave get_test_results usable.
        [Test]
        public void ReloadSequence_BothOrders_GetTestResults_Available()
        {
            // Order A: InitDefaults → ResetOnReload (safe order)
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            ResetOnReloadMethod?.Invoke(null, null);
            Assert.IsTrue(CommandRegistry.IsRegistered("get_test_results"),
                "Order A: get_test_results must be registered");
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_test_results"),
                "Order A: get_test_results must be allowedDuringCompile");

            // Order B: ResetOnReload → InitDefaults (worst-case Unity ordering)
            CommandRegistry.Clear();
            ResetOnReloadMethod?.Invoke(null, null);
            CommandRegistry.InitDefaults();
            Assert.IsTrue(CommandRegistry.IsRegistered("get_test_results"),
                "Order B: get_test_results must be registered");
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_test_results"),
                "Order B: get_test_results must be allowedDuringCompile");
        }

        // T7: completeness gate — all 4 test-runner commands survive a reload sequence.
        [Test]
        public void AllCriticalTestCommands_RegisteredAndAllowedAfterReload()
        {
            var commands = new[]
            {
                (cmd: "run_tests",         mustBeAllowedDuringCompile: false),
                (cmd: "get_test_results",  mustBeAllowedDuringCompile: true),
                (cmd: "get_test_progress", mustBeAllowedDuringCompile: true),
                (cmd: "get_test_count",    mustBeAllowedDuringCompile: true),
            };

            ResetOnReloadMethod?.Invoke(null, null);

            foreach (var (cmd, mustAllow) in commands)
            {
                Assert.IsTrue(CommandRegistry.IsRegistered(cmd),
                    $"{cmd} must be registered after reload sequence");

                if (mustAllow)
                    Assert.IsTrue(CommandRouter.IsAllowedDuringCompile(cmd),
                        $"{cmd} must be allowedDuringCompile");
                else
                    Assert.IsFalse(CommandRouter.IsAllowedDuringCompile(cmd),
                        $"run_tests must NOT be allowedDuringCompile — blocked during compile intentionally");
            }
        }
    }
}
