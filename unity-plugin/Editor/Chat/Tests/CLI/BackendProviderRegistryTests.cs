// TDD: BackendProviderRegistry — discovery, sorting, Get, KindToId.
// Uses Override seam (TypeCache is Unity-only; tests run in NUnit EditMode).
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendProviderRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Minimal stub — no binary check needed for registry unit tests.
        private sealed class StubProvider : IBackendProvider
        {
            public string ProviderId  { get; }
            public string BinaryName  { get; }
            public string DisplayName { get; }
            public int    SortOrder   { get; }
            public StubProvider(string id, string display, int sort)
            { ProviderId = id; BinaryName = id; DisplayName = display; SortOrder = sort; }
            public IChatBackend Create(BackendCreateArgs a) => null;
        }

        [SetUp]
        public void SetUp() => BackendProviderRegistry.ResetForTests();

        [TearDown]
        public void TearDown() => BackendProviderRegistry.ResetForTests();

        // ── All returns Override when set ─────────────────────────────────────

        [Test]
        public void All_WithOverride_ReturnsInjectedProviders()
        {
            BackendProviderRegistry.Override = new List<IBackendProvider>
            {
                new StubProvider("claude", "Claude", 0),
                new StubProvider("codex",  "Codex",  10),
            };

            Assert.AreEqual(2, BackendProviderRegistry.All.Count);
        }

        // ── Get returns provider by ProviderId ────────────────────────────────

        [Test]
        public void Get_ExistingId_ReturnsProvider()
        {
            BackendProviderRegistry.Override = new List<IBackendProvider>
            {
                new StubProvider("claude", "Claude", 0),
            };

            var p = BackendProviderRegistry.Get("claude");
            Assert.IsNotNull(p);
            Assert.AreEqual("Claude", p.DisplayName);
        }

        [Test]
        public void Get_MissingId_ReturnsNull()
        {
            BackendProviderRegistry.Override = new List<IBackendProvider>
            {
                new StubProvider("claude", "Claude", 0),
            };

            Assert.IsNull(BackendProviderRegistry.Get("gemini"));
        }

        // ── KindToId maps enum correctly ──────────────────────────────────────

        [Test]
        public void KindToId_Claude_ReturnsClaude()
            => Assert.AreEqual("claude", BackendProviderRegistry.KindToId(BackendKind.Claude));

        [Test]
        public void KindToId_Codex_ReturnsCodex()
            => Assert.AreEqual("codex", BackendProviderRegistry.KindToId(BackendKind.Codex));

        // ── Get is case-sensitive ─────────────────────────────────────────────

        [Test]
        public void Get_WrongCase_ReturnsNull()
        {
            BackendProviderRegistry.Override = new List<IBackendProvider>
            {
                new StubProvider("claude", "Claude", 0),
            };

            Assert.IsNull(BackendProviderRegistry.Get("Claude"));
        }

        // ── Empty Override list ───────────────────────────────────────────────

        [Test]
        public void All_EmptyOverride_ReturnsEmpty()
        {
            BackendProviderRegistry.Override = new List<IBackendProvider>();
            Assert.AreEqual(0, BackendProviderRegistry.All.Count);
        }
    }
}
