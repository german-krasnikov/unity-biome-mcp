using System;
using NUnit.Framework;
using UnityEngine.Networking;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpdateCheckerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void HasUpdate_FalseWhenNoVersion()
        {
            Assert.IsFalse(UpdateChecker.HasUpdate);
        }

        [Test]
        public void HasUpdate_TrueWhenVersionSet()
        {
            UpdateChecker.SetAvailableVersionForTest("1.99.0");
            Assert.IsTrue(UpdateChecker.HasUpdate);
        }

        [Test]
        public void SkipVersion_ClearsAvailableVersion()
        {
            ProtectEditorPrefString("UnityMCP.SkippedVersion");
            UpdateChecker.SetAvailableVersionForTest("1.99.0");
            UpdateChecker.SkipVersion();
            Assert.IsFalse(UpdateChecker.HasUpdate);
        }

        [Test]
        public void SkipVersion_WhenNoVersion_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UpdateChecker.SkipVersion());
        }

        [Test]
        public void ForceCheckAsync_WhenIsCheckingStuck_ResetsAndStartsNewRequest()
        {
            using (UpdateChecker.BeginTestIsolation())
            {
                UpdateChecker.SetStateForTest(null, true, null);

                UpdateChecker.ForceCheckAsync();

                Assert.IsTrue(UpdateChecker.IsChecking);
                Assert.IsNotNull(UpdateChecker.ActiveRequestForTest);
            }
        }

        [Test]
        public void ForceCheckAsync_SetsTimeoutOnRequest()
        {
            using (UpdateChecker.BeginTestIsolation())
            {
                UpdateChecker.ForceCheckAsync();

                Assert.AreEqual(15, UpdateChecker.ActiveRequestForTest.timeout);
            }
        }

        [Test]
        public void ForceCheckAsync_CancelsStaleRequest_BeforeStarting()
        {
            using (UpdateChecker.BeginTestIsolation())
            {
                var staleRequest = UnityWebRequest.Get("https://example.invalid/stale");
                UpdateChecker.SetStateForTest(null, true, null, staleRequest);

                UpdateChecker.ForceCheckAsync();

                Assert.IsNotNull(UpdateChecker.ActiveRequestForTest);
                Assert.AreNotSame(staleRequest, UpdateChecker.ActiveRequestForTest);
            }
        }

        [Test]
        public void TestIsolation_NestedScope_RestoresExactOuterState()
        {
            var outerRequest = UnityWebRequest.Get("https://example.invalid/update-check");
            UpdateChecker.SetStateForTest("1.2.3", true, "outer-error", outerRequest);
            var outerCompletions = 0;
            Action outerHandler = () => outerCompletions++;
            UpdateChecker.CheckCompleted += outerHandler;

            using (UpdateChecker.BeginTestIsolation())
            {
                Assert.IsNull(UpdateChecker.AvailableVersion);
                Assert.IsFalse(UpdateChecker.IsChecking);
                Assert.IsNull(UpdateChecker.LastError);
                Assert.IsNull(UpdateChecker.ActiveRequestForTest);

                var innerCompletions = 0;
                UpdateChecker.SetStateForTest("9.9.9", true, "inner-error");
                UpdateChecker.CheckCompleted += () => innerCompletions++;
                UpdateChecker.RaiseCheckCompletedForTest();

                Assert.AreEqual(1, innerCompletions);
                Assert.AreEqual(0, outerCompletions);
            }

            Assert.AreEqual("1.2.3", UpdateChecker.AvailableVersion);
            Assert.IsTrue(UpdateChecker.IsChecking);
            Assert.AreEqual("outer-error", UpdateChecker.LastError);
            Assert.AreSame(outerRequest, UpdateChecker.ActiveRequestForTest);

            UpdateChecker.RaiseCheckCompletedForTest();
            Assert.AreEqual(1, outerCompletions);
            UpdateChecker.CheckCompleted -= outerHandler;
        }
    }
}
