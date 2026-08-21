// TDD — DedupRegistry domain-reload persistence (Subtask 1 of ARCH-domain-reload-state-fixes).
// Tests simulate a domain reload: populate in-memory state, call Save(), create a NEW
// DedupRegistry instance (mimics field re-initialization on reload), call Load(), assert state.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DedupRegistryReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private long _nowTicks;
        private DedupRegistry _original;

        [SetUp]
        public void Setup()
        {
            _nowTicks = DateTime.UtcNow.Ticks;
            _original = CommandRouter._dedupRegistry;
            SessionState.EraseString(DedupRegistry.SessionKey);
        }

        [TearDown]
        public void Teardown()
        {
            CommandRouter._dedupRegistry = _original;
            SessionState.EraseString(DedupRegistry.SessionKey);
        }

        // ── Round-trip ────────────────────────────────────────────────────────

        [Test]
        public void DedupRegistry_SurvivesDomainReload_KnownOpIdBlocked()
        {
            var before = new DedupRegistry(clock: () => _nowTicks);
            before.TryRegister("op-reload-1", "result-text");
            before.Save();

            var after = new DedupRegistry(clock: () => _nowTicks);
            after.Load();

            Assert.IsTrue(after.ContainsWithinTtl("op-reload-1"),
                "op must be visible after Save/Load round-trip");
            Assert.IsFalse(after.TryRegister("op-reload-1"),
                "TryRegister must return false — dedup is active post-reload");
        }

        // ── Recent-only filter ────────────────────────────────────────────────

        [Test]
        public void DedupRegistry_SurvivesDomainReload_OnlyRecentOpsPreserved()
        {
            long mutableClock = _nowTicks - TimeSpan.FromSeconds(70).Ticks;
            var before = new DedupRegistry(clock: () => mutableClock);
            before.TryRegister("old-op");       // stored at now - 70s

            mutableClock = _nowTicks;
            before.TryRegister("recent-op");    // stored at now
            before.Save();                      // cutoff = now - 60s → old-op excluded

            var after = new DedupRegistry(clock: () => _nowTicks);
            after.Load();

            Assert.IsFalse(after.ContainsWithinTtl("old-op"),
                "ops older than 60s must not be persisted across reload");
            Assert.IsTrue(after.ContainsWithinTtl("recent-op"),
                "ops within last 60s must survive domain reload");
        }

        // ── Empty SessionState ────────────────────────────────────────────────

        [Test]
        public void DedupRegistry_Load_EmptySessionState_DoesNotThrow()
        {
            var registry = new DedupRegistry();
            Assert.DoesNotThrow(() => registry.Load());
            Assert.AreEqual(0, registry.Count);
        }

        // ── CommandRouter integration ─────────────────────────────────────────

        [Test]
        public void CommandRouter_RetryAfterReload_ReturnsDedupApplied()
        {
            // Arrange: register op, save, simulate reload by replacing _dedupRegistry
            var pre = new DedupRegistry(clock: () => _nowTicks);
            pre.TryRegister("op-router-reload", "{}");
            pre.Save();

            var post = new DedupRegistry(clock: () => _nowTicks);
            post.Load();
            CommandRouter._dedupRegistry = post;

            // Act: send retry with op_id that was processed before reload
            const string json = "{\"id\":\"t1\",\"cmd\":\"ping\",\"retry_op_id\":\"op-router-reload\",\"args\":{}}";
            var response = CommandRouter.Process(json);

            // Assert: response acknowledges dedup even though result is unknown
            StringAssert.Contains("dedup_applied", response,
                "Process must return dedup_applied:true for op known post-reload");
        }
    }
}
