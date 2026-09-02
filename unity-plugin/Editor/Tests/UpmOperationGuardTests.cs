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

        // QUALITY-FIX-CS #1: the docstring on StaleCeilingSeconds claims it "Mirrors
        // CompileNotifier.StaleCeilingSeconds" but the value was an independent 180f
        // literal (CompileNotifier's is 300f) — the comment was a lie. Pins the single
        // source of truth so the two can never silently diverge again.
        [Test]
        public void StaleCeilingSeconds_MirrorsCompileNotifierSingleSource()
        {
            Assert.AreEqual(CompileNotifier.StaleCeilingSeconds, UpmOperationGuard.StaleCeilingSeconds);
        }

        // QUALITY-FIX-CS #1: UpmPluginUpdater.Update() chains
        // UpmPluginUpdater.ChainedPackageAdds sequential Client.Add calls (editor
        // package, then reload package), each defaulting to
        // UpmPluginUpdater.DefaultTimeoutSeconds — worst-case legitimate in-flight
        // duration is their product. A ceiling below that (the old 180f) lets a second
        // caller falsely reclaim the guard while a real, still-running update holds it —
        // the P6-symptom of a parallel Add causing "UPM busy". The ceiling must outlive
        // that worst case.
        [Test]
        public void TryBegin_AtWorstCaseUpmDuration_DoesNotFalselySelfHeal()
        {
            const float worstCaseUpmDurationSeconds =
                (float)(UpmPluginUpdater.ChainedPackageAdds * UpmPluginUpdater.DefaultTimeoutSeconds);
            var now = 0f;
            UpmOperationGuard.NowSecondsFloat = () => now;
            Assert.IsTrue(UpmOperationGuard.TryBegin("1.0.0"));

            now = worstCaseUpmDurationSeconds - 1f;

            Assert.IsFalse(UpmOperationGuard.TryBegin("2.0.0"),
                "guard self-healed before the legitimate worst-case UPM duration elapsed");
            Assert.AreEqual("1.0.0", UpmOperationGuard.InFlightVersion);
        }

        // R2 #5: a stale claim left by any reload OTHER than the update's own version
        // bump (an unrelated script save, entering Play Mode, a sync_unity recompile)
        // never runs CheckVersionChange's version-match release path, so nothing but
        // this getter's own ceiling check can ever clear it. Proves the getter
        // ACTIVELY clears the claim (Complete()), not merely reports staleness.
        [Test]
        public void IsActive_PastCeiling_SelfHealsWithoutTryBegin()
        {
            var now = 0f;
            UpmOperationGuard.NowSecondsFloat = () => now;
            UpmOperationGuard.TryBegin("9.9.9");

            now = UpmOperationGuard.StaleCeilingSeconds + 1f;

            Assert.IsFalse(UpmOperationGuard.IsActive);
            Assert.IsFalse(UpmOperationGuard.IsInFlight);
        }

        [Test]
        public void IsActive_UnderCeiling_StaysInFlight()
        {
            var now = 0f;
            UpmOperationGuard.NowSecondsFloat = () => now;
            UpmOperationGuard.TryBegin("9.9.9");

            now = UpmOperationGuard.StaleCeilingSeconds - 1f;

            Assert.IsTrue(UpmOperationGuard.IsActive);
            Assert.IsTrue(UpmOperationGuard.IsInFlight);
        }
    }
}
