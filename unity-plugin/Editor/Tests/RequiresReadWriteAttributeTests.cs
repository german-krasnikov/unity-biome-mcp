// TDD — RequiresReadWriteAttribute classification and enforcement logic.
// EnforceReadWriteRequirement returns a reason string (non-null = skip) instead
// of throwing, so tests can assert on the result without IgnoreException issues.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RequiresReadWriteAttributeTests : UnityMcpTestBase
    {
        // ── Helper types for attribute discovery ──────────────────────────────

        [RequiresReadWrite("class level reason")]
        private sealed class DecoratedClass { }

        private sealed class UndecoratedClass
        {
            [RequiresReadWrite("method level reason")]
            public void AnnotatedMethod() { }

            public void PlainMethod() { }
        }

        // ── Attribute structural tests ────────────────────────────────────────

        [Test]
        public void RequiresReadWriteAttribute_ValidReason_StoresReason()
        {
            var attr = new RequiresReadWriteAttribute("my reason");
            Assert.AreEqual("my reason", attr.Reason);
        }

        [Test]
        public void RequiresReadWriteAttribute_EmptyReason_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new RequiresReadWriteAttribute(""));
        }

        [Test]
        public void RequiresReadWriteAttribute_NullReason_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new RequiresReadWriteAttribute(null));
        }

        // ── EnforceReadWriteRequirement boundary tests ────────────────────────

        [Test]
        public void RequiresReadWrite_ClassLevel_SkipsOnReadOnly()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(DecoratedClass), null, () => true);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void RequiresReadWrite_MethodLevel_SkipsOnReadOnly()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(UndecoratedClass),
                nameof(UndecoratedClass.AnnotatedMethod),
                () => true);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void RequiresReadWrite_ReadWriteWorker_Proceeds()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(DecoratedClass), null, () => false);
            Assert.IsNull(reason);
        }

        [Test]
        public void RequiresReadWrite_NoAttribute_Proceeds()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(UndecoratedClass),
                nameof(UndecoratedClass.PlainMethod),
                () => true);
            Assert.IsNull(reason);
        }

        [Test]
        public void RequiresReadWrite_ReasonPropagated()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(DecoratedClass), null, () => true);
            StringAssert.Contains("class level reason", reason);
        }

        [Test]
        public void RequiresReadWrite_MethodLevelReason_Propagated()
        {
            var reason = UnityMcpTestBase.EnforceReadWriteRequirement(
                typeof(UndecoratedClass),
                nameof(UndecoratedClass.AnnotatedMethod),
                () => true);
            StringAssert.Contains("method level reason", reason);
        }
    }
}
