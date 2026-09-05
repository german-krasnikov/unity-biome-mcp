// TDD: guards the FixtureAsyncState one-frame race — Run()'s while loop used to set
// progress=1f a full frame before isRunning=false, so a DSL step reading Progress==1
// could observe IsRunning still true. This fixture isolates the invariant directly
// against the component instead of round-tripping through the DSL runner, so the test
// stays deterministic regardless of PlaytestRunner step-to-step frame cadence.
//
// Same design rationale as PlaytestCorpusPlayModeTests: plain [TestFixture], not
// UnityMcpTestBase — that base is Editor-only and unverified under Play Mode. Nothing
// to leak here: the probe GameObject is created and destroyed within the test.
using System.Threading.Tasks;
using McpFeedbackFixture;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class FixtureAsyncStatePlayModeTests
    {
        private const int MaxFrames = 300;

        [Test]
        public async Task Run_ProgressReachesOne_IsRunningAlreadyFalseSameFrame()
        {
            var go = new GameObject("FixtureAsyncStateProbe");
            try
            {
                var state = go.AddComponent<FixtureAsyncState>();
                state.StartOperation(0.05f);

                var frames = 0;
                while (state.Progress < 1f)
                {
                    Assert.Less(frames, MaxFrames, "Progress never reached 1 within the frame budget");
                    await Awaitable.NextFrameAsync();
                    frames++;
                }

                Assert.IsFalse(state.IsRunning, "IsRunning must already be false the same frame Progress first reads 1");
            }
            finally
            {
                Object.Destroy(go);
            }
        }
    }
}
