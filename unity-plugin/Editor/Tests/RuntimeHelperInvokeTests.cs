// TDD #01: RuntimeHelper.InvokeMethod exception unwrapping + ErrorClassifier stack frame.
// EditMode only — no TCP, no Play Mode required.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperInvokeTests
    {
        // ── ErrorClassifier.FormatError ───────────────────────────────────────

        [Test]
        public void FormatError_NullRefException_StartsWithNullRef()
        {
            Exception e;
            try { throw new NullReferenceException("test null ref"); }
            catch (Exception ex) { e = ex; }
            Assert.That(ErrorClassifier.FormatError(e), Does.StartWith("NULL_REF:"));
        }

        [Test]
        public void FormatError_NullRefException_ContainsMessage()
        {
            Exception e;
            try { throw new NullReferenceException("test"); }
            catch (Exception ex) { e = ex; }
            Assert.That(ErrorClassifier.FormatError(e), Does.Contain("test"));
        }

        [Test]
        public void FormatError_ArgumentException_StartsWithValidation()
        {
            Exception e;
            try { throw new ArgumentException("bad arg"); }
            catch (Exception ex) { e = ex; }
            Assert.That(ErrorClassifier.FormatError(e), Does.StartWith("VALIDATION:"));
        }

        [Test]
        public void FormatError_MessageNoStack_OmitsAtLine()
        {
            // Exception with no stack trace (created but not thrown)
            var e = new Exception("plain message");
            var result = ErrorClassifier.FormatError(e);
            Assert.That(result, Does.StartWith("INTERNAL: plain message"));
            // No " at " line appended when there is no stack trace
            Assert.That(result, Does.Not.Contain(" at "));
        }

        // ── ExceptionDispatchInfo preserves type (mirrors the RuntimeHelper fix) ──

        [Test]
        public void ExceptionDispatchInfo_PreservesOriginalExceptionType()
        {
            // Simulates what InvokeMethod now does: rethrow inner via EDI
            NullReferenceException original;
            try { throw new NullReferenceException("original"); }
            catch (NullReferenceException ex) { original = ex; }

            Exception rethrown = null;
            try
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
            }
            catch (Exception ex) { rethrown = ex; }

            Assert.IsInstanceOf<NullReferenceException>(rethrown);
            var formatted = ErrorClassifier.FormatError(rethrown);
            Assert.That(formatted, Does.StartWith("NULL_REF:"));
            Assert.That(formatted, Does.Contain("original"));
        }

        [Test]
        public void ExceptionDispatchInfo_ArgumentException_FormatsAsValidation()
        {
            ArgumentException original;
            try { throw new ArgumentException("bad"); }
            catch (ArgumentException ex) { original = ex; }

            Exception rethrown = null;
            try
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
            }
            catch (Exception ex) { rethrown = ex; }

            Assert.That(ErrorClassifier.FormatError(rethrown), Does.StartWith("VALIDATION:"));
        }
    }
}
