// TDD — RED first. Tests drive CompileAutoFix contract via a test seam.
// compilationFinished is simulated by calling SimulateCompilation() instead of real compile.
using System;
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CompileAutoFixTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private CompileAutoFix _fix;
        private string         _lastDetectedErrors;
        private int            _fireCount;

        [SetUp]
        public void SetUp()
        {
            CompileErrorCapture.Clear();
            _fix              = new CompileAutoFix();
            _lastDetectedErrors = null;
            _fireCount        = 0;
            _fix.OnErrorsDetected += OnDetected;
        }

        [TearDown]
        public void TearDown()
        {
            _fix.OnErrorsDetected -= OnDetected;
        }

        private void OnDetected(string errors) { _lastDetectedErrors = errors; _fireCount++; }

        // ── Armed + errors → event fires ──────────────────────────────────────

        [Test]
        public void Armed_CompileWithErrors_EventFires()
        {
            InjectError("CS0001");
            _fix.Arm();
            _fix.SimulateCompilation(hasErrors: true);
            Assert.IsNotNull(_lastDetectedErrors, "OnErrorsDetected must fire when armed + errors");
        }

        [Test]
        public void Armed_CompileWithErrors_EventCarriesErrorText()
        {
            InjectError("CS0001: something wrong");
            _fix.Arm();
            _fix.SimulateCompilation(hasErrors: true);
            StringAssert.Contains("CS0001", _lastDetectedErrors);
        }

        // ── Armed + no errors → disarms, no event ─────────────────────────────

        [Test]
        public void Armed_CompileClean_NoEvent()
        {
            _fix.Arm();
            _fix.SimulateCompilation(hasErrors: false);
            Assert.IsNull(_lastDetectedErrors, "No event on clean compile");
        }

        [Test]
        public void Armed_CompileClean_Disarms()
        {
            _fix.Arm();
            _fix.SimulateCompilation(hasErrors: false);
            Assert.IsFalse(_fix.IsArmed);
        }

        // ── Not armed → no event ─────────────────────────────────────────────

        [Test]
        public void NotArmed_CompileWithErrors_NoEvent()
        {
            InjectError("CS0002");
            // Do NOT call Arm()
            _fix.SimulateCompilation(hasErrors: true);
            Assert.IsNull(_lastDetectedErrors);
        }

        // ── Retry cap (MaxRetries=3) ──────────────────────────────────────────

        [Test]
        public void Armed_RetryCap_StopsAfterThree()
        {
            InjectError("CS0003");
            _fix.Arm();
            _fix.SimulateCompilation(hasErrors: true);
            _fix.Arm(); // re-arm for second retry
            _fix.SimulateCompilation(hasErrors: true);
            _fix.Arm(); // re-arm for third retry
            _fix.SimulateCompilation(hasErrors: true);
            Assert.AreEqual(3, _fireCount, "Event must fire exactly 3 times");
        }

        // ── Disarm resets RetriesLeft ─────────────────────────────────────────

        [Test]
        public void Disarm_ResetsRetriesLeft()
        {
            InjectError("CS0006");
            // Use up 2 retries
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);
            // Explicit disarm (user intervened)
            _fix.Disarm();
            // Arm again — should allow 3 more retries
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);
            Assert.AreEqual(5, _fireCount, "After Disarm reset, 3 more retries must be available");
        }

        // ── No compile → no hang ─────────────────────────────────────────────

        [Test]
        public void NoCompile_Armed_NoEventNoException()
        {
            _fix.Arm();
            // Never call SimulateCompilation — just verify no crash/assertion.
            Assert.IsTrue(_fix.IsArmed, "Must stay armed without a compile event");
            Assert.IsNull(_lastDetectedErrors);
        }

        // ── CRITICAL #1: cap chip fires AFTER 3rd dispatch, NOT on same call ──

        [Test]
        public void ThreeErrorCompiles_ExactlyThreeDispatches_NoFourthFire()
        {
            // 3 error-compiles → exactly 3 OnErrorsDetected (= 3 dispatched turns).
            // After the 3rd, RetriesLeft==0 and IsArmed==false.
            // A 4th SimulateCompilation (armed=false already) must not fire again.
            InjectError("CS9999");
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);  // dispatch 1
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);  // dispatch 2
            _fix.Arm(); _fix.SimulateCompilation(hasErrors: true);  // dispatch 3
            Assert.AreEqual(3, _fireCount, "Exactly 3 dispatches expected");
            Assert.AreEqual(0, _fix.RetriesLeft, "RetriesLeft must be 0 after 3 errors");
            Assert.IsFalse(_fix.IsArmed, "Must not be armed after cap");
            // No 4th dispatch even if someone tries to arm beyond cap.
            _fix.SimulateCompilation(hasErrors: true);
            Assert.AreEqual(3, _fireCount, "No 4th dispatch");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void InjectError(string msg) => CompileErrorCapture.InjectForTest(msg);
    }
}
