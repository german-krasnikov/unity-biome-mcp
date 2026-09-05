// TDD: C06 — EXPECT_FAIL step modifier, pure-function slice only (parsing + AdvanceStep wiring
// land in C07). ApplyExpectFail compares the run's passed/failed counters before and after one
// step to find which counter that step itself moved, then inverts it when expectFail is set.
// Modeled on PlaytestRunnerPureLogicTests.cs: no scene, no Play Mode, pure static calls.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestExpectFailTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ApplyExpectFail_StepRawFailed_FlipsToPassed()
        {
            // Step raw-failed (failed 0 -> 1). EXPECT_FAIL means that failure was expected,
            // so the overall outcome flips to a pass.
            var (passed, failed) = PlaytestRunner.ApplyExpectFail(
                passedBefore: 0, failedBefore: 0, passedAfter: 0, failedAfter: 1, expectFail: true);

            Assert.AreEqual(1, passed);
            Assert.AreEqual(0, failed);
        }

        [Test]
        public void ApplyExpectFail_StepRawPassed_FlipsToFailed()
        {
            // Step raw-passed (passed 0 -> 1) but was EXPECTed to fail — an expected-fail step
            // that unexpectedly passes IS itself a failure.
            var (passed, failed) = PlaytestRunner.ApplyExpectFail(
                passedBefore: 0, failedBefore: 0, passedAfter: 1, failedAfter: 0, expectFail: true);

            Assert.AreEqual(0, passed);
            Assert.AreEqual(1, failed);
        }

        [Test]
        public void ApplyExpectFail_ExpectFailFalse_NoOp()
        {
            // Regression: every existing script (no EXPECT_FAIL) is bit-for-bit unchanged.
            var (passed, failed) = PlaytestRunner.ApplyExpectFail(
                passedBefore: 3, failedBefore: 1, passedAfter: 4, failedAfter: 1, expectFail: false);

            Assert.AreEqual(4, passed);
            Assert.AreEqual(1, failed);
        }
    }
}
