// TDD — RequiresReadWriteAttribute classification and enforcement logic.
// Tests call EnforceReadWriteRequirement directly to avoid IgnoreException propagation.
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
            // Decorated class + IsReadOnly=true → throws (ignore exception)
            var ex = Assert.Throws<Exception>(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(DecoratedClass), null, () => true));
            Assert.IsNotNull(ex);
        }

        [Test]
        public void RequiresReadWrite_MethodLevel_SkipsOnReadOnly()
        {
            var ex = Assert.Throws<Exception>(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(UndecoratedClass),
                    nameof(UndecoratedClass.AnnotatedMethod),
                    () => true));
            Assert.IsNotNull(ex);
        }

        [Test]
        public void RequiresReadWrite_ReadWriteWorker_Proceeds()
        {
            // Decorated class but IsReadOnly=false → no exception
            Assert.DoesNotThrow(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(DecoratedClass), null, () => false));
        }

        [Test]
        public void RequiresReadWrite_NoAttribute_Proceeds()
        {
            // No attribute + IsReadOnly=true → no exception
            Assert.DoesNotThrow(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(UndecoratedClass),
                    nameof(UndecoratedClass.PlainMethod),
                    () => true));
        }

        [Test]
        public void RequiresReadWrite_ReasonPropagated()
        {
            var ex = Assert.Throws<Exception>(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(DecoratedClass), null, () => true));
            StringAssert.Contains("class level reason", ex.Message);
        }

        [Test]
        public void RequiresReadWrite_MethodLevelReason_Propagated()
        {
            var ex = Assert.Throws<Exception>(
                () => UnityMcpTestBase.EnforceReadWriteRequirement(
                    typeof(UndecoratedClass),
                    nameof(UndecoratedClass.AnnotatedMethod),
                    () => true));
            StringAssert.Contains("method level reason", ex.Message);
        }
    }
}
