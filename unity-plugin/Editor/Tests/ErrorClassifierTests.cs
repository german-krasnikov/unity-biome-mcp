using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ErrorClassifierTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ClassifyError_ArgumentException_ReturnsValidation()
        {
            Assert.AreEqual("VALIDATION", ErrorClassifier.Classify(new ArgumentException("bad arg")));
        }

        [Test]
        public void ClassifyError_ArgumentNullException_ReturnsValidation()
        {
            Assert.AreEqual("VALIDATION", ErrorClassifier.Classify(new ArgumentNullException("param")));
        }

        [Test]
        public void ClassifyError_KeyNotFoundException_ReturnsNotFound()
        {
            Assert.AreEqual("NOT_FOUND", ErrorClassifier.Classify(new KeyNotFoundException("key")));
        }

        [Test]
        public void ClassifyError_FileNotFoundException_ReturnsNotFound()
        {
            Assert.AreEqual("NOT_FOUND", ErrorClassifier.Classify(new FileNotFoundException("file")));
        }

        [Test]
        public void ClassifyError_InvalidOperationException_ReturnsState()
        {
            Assert.AreEqual("STATE", ErrorClassifier.Classify(new InvalidOperationException("bad state")));
        }

        [Test]
        public void ClassifyError_TimeoutException_ReturnsTimeout()
        {
            Assert.AreEqual("TIMEOUT", ErrorClassifier.Classify(new TimeoutException("timed out")));
        }

        [Test]
        public void ClassifyError_NullReferenceException_ReturnsNullRef()
        {
            Assert.AreEqual("NULL_REF", ErrorClassifier.Classify(new NullReferenceException("null")));
        }

        [Test]
        public void ClassifyError_GenericException_ReturnsInternal()
        {
            Assert.AreEqual("INTERNAL", ErrorClassifier.Classify(new Exception("generic")));
        }

        [Test]
        public void FormatError_IncludesClassAndMessage()
        {
            var result = ErrorClassifier.FormatError(new ArgumentException("bad arg"));
            Assert.AreEqual("VALIDATION: bad arg", result);
        }

        [Test]
        public void FormatError_TimeoutException_IncludesClassAndMessage()
        {
            var result = ErrorClassifier.FormatError(new TimeoutException("timed out"));
            Assert.AreEqual("TIMEOUT: timed out", result);
        }
    }
}
