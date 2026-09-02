using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpmOperationGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUpGuardTest()
        {
            // These SessionState keys are outside UnityMcpTestBase's known
            // isolations (ARC-10 T1) — reset explicitly so a leaked claim from a
            // prior test can't bleed in, and register restoration for this one.
            UpmOperationGuard.Complete();
            var originalClock = UpmOperationGuard.NowSecondsFloat;
            RegisterCleanup(() =>
            {
                UpmOperationGuard.Complete();
                UpmOperationGuard.NowSecondsFloat = originalClock;
            });
        }

        [Test]
        public void TryBegin_FirstCall_ClaimsGuard()
        {
            Assert.IsTrue(UpmOperationGuard.TryBegin("1.2.3"));
            Assert.IsTrue(UpmOperationGuard.IsInFlight);
            Assert.AreEqual("1.2.3", UpmOperationGuard.InFlightVersion);
        }

        [Test]
        public void TryBegin_WhileInFlight_SecondCallerBlocked()
        {
            Assert.IsTrue(UpmOperationGuard.TryBegin("1.0.0"));

            Assert.IsFalse(UpmOperationGuard.TryBegin("2.0.0"));
            Assert.AreEqual("1.0.0", UpmOperationGuard.InFlightVersion);
        }

        [Test]
        public void Complete_ClearsState()
        {
            UpmOperationGuard.TryBegin("1.2.3");

            UpmOperationGuard.Complete();

            Assert.IsFalse(UpmOperationGuard.IsInFlight);
            Assert.AreEqual("", UpmOperationGuard.InFlightVersion);
        }

        [Test]
        public void TryBegin_PastStaleCeiling_SelfHeals()
        {
            var now = 0f;
            UpmOperationGuard.NowSecondsFloat = () => now;
            Assert.IsTrue(UpmOperationGuard.TryBegin("1.0.0"));

            now = UpmOperationGuard.StaleCeilingSeconds + 1f;

            Assert.IsTrue(UpmOperationGuard.TryBegin("2.0.0"));
            Assert.AreEqual("2.0.0", UpmOperationGuard.InFlightVersion);
        }
    }
}
