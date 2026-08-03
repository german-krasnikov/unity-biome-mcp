// RelayBackendConstructionMonkeyTests — 25 ctor/SetMode/Stop edge-case tests.
// Tests 101-125. No real relay process — tests stop before Start() or use null proc.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayBackendConstructionMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            RelayBackend.ProcessFactory       = null;
            RelaySpawner.EnsureRunningOverride = null;
        }

        static FieldInfo Field(string name) =>
            typeof(RelayBackend).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        [Test] public void Ctor_EmptyBackendId_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("", "ask", "", 0));

        [Test] public void Ctor_NullModel_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", null, 0));

        [Test] public void Ctor_UnicodeModel_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", "モデル-v1", 0));

        [Test] public void Ctor_NegativePort_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", "", -1));

        [Test] public void Ctor_LargePort_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", "", 65535));

        [Test] public void Ctor_WithResumeSession_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", "", 0, "sess-xyz"));

        [Test] public void Ctor_NullResumeSession_DoesNotThrow()
            => Assert.DoesNotThrow(() => new RelayBackend("claude", "ask", "", 0, null));

        [Test] public void Ctor_IsRunning_FalseInitially()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.IsFalse(b.IsRunning);
        }

        [Test] public void Ctor_SessionId_NullInitially()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.IsNull(b.SessionId);
        }

        [Test] public void SetMode_Agent_UpdatesModeField()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            b.SetMode("agent");
            Assert.AreEqual("agent", (string)Field("_mode").GetValue(b));
        }

        [Test] public void SetMode_Ask_UpdatesModeField()
        {
            var b = new RelayBackend("claude", "agent", "", 0);
            b.SetMode("ask");
            Assert.AreEqual("ask", (string)Field("_mode").GetValue(b));
        }

        [Test] public void SetMode_Null_UpdatesModeField()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            b.SetMode(null);
            Assert.IsNull((string)Field("_mode").GetValue(b));
        }

        [Test] public void SetMode_100x_FinalValueCorrect()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            for (int i = 0; i < 100; i++) b.SetMode(i % 2 == 0 ? "ask" : "agent");
            // last i=99 → "agent"
            Assert.AreEqual("agent", (string)Field("_mode").GetValue(b));
        }

        [Test] public void Stop_WithNullProc_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => b.Stop());
        }

        [Test] public void Stop_5x_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 5; i++) b.Stop(); });
        }

        [Test] public void Dispose_WithNullProc_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => b.Dispose());
        }

        [Test] public void Dispose_5x_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 5; i++) b.Dispose(); });
        }

        [Test] public void StopThenDispose_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => { b.Stop(); b.Dispose(); });
        }

        [Test] public void DrainEvents_NullProc_ProducesNoOutput()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            var output = new List<ChatEvent>();
            b.DrainEvents(output);
            Assert.AreEqual(0, output.Count);
        }

        [Test] public void DrainEvents_NullProc_NullToolOutput_NoThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            var output = new List<ChatEvent>();
            Assert.DoesNotThrow(() => b.DrainEvents(output, null));
        }

        [Test] public void DrainEvents_100x_NullProc_ZeroEvents()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            var output = new List<ChatEvent>();
            for (int i = 0; i < 100; i++) b.DrainEvents(output);
            Assert.AreEqual(0, output.Count);
        }

        [Test] public void SendControlResponse_WithNullProc_DoesNotThrow()
        {
            var b = new RelayBackend("claude", "ask", "", 0);
            Assert.DoesNotThrow(() => b.SendControlResponse("{}"));
        }

        [Test] public void Ctor_BackendIdField_Matches()
        {
            var b = new RelayBackend("codex", "ask", "", 0);
            Assert.AreEqual("codex", (string)Field("_backendId").GetValue(b));
        }

        [Test] public void Ctor_ModeField_Matches()
        {
            var b = new RelayBackend("claude", "agent", "", 0);
            Assert.AreEqual("agent", (string)Field("_mode").GetValue(b));
        }

        [Test] public void Stop_SetsIsRunningFalse()
        {
            RelayBackend.ProcessFactory       = () => new RelayChatProcess(j => "{\"ok\":true,\"data\":\"\"}");
            RelaySpawner.EnsureRunningOverride = () => 19600;
            var b = new RelayBackend("claude", "ask", "", 0);
            b.Start();
            b.Stop();
            Assert.IsFalse(b.IsRunning);
        }
    }
}
