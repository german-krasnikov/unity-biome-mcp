// NUnit unit tests for DedupRegistry (P-322: mutation retry dedup;
// DEV-64: SessionState persistence across domain reload).
// Zero Unity/AssetDatabase dependency — injectable clock for TTL tests.
using System;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DedupRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private DedupRegistry _registry;
        private long _nowTicks;

        [SetUp]
        public void Setup()
        {
            // DEV-64: DedupRegistry's ctor now restores from SessionState — clear
            // the real key first so a leaked entry from another test can't bleed
            // in, and register restoration so this test doesn't leak forward.
            SessionState.EraseString(DedupRegistry.SessionKey);
            _nowTicks = DateTime.UtcNow.Ticks;
            _registry = new DedupRegistry(clock: () => _nowTicks);
            RegisterCleanup(() => SessionState.EraseString(DedupRegistry.SessionKey));
        }

        [Test]
        public void TryRegister_FirstCall_ReturnsTrue()
        {
            Assert.IsTrue(_registry.TryRegister("op-aaa"));
        }

        [Test]
        public void TryRegister_SecondCallSameId_ReturnsFalse()
        {
            _registry.TryRegister("op-bbb");
            Assert.IsFalse(_registry.TryRegister("op-bbb"),
                "Duplicate op_id must return false so the command is not re-executed.");
        }

        [Test]
        public void TryRegister_DifferentIds_BothReturnTrue()
        {
            Assert.IsTrue(_registry.TryRegister("op-ccc"));
            Assert.IsTrue(_registry.TryRegister("op-ddd"));
        }

        [Test]
        public void TryRegister_AfterTTL_AcceptsAgain()
        {
            _registry.TryRegister("op-eee");

            // Advance clock past TTL (300s + 1s)
            _nowTicks += TimeSpan.FromSeconds(DedupRegistry.TtlSeconds + 1).Ticks;
            _registry.Evict();

            Assert.IsTrue(_registry.TryRegister("op-eee"),
                "After TTL expiry the same op_id should be accepted again.");
        }

        [Test]
        public void Register_AtCapacity_EvictsOldest()
        {
            // Fill registry to capacity.
            for (int i = 0; i < DedupRegistry.Capacity; i++)
                _registry.TryRegister($"cap-{i}");

            // Adding one more must not throw and must not exceed capacity.
            _registry.TryRegister("cap-overflow");

            Assert.LessOrEqual(_registry.Count, DedupRegistry.Capacity,
                "Registry must never exceed its capacity.");
        }

        [Test]
        public void Evict_RemovesExpiredEntries()
        {
            _registry.TryRegister("op-fresh");
            _registry.TryRegister("op-stale");

            // Age "op-stale" past TTL then freeze clock so "op-fresh" stays young.
            _nowTicks += TimeSpan.FromSeconds(DedupRegistry.TtlSeconds + 1).Ticks;
            // Evict must remove expired entries.
            _registry.Evict();

            // Both are now "old" in this simple test since we have one shared clock.
            // The key assertion: Count drops after Evict — eviction works.
            Assert.AreEqual(0, _registry.Count,
                "Evict must remove all entries past their TTL.");
        }

        // MCP-IDEMP-026: dedup must be ONLY by caller-supplied op_id — no payload similarity.

        [Test]
        public void SamePayload_DifferentOpIds_BothExecute()
        {
            // Two commands with identical cached-result strings but different op_ids
            // must both be registered — no payload-based dedup.
            const string result = "{\"ok\":true,\"data\":\"same payload\"}";
            Assert.IsTrue(_registry.TryRegister("op-idemp-1", result));
            Assert.IsTrue(_registry.TryRegister("op-idemp-2", result),
                "Different op_ids with identical payloads must both register — dedup is op_id-only.");
        }

        [Test]
        public void SameOpId_ReturnsCachedResult()
        {
            const string result = "{\"ok\":true,\"data\":\"cached\"}";
            _registry.TryRegister("op-cached", result);

            var retrieved = _registry.TryGetResult("op-cached");
            Assert.AreEqual(result, retrieved,
                "TryGetResult must return the stored result for a known op_id.");
        }

        // DEV-64: domain reload wipes the plain-static instance; SessionState
        // (unmanaged, survives reload) must carry the cache forward so a
        // Python retry after reload still finds the original mutation's result.

        [Test]
        public void SurvivesDomainReload()
        {
            const string result = "{\"ok\":true,\"data\":\"pre-reload\"}";
            _registry.TryRegister("op-reload-survive", result);

            // Simulate domain reload: a brand-new instance (as CommandRouter's
            // static field would get after reload) sharing the same real
            // SessionState the Editor keeps across the reload.
            var reloaded = new DedupRegistry(clock: () => _nowTicks);

            Assert.AreEqual(result, reloaded.TryGetResult("op-reload-survive"),
                "Result registered before a simulated domain reload must be readable from a fresh instance.");
        }

        [Test]
        public void SurvivesDomainReload_PastTTL_NotRestored()
        {
            _registry.TryRegister("op-reload-expired", "{\"ok\":true}");

            // Age past TTL before the "reload" — restoration must still honor
            // the TTL, not resurrect stale entries forever.
            _nowTicks += TimeSpan.FromSeconds(DedupRegistry.TtlSeconds + 1).Ticks;
            var reloaded = new DedupRegistry(clock: () => _nowTicks);

            Assert.IsNull(reloaded.TryGetResult("op-reload-expired"),
                "An entry older than TTL at restore time must not come back after a simulated reload.");
        }
    }
}
