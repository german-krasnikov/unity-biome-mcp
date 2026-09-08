using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class CompileStateLifecycleTests
#if UNITY_EDITOR
        : UnityMCP.Editor.Testing.UnityMcpTestBase
#endif
    {
        [Test]
        public void FailedNativeSampleThenSuccessfulReload_ClearsOnlyThatCompletedCycle()
        {
            using var scope = CompileNotifier.BeginTestIsolation();
            float time = 10;
            CompileNotifier.NowSecondsFloat = () => time;
            CompileNotifier.RecordStarted(); time = 12; CompileNotifier.RecordFinished(true);
            time = 20; CompileNotifier.RecordStarted(); time = 22; CompileNotifier.RecordFinished(true);
            Assert.That(CompileNotifier.GetStatus(), Does.StartWith("idle-failed"));
            CompileNotifier.ReconcileAfterReload(false, false);
            Assert.That(CompileNotifier.GetStatus(), Does.StartWith("idle|"));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ReloadWithCurrentFailureCannotClearFailedState(bool nativeFailure, bool capturedErrors)
        {
            using var scope = CompileNotifier.BeginTestIsolation();
            CompileNotifier.RecordStarted(); CompileNotifier.RecordFinished(true);
            CompileNotifier.ReconcileAfterReload(nativeFailure, capturedErrors);
            Assert.That(CompileNotifier.GetStatus(), Does.StartWith("idle-failed"));
        }

        [Test]
        public void UnrelatedReloadCannotClearFailureWithoutCompletedCycle()
        {
            using var scope = CompileNotifier.BeginTestIsolation();
            SessionState.SetBool("MCP_CompileFailed", true);
            SessionState.SetFloat("MCP_CompileStart", 0);
            SessionState.EraseBool("MCP_CompileCompletedSinceReload");
            CompileNotifier.ReconcileAfterReload(false, false);
            Assert.That(CompileNotifier.GetStatus(), Does.StartWith("idle-failed"));
        }

        [Test]
        public void FinishWithoutObservedStartCannotCertifyLaterReload()
        {
            using var scope = CompileNotifier.BeginTestIsolation();
            SessionState.SetFloat("MCP_CompileStart", 0);
            SessionState.EraseBool("MCP_CompileCompletedSinceReload");
            CompileNotifier.RecordFinished(true);
            CompileNotifier.ReconcileAfterReload(false, false);
            Assert.That(CompileNotifier.GetStatus(), Does.StartWith("idle-failed"));
        }

        [Test]
        public void NewCompileSupersedesPendingReloadReconciliation()
        {
            using var scope = CompileNotifier.BeginTestIsolation();
            CompileNotifier.RecordStarted(); CompileNotifier.RecordFinished(true);
            CompileNotifier.RecordStarted();
            SessionState.SetBool("MCP_CompileFailed", true);
            CompileNotifier.ReconcileAfterReload(false, false);
            Assert.That(SessionState.GetBool("MCP_CompileFailed", false), Is.True);
            Assert.That(CompileNotifier.IsCompiling, Is.True);
        }

        [Test]
        public void GlobalDisplayCapCannotEraseLaterTargetFailure()
        {
            var name = "OwnedCapped_" + Guid.NewGuid().ToString("N");
            var key = "MCP_CompileErrors_" + name;
            var callback = typeof(CompileErrorCapture).GetMethod("OnCompilationFinished", BindingFlags.Static | BindingFlags.NonPublic);
            var errors = typeof(CompileErrorCapture).GetField("_errors", BindingFlags.Static | BindingFlags.NonPublic);
            var original = ((System.Collections.Generic.List<string>)errors.GetValue(null)).ToArray();
            var session = SessionState.GetString("MCP_CompileErrors", "");
            SessionState.SetString(key, "error CS0001: older target error");
            try
            {
                var list = (System.Collections.Generic.List<string>)errors.GetValue(null);
                list.Clear(); for (int i = 0; i < 50; i++) list.Add("error CS0002: other target");
                callback.Invoke(null, new object[] { name + ".dll", new[] { new CompilerMessage { type = CompilerMessageType.Error, message = "error CS9999: current target" } } });
                Assert.That(CompileErrorCapture.GetErrorsForAssembly(name + ".dll"), Does.Contain("CS9999"));
                Assert.That(CompileErrorCapture.HasErrors(), Is.True);
            }
            finally
            {
                var list = (System.Collections.Generic.List<string>)errors.GetValue(null);
                list.Clear(); list.AddRange(original);
                if (session == "") SessionState.EraseString("MCP_CompileErrors"); else SessionState.SetString("MCP_CompileErrors", session);
                callback.Invoke(null, new object[] { name + ".dll", Array.Empty<CompilerMessage>() });
                if (session == "") SessionState.EraseString("MCP_CompileErrors"); else SessionState.SetString("MCP_CompileErrors", session);
                SessionState.EraseString(key);
            }
        }

        [Test]
        public void ActualSuccessfulTargetCallbackClearsItsOldPersistedErrorsOnly()
        {
            var name = "OwnedFreshness_" + Guid.NewGuid().ToString("N");
            var key = "MCP_CompileErrors_" + name;
            var otherKey = key + "_Other";
            SessionState.SetString(key, "error CS0001: previous cycle");
            SessionState.SetString(otherKey, "error CS0002: another target");
            try
            {
                var callback = typeof(CompileErrorCapture).GetMethod("OnCompilationFinished", BindingFlags.Static | BindingFlags.NonPublic);
                callback.Invoke(null, new object[] { name + ".dll", Array.Empty<CompilerMessage>() });
                Assert.That(CompileErrorCapture.GetErrorsForAssembly(name + ".dll"), Is.EqualTo("No compilation errors"));
                Assert.That(SessionState.GetString(otherKey, ""), Does.Contain("CS0002"));
            }
            finally
            {
                SessionState.EraseString(key); SessionState.EraseString(otherKey);
            }
        }
    }
}
