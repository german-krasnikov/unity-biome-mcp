// TDD (PR-05 05.3): ProviderUnavailableException classification and SearchHelper.SearchContext
// no-provider behavior. Red->Green: ProviderUnavailableException must be classified as
// "UNAVAILABLE" (an expected "Chat.CLI not loaded" condition), not the generic "STATE" that a
// plain InvalidOperationException gets — precedent: StaleCacheErrorTests.cs.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProviderUnavailableErrorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ErrorClassifier ───────────────────────────────────────────────────

        [Test]
        public void ProviderUnavailableException_ClassifiedAsUnavailable()
        {
            var ex = new ProviderUnavailableException("search_context unavailable: x");
            Assert.AreEqual("UNAVAILABLE", ErrorClassifier.Classify(ex));
        }

        [Test]
        public void InvalidOperationException_StillMapsToState()
        {
            // Regression guard: regular InvalidOperationException must NOT become UNAVAILABLE.
            var ex = new System.InvalidOperationException("bad state");
            Assert.AreEqual("STATE", ErrorClassifier.Classify(ex));
        }

        // ── SearchHelper.SearchContext ────────────────────────────────────────

        [Test]
        public void SearchContext_ProviderNull_ThrowsProviderUnavailable_NotGenericState()
        {
            var previous = SearchHelper.SearchContextProvider;
            RegisterCleanup(() => SearchHelper.SearchContextProvider = previous);
            SearchHelper.SearchContextProvider = null;

            // Litmus: reverting the SearchHelper.cs throw-type change turns this red even
            // though the base-type assert below would still pass.
            Assert.Throws<ProviderUnavailableException>(() => SearchHelper.SearchContext());
        }
    }
}
