// TDD: CommandRegistry/CommandValidator contract coverage (Issue 23 review action item #1).
// CommandRegistry/CommandValidator are static — tests below reuse REAL commands already
// registered by RegisterAll() (ping, get_component, ask_user, the 6 async commands)
// instead of registering throwaway commands, to avoid static-state leak between tests.
// Exception: tests 9/10 (attribute-based) DO register a temp command, because
// AttributeScannerTests.ScanAndRegister_SkipsNonUserAssemblies proves ScanAndRegister()
// always returns 0 from inside this assembly (IsUserAssembly excludes "Unity*" names) —
// so the only way to test the attribute-forwarding contract is to replicate
// AttributeScanner's exact forwarding line (`attr.Required ?? ""`) directly.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class CommandRegistryContractTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Explicit Clear() before InitDefaults() — do not rely on RegisterAll()'s internal
        // Clear() as the sole cleanup mechanism. Tests 9/10 below register throwaway
        // "contract_test_*" commands directly via CommandRegistry.Register; without an
        // explicit Clear() here, a future refactor of RegisterAll() that drops its internal
        // Clear() would silently leak those commands across the whole test session (Issue 23
        // review C9).
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(CommandRegistry.InitDefaults);
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        // ── Validate: structured contract ─────────────────────────────────────

        [Test]
        public void Register_WithContract_ValidatesRequired()
        {
            // get_component: required "path,type" — omit "type"
            var err = CommandValidator.Validate("get_component", "{\"path\":\"/A\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("!type", err);
        }

        [Test]
        public void Register_WithContract_ValidatesUnknown()
        {
            // "typ" is 1 edit from "type" — should suggest
            var err = CommandValidator.Validate("get_component",
                "{\"path\":\"/A\",\"type\":\"Transform\",\"typ\":\"x\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("?typ→type", err);
        }

        [Test]
        public void Register_EmptyContract_RejectsUnknown()
        {
            // ping: required="" optional="" — explicit EMPTY contract, not free-form.
            var err = CommandValidator.Validate("ping", "{\"bogus\":\"1\"}");
            Assert.IsNotNull(err, "empty contract must still reject unknown params (no escape hatch)");
            StringAssert.Contains("?bogus", err);
        }

        [Test]
        public void AutoUsage_Format()
        {
            var usage = CommandValidator.AutoUsage("get_component", new[] { "path", "type" }, new[] { "scene" });
            Assert.AreEqual("get_component path=... type=... [scene=...]", usage);
        }

        // Issue 23 review M5: high-arity commands (e.g. shader has 18 optional params) must
        // not blow up AutoUsage token cost — cap displayed optionals at 5, summarize the rest.
        [Test]
        public void AutoUsage_CapsOptionalParamsAtFive()
        {
            var many = new[] { "a", "b", "c", "d", "e", "f", "g" };
            var usage = CommandValidator.AutoUsage("cmd", System.Array.Empty<string>(), many);
            Assert.AreEqual("cmd [a=...] [b=...] [c=...] [d=...] [e=...] [+2 more]", usage);
        }

        // ── IsBatchable: structural check ──────────────────────────────────────

        [Test]
        public void IsBatchable_SyncCommand_True()
            => Assert.IsTrue(CommandRegistry.IsBatchable("ping"));

        [Test]
        public void IsBatchable_AsyncCommand_False()
            => Assert.IsFalse(CommandRegistry.IsBatchable("ask_user"));

        [TestCase("run_tests")]
        [TestCase("ask_user")]
        [TestCase("wait_until")]
        [TestCase("move_to")]
        [TestCase("test_step")]
        [TestCase("run_playtest")]
        public void IsBatchable_AllAsyncCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRegistry.IsBatchable(cmd),
                $"{cmd} is registered via RegisterAsync — must not be batchable");

        // Bug fix (action item #2): screenshot has AsyncHandler==null (it's a throwing sync
        // stub — real logic is intercepted in CommandRouter.Process before reaching the
        // registry), so the old AsyncHandler-only check incorrectly saw it as batchable.
        [Test]
        public void Screenshot_NotBatchable_ViaSpecialDispatch()
            => Assert.IsFalse(CommandRegistry.IsBatchable("screenshot"),
                "screenshot must be excluded from batch via SpecialDispatch");

        // ── Plugin-style registration forwards a contract (no bypass) ───────

        [Test]
        public void Plugin_RequiredOptional_Validated()
        {
            CommandRegistry.Register("contract_test_required", _ => "ok", false, false, "x", "y");

            var err = CommandValidator.Validate("contract_test_required", "{}");

            Assert.IsNotNull(err);
            StringAssert.Contains("!x", err);
        }

        [Test]
        public void Plugin_EmptyContract_StillValidates()
        {
            CommandRegistry.Register("contract_test_no_bypass", _ => "ok", false, false, "", "");

            var err = CommandValidator.Validate("contract_test_no_bypass", "{\"garbage\":\"1\"}");

            Assert.IsNotNull(err, "command with empty Required/Optional must still validate");
            StringAssert.Contains("?garbage", err);
        }

        // ── BatchHelper integration ─────────────────────────────────────────────

        [Test]
        public void Batch_AsyncCommand_UniformError()
        {
            var r1 = BatchHelper.Execute("ask_user", "continue");
            var r2 = BatchHelper.Execute("wait_until", "continue");
            StringAssert.Contains("async-only", r1);
            StringAssert.Contains("async-only", r2);
        }

        [Test]
        public void Batch_MissingRequired_ShowsAutoUsage()
        {
            var result = BatchHelper.Execute("get_component path=/A", "continue");
            StringAssert.Contains("!type", result);
            StringAssert.Contains("get_component path=... type=...", result);
        }

        [Test]
        public void Batch_UnknownParam_ShowsFuzzyMatch()
        {
            var result = BatchHelper.Execute("get_component path=/A type=Transform typ=x", "continue");
            StringAssert.Contains("?typ→type", result);
        }
    }
}
