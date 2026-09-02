// ARC-15 T2 (DEV-49): 30s rate limiter for the *unrecognized* desync-warning path only —
// IsKnownForeignProtocolProbe (DEV-48) already routes recognized AV/EDR/health-checker probes
// to Debug.Log, never reaching this limiter. Pure function of an injected nowTicks, so tests
// never sleep 30 real seconds. Exact-count assertions only (ARC-0a Arm B): no ">=" / "> 0"
// that could mask a miscount.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DesyncWarnLimiterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly long WindowTicks = TimeSpan.FromSeconds(30).Ticks;

        [Test]
        public void Record_FirstCallInWindow_LogsImmediately()
        {
            var limiter = new ClientConnectionHandler.DesyncWarnLimiter(WindowTicks);

            var (shouldLog, suppressed) = limiter.Record(1000L);

            Assert.IsTrue(shouldLog);
            Assert.AreEqual(0, suppressed);
        }

        [Test]
        public void Record_SecondAndThirdCallSameWindow_Suppressed()
        {
            var limiter = new ClientConnectionHandler.DesyncWarnLimiter(WindowTicks);
            const long t0 = 1000L;
            limiter.Record(t0);

            var (shouldLog2, _) = limiter.Record(t0 + 1);
            var (shouldLog3, _) = limiter.Record(t0 + 2);

            Assert.IsFalse(shouldLog2);
            Assert.IsFalse(shouldLog3);
        }

        [Test]
        public void Record_CallAfterWindowElapses_LogsWithExactSuppressedCount()
        {
            var limiter = new ClientConnectionHandler.DesyncWarnLimiter(WindowTicks);
            const long t0 = 1000L;
            limiter.Record(t0);
            limiter.Record(t0 + 1);
            limiter.Record(t0 + 2);

            var (shouldLog, suppressed) = limiter.Record(t0 + WindowTicks + 1);

            Assert.IsTrue(shouldLog);
            Assert.AreEqual(2, suppressed);
        }
    }
}
