// TDD: Result envelope fixes — ok:true on failure (Phase 2, MCP audit v0.91).
// Covers: TargetInvocationException unwrapping in ErrorClassifier.FormatError.
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ResultEnvelopeTests
    {
        [Test]
        public void ErrorClassifier_UnwrapsTargetInvocationException()
        {
            var inner   = new System.NullReferenceException("obj was null");
            var wrapped = new TargetInvocationException(inner);
            var result  = ErrorClassifier.FormatError(wrapped);
            Assert.That(result, Does.StartWith("NULL_REF:"));
            Assert.That(result, Does.Contain("obj was null"));
        }

        [Test]
        public void ErrorClassifier_TargetInvocationException_NoInner_FallsThrough()
        {
            // InnerException == null → classify the wrapper itself
            var wrapped = new TargetInvocationException("no inner", null);
            var result  = ErrorClassifier.FormatError(wrapped);
            // TargetInvocationException is not in the switch → "INTERNAL"
            Assert.That(result, Does.StartWith("INTERNAL:"));
        }
    }
}
