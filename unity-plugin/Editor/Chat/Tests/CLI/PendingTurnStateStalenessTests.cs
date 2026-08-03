// TDD — PendingTurnState staleness guard, corrupt input, and v4 backward compat.
// Zero Unity deps: System only. Pure NUnit.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PendingTurnStateStalenessTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Staleness_ActiveTurn_OldTimestamp_ShouldDiscard()
        {
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 1000L);
            Assert.IsTrue(PendingTurnState.IsStale(state, nowUtc: 1061L));
        }

        [Test]
        public void Staleness_ActiveTurn_FreshTimestamp_ShouldKeep()
        {
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 1000L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 1005L));
        }

        [Test]
        public void Staleness_IdleSave_OldTimestamp_Exempt()
        {
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Idle", savedAtUtc: 1000L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 9999L));
        }

        [Test]
        public void Staleness_Legacy_ZeroTimestamp_AllowedThrough()
        {
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 0L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 9999L));
        }

        // ── Corrupt input ─────────────────────────────────────────────────────

        [Test]
        public void Deserialize_TruncatedHeader_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize("a|b|c"));

        [Test]
        public void Deserialize_MissingChipLines_GracefulEmpty()
        {
            var textB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            var raw = $"sess|{textB64}|0||{actB64}|2|5|100|0";
            var rt = PendingTurnState.Deserialize(raw);
            Assert.IsNotNull(rt);
            Assert.AreEqual(2, rt.Value.ChipPaths.Length);
            Assert.AreEqual("", rt.Value.ChipPaths[0]);
            Assert.AreEqual("", rt.Value.ChipPaths[1]);
        }

        [Test]
        public void Deserialize_InvalidBase64InText_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize("sess|NOT_VALID!!!|0||SWRsZQ==|0"));

        [Test]
        public void Deserialize_NegativeChipCount_ReturnsNull()
        {
            var textB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            Assert.IsNull(PendingTurnState.Deserialize($"sess|{textB64}|0||{actB64}|-1"));
        }

        // ── Backward compat ───────────────────────────────────────────────────

        [Test]
        public void V1Header_AllDefaultsIncludingBackendAndKindKeys()
        {
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind);
            Assert.AreEqual(0, rt.Value.KindKeys.Length);
        }

        [Test]
        public void RoundTrip_ChipPathWithPipe_SurvivesBase64()
        {
            var orig = new PendingTurnState("s", "t",
                new[] { "Assets/foo|bar.cs" }, false, "", "Idle",
                kindKeys: new[] { ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo|bar.cs", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void RoundTrip_ChipPathWithNewline_SurvivesBase64()
        {
            var orig = new PendingTurnState("s", "t",
                new[] { "Assets/foo\nbar.cs" }, false, "", "Idle",
                kindKeys: new[] { ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo\nbar.cs", rt.Value.ChipPaths[0]);
        }

        // ── Custom threshold ──────────────────────────────────────────────────

        [Test]
        public void IsStale_CustomThreshold_BarelyOver_IsStale()
        {
            // savedAt=1000, now=1011, threshold=10 → 11 > 10 → stale
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 1000L);
            Assert.IsTrue(PendingTurnState.IsStale(state, nowUtc: 1011L, thresholdSec: 10));
        }

        [Test]
        public void IsStale_CustomThreshold_BarelyUnder_NotStale()
        {
            // savedAt=1000, now=1009, threshold=10 → 9 <= 10 → not stale
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 1000L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 1009L, thresholdSec: 10));
        }

        [Test]
        public void IsStale_CustomThreshold_ExactlyEqual_NotStale()
        {
            // nowUtc - savedAtUtc == threshold → NOT stale (> not >=)
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Sending", savedAtUtc: 1000L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 1010L, thresholdSec: 10));
        }

        [Test]
        public void IsStale_VeryShortThreshold_StillIdleExempt()
        {
            var state = new PendingTurnState("s", "t", new string[0], false, "", "Idle", savedAtUtc: 1000L);
            Assert.IsFalse(PendingTurnState.IsStale(state, nowUtc: 1001L, thresholdSec: 1));
        }
    }
}
