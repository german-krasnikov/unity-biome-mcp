// TDD T2.5 — ToolCardBase protection contract.
//
// Main guarantee: naslednik fizicheski ne mozhet postavit' metku ran'she soderzhimogo.
// RenderedClass — privateoe pole bazy, OnUpdate — ne virtual.
//
// Double-red requirement:
//   Remove the try/catch from OnUpdate → first call sets marker before exception →
//   second call sees marker, skips, no content → Assert.AreEqual(1, childCount) FAILS.
//   Remove the "if (built)" guard → ReturnsFalse test fails (marker set when not ready).
//   Remove OnAdditionalRender call → AdditionalRenderCalled test fails.
//   Collapse IOException/Exception split → IOException test logs unexpected warning → FAILS.
//   Remove LogWarning from Exception catch → NRE test's LogAssert.Expect unsatisfied → FAILS.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ToolCardBaseTests : UnityMcpTestBase
    {
        // ── Test doubles ─────────────────────────────────────────────────────────

        /// <summary>Throws on first call, succeeds on second.</summary>
        private sealed class ThrowingCard : ToolCardBase
        {
            public int BuildCallCount;
            private bool _firstDone;

            public ThrowingCard() : base("test-rendered") { }

            protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
            {
                if (!_firstDone)
                {
                    _firstDone = true;
                    throw new System.InvalidOperationException("simulated build failure");
                }
                BuildCallCount++;
                chip.Add(new Label("content"));
                return true;
            }
        }

        /// <summary>Returns false until ReadyNow is set, then returns true.</summary>
        private sealed class NotReadyCard : ToolCardBase
        {
            public bool ReadyNow;

            public NotReadyCard() : base("test-rendered") { }

            protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
            {
                if (!ReadyNow) return false;
                chip.Add(new Label("ready"));
                return true;
            }
        }

        /// <summary>Always throws IOException — the silent/expected case (file gone between check and read).</summary>
        private sealed class IoCard : ToolCardBase
        {
            public IoCard() : base("io-rendered") { }
            protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
                => throw new System.IO.IOException("file gone between check and read");
        }

        /// <summary>Throws NullReferenceException — must produce a LogWarning.</summary>
        private sealed class NreCard : ToolCardBase
        {
            public NreCard() : base("nre-rendered") { }
            protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
                => throw new System.NullReferenceException("simulated null");
        }

        /// <summary>Counts OnAdditionalRender calls.</summary>
        private sealed class AdditionalRenderCard : ToolCardBase
        {
            public int AdditionalCallCount;

            public AdditionalRenderCard() : base("test-rendered") { }

            protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
            {
                chip.Add(new Label("content"));
                return true;
            }

            protected override void OnAdditionalRender(VisualElement chip, ToolCallRecord rec)
            {
                AdditionalCallCount++;
            }
        }

        private static ToolCallRecord MakeRec() =>
            new ToolCallRecord("test", "id-1", "{}", "result");

        // ── Core protection test ──────────────────────────────────────────────────
        //
        // RED if the try/catch is removed from OnUpdate:
        //   first call throws → marker set before exception lands → second call
        //   sees marker and skips → no content → Assert fails.

        [Test]
        public void OnUpdate_ContentBuildThrows_MarkerNotSet_AllowsRetry()
        {
            var card = new ThrowingCard();
            var chip = new VisualElement();
            var rec  = MakeRec();

            // ThrowingCard throws InvalidOperationException (non-IO) → must log a warning.
            // RED if LogWarning removed from the Exception catch in OnUpdate.
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ToolCardBase\].*test-rendered"));
            card.OnUpdate(chip, rec); // first call throws internally — must not propagate

            Assert.IsFalse(chip.ClassListContains("test-rendered"),
                "Marker must NOT be set when TryBuildContent throws. " +
                "Bug: marker placed before content — blocks all future retries.");
            Assert.AreEqual(0, chip.childCount, "No content after failed build");

            card.OnUpdate(chip, rec); // second call succeeds

            Assert.IsTrue(chip.ClassListContains("test-rendered"),
                "Marker must be set after successful retry");
            Assert.AreEqual(1, chip.childCount, "Content present after retry");
        }

        // ── Basic marker mechanics ────────────────────────────────────────────────

        [Test]
        public void OnUpdate_ContentReturnsTrue_MarkerSet()
        {
            var card = new AdditionalRenderCard();
            var chip = new VisualElement();
            card.OnUpdate(chip, MakeRec());
            Assert.IsTrue(chip.ClassListContains("test-rendered"),
                "Marker must be set when TryBuildContent returns true");
        }

        [Test]
        public void OnUpdate_ContentReturnsFalse_MarkerNotSet()
        {
            var card = new NotReadyCard { ReadyNow = false };
            var chip = new VisualElement();
            card.OnUpdate(chip, MakeRec());
            Assert.IsFalse(chip.ClassListContains("test-rendered"),
                "Marker must NOT be set when TryBuildContent returns false (not ready)");
        }

        [Test]
        public void OnUpdate_MarkerAlreadySet_ContentNotCalledAgain()
        {
            var card = new ThrowingCard();
            var chip = new VisualElement();
            var rec  = MakeRec();

            // Skip first-throw, let second succeed.
            // ThrowingCard throws InvalidOperationException (non-IO) → warning logged.
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ToolCardBase\].*test-rendered"));
            try { card.OnUpdate(chip, rec); } catch { }
            card.OnUpdate(chip, rec); // sets marker

            int countBefore = card.BuildCallCount;
            card.OnUpdate(chip, rec); // third call — marker already set
            Assert.AreEqual(countBefore, card.BuildCallCount,
                "TryBuildContent must not be called again once the primary marker is set");
        }

        // ── OnAdditionalRender hook ───────────────────────────────────────────────
        //
        // Base calls hook every time marker is set. Subclass owns idempotency.
        // RED if OnAdditionalRender call is removed from OnUpdate.

        [Test]
        public void OnUpdate_MarkerSet_AdditionalRenderCalledEachSubsequentUpdate()
        {
            var card = new AdditionalRenderCard();
            var chip = new VisualElement();
            var rec  = MakeRec();

            card.OnUpdate(chip, rec); // sets marker, calls hook once
            Assert.AreEqual(1, card.AdditionalCallCount,
                "OnAdditionalRender must be called on the update that sets the marker");

            card.OnUpdate(chip, rec); // marker already set — hook called again
            Assert.AreEqual(2, card.AdditionalCallCount,
                "OnAdditionalRender must be called on every subsequent update (base does not guard it)");
        }

        // ── IOException is silent (expected I/O race) ─────────────────────────────
        //
        // RED if the IOException catch is removed (merged into the Exception catch):
        //   IoCard throws IOException → LogWarning is called → Unity fails the test
        //   because an unexpected warning appeared.

        [Test]
        public void OnUpdate_TryBuildThrowsIO_Silent_NoWarningAndAllowsRetry()
        {
            var card = new IoCard();
            var chip = new VisualElement();
            // No LogAssert.Expect — IOException must produce NO log (silent, expected I/O race).
            // Unity fails the test if an unexpected warning appears.
            card.OnUpdate(chip, MakeRec());
            Assert.IsFalse(chip.ClassListContains("io-rendered"),
                "Marker not set on IOException — retry allowed");
        }

        // ── Non-IO exceptions log a warning ──────────────────────────────────────
        //
        // RED if LogWarning is removed from the Exception catch in OnUpdate.

        [Test]
        public void OnUpdate_TryBuildThrowsNRE_LogsWarning()
        {
            var card = new NreCard();
            var chip = new VisualElement();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ToolCardBase\].*nre-rendered"));
            card.OnUpdate(chip, MakeRec());
            Assert.IsFalse(chip.ClassListContains("nre-rendered"),
                "Marker not set on NRE — retry allowed");
        }

        // ── OnStart is a no-op ────────────────────────────────────────────────────

        [Test]
        public void OnStart_DoesNotModifyChip()
        {
            IToolCardRenderer card = new AdditionalRenderCard();
            var chip = new VisualElement();
            card.OnStart(chip, MakeRec());
            Assert.AreEqual(0, chip.childCount, "OnStart must be a no-op");
        }
    }
}
