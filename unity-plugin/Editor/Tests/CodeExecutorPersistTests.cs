using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    /// <summary>TDD tests for Phase C persist_as feature (Subtasks 10-15).</summary>
    [TestFixture]
    public class CodeExecutorPersistTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            HeldTypeStore.Clear();
            ProtectEditorPrefInt("UnityMCP_SecurityLevel");
            MCPSettings.SetSecurityLevel(SecurityLevel.Standard);
        }

        [TearDown]
        public void TearDown() => HeldTypeStore.Clear();

        // ── Subtask 10: ProbeCreateFromStream ────────────────────────────────

        [Test]
        public void ProbeCreateFromStream_ReturnsConsistentResult()
        {
            var first = CodeExecutor.ProbeCreateFromStream();
            var second = CodeExecutor.ProbeCreateFromStream();
            Assert.AreEqual(first, second, "Probe must be cached — two calls must return same result");
            Debug.Log($"[Spike] CreateFromStream available: {first} (Path {(first ? 'A' : 'B')})");
        }

        // ── Subtask 11: CompileToBytes ────────────────────────────────────────

        [Test]
        public void CompileToBytes_ReturnsBytesAndAssembly()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("return 42;");
            var (asm, bytes) = CodeExecutor.CompileToBytes(wrapped);
            Assert.IsNotNull(asm, "Assembly must not be null");
            Assert.Greater(bytes.Length, 0, "Bytes must be non-empty");
        }

        // ── Subtask 14: Execute() persist_as param ────────────────────────────

        [Test]
        public void Execute_PersistAs_NoRunMethod_ReturnsPersisted()
        {
            // Full class with no Run() — should not throw, should return persisted: message
            var code = "public static class PersistTestNoRun { public static int Value = 99; }";
            var result = CodeExecutor.Execute(code, "test", "PersistTestNoRun");
            StringAssert.StartsWith("persisted:PersistTestNoRun", result);
            Assert.AreEqual(1, HeldTypeStore.Count);
        }

        [Test]
        public void Execute_PersistAs_TypeVisibleInNextCompile()
        {
            // 1. Persist a helper class
            var defineCode = "public static class PersistGreeter { public static string Hi() => \"hello\"; }";
            var r1 = CodeExecutor.Execute(defineCode, "test", "PersistGreeter");
            StringAssert.StartsWith("persisted:PersistGreeter", r1);

            // 2. Use the persisted type in the next compile (no persist_as)
            var useCode = "return PersistGreeter.Hi();";
            var r2 = CodeExecutor.Execute(useCode, "test");
            Assert.AreEqual("hello", r2);
        }

        [Test]
        public void Execute_NoPersistAs_UnchangedBehavior()
        {
            // Regression guard: bare statement without persist_as works as before
            var result = CodeExecutor.Execute("return (6 * 7).ToString();", "test");
            Assert.AreEqual("42", result);
        }

        // ── Subtask 15: CommandRegistry registrations ─────────────────────────

        [Test]
        public void CommandRegistry_ExecuteCode_HasPersistAsOptional()
        {
            CommandRegistry.InitDefaults();
            CommandRegistry.TryGetContract("execute_code", out _, out var optional, out _);
            CollectionAssert.Contains(optional, "persist_as");
        }

        [Test]
        public void CommandRegistry_ClearHeldTypes_IsRegistered()
        {
            CommandRegistry.InitDefaults();
            Assert.IsTrue(CommandRegistry.IsRegistered("clear_held_types"));
        }
    }
}
