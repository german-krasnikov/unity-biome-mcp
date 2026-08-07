// TDD: BatchHelper ReadOnly guard tests.
// Verifies mutating batch sub-commands are blocked when IsReadOnly=true.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchHelperReadOnlyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Func<bool> _savedIsReadOnly;

        [SetUp]
        public void SetUp()
        {
            _savedIsReadOnly = CommandRouter.IsReadOnly;
            CommandRegistry.Register("test_ro_mutating", _ => "ok",
                mutating: true, required: "", alwaysAllowed: true);
            CommandRegistry.Register("test_ro_read", _ => "ok",
                required: "", alwaysAllowed: true);
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsReadOnly = _savedIsReadOnly;
        }

        [Test]
        public void Batch_ReadOnly_BlocksMutating_ContainsReadOnlyBlocked()
        {
            CommandRouter.IsReadOnly = () => true;
            var result = BatchHelper.Execute("test_ro_mutating", "continue");
            StringAssert.Contains("READ_ONLY_BLOCKED", result);
        }

        [Test]
        public void Batch_ReadOnly_AllReadCommands_AllPass()
        {
            CommandRouter.IsReadOnly = () => true;
            var result = BatchHelper.Execute("test_ro_read\ntest_ro_read", "continue");
            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", result);
            Assert.IsFalse(BatchHelper.HasErrors(result));
        }

        [Test]
        public void Batch_NotReadOnly_AllowsMutating_NoReadOnlyError()
        {
            CommandRouter.IsReadOnly = () => false;
            var result = BatchHelper.Execute("test_ro_mutating", "continue");
            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", result);
        }

        [Test]
        public void Batch_ReadOnly_MixedCommands_ReadPassesMutatingBlocked()
        {
            CommandRouter.IsReadOnly = () => true;
            var result = BatchHelper.Execute("test_ro_read\ntest_ro_mutating", "continue");
            StringAssert.Contains("READ_ONLY_BLOCKED", result);
            // Read command at [0] returned "ok" — no line output for it
            StringAssert.DoesNotContain("[0]", result);
        }
    }
}
