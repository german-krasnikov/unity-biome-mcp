using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchProviderSlotTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeProvider : ISourcePatchProvider
        {
            public SourcePatchApplyOutcome Apply(SourcePatchRequest request) => SourcePatchApplyOutcome.Applied;
        }

        [SetUp]
        public void SetUpProviderSlotIsolation()
        {
            RegisterCleanup(SourcePatchProviderSlot.ResetForTests);
            SourcePatchProviderSlot.ResetForTests();
        }

        [Test]
        public void TryGet_NoRegistration_ReturnsFalse()
        {
            var found = SourcePatchProviderSlot.TryGet(out var provider);

            Assert.IsFalse(found);
            Assert.IsNull(provider);
        }

        [Test]
        public void Register_FirstRegistration_ReturnsRegisteredAndStoresProvider()
        {
            var provider = new FakeProvider();

            var result = SourcePatchProviderSlot.Register("provider-a", provider);

            Assert.AreEqual(SourcePatchRegistrationResult.Registered, result);
            SourcePatchProviderSlot.TryGet(out var stored);
            Assert.AreSame(provider, stored);
        }

        [Test]
        public void Register_SameIdTwice_ReturnsAlreadyRegisteredAndKeepsOriginalInstance()
        {
            var first = new FakeProvider();
            var second = new FakeProvider();
            SourcePatchProviderSlot.Register("provider-a", first);

            var result = SourcePatchProviderSlot.Register("provider-a", second);

            Assert.AreEqual(SourcePatchRegistrationResult.AlreadyRegistered, result);
            SourcePatchProviderSlot.TryGet(out var stored);
            Assert.AreSame(first, stored);
        }

        [Test]
        public void Register_DifferentIdWhileOccupied_ReturnsConflictAndKeepsOriginalInstance()
        {
            var first = new FakeProvider();
            var second = new FakeProvider();
            SourcePatchProviderSlot.Register("provider-a", first);

            var result = SourcePatchProviderSlot.Register("provider-b", second);

            Assert.AreEqual(SourcePatchRegistrationResult.Conflict, result);
            SourcePatchProviderSlot.TryGet(out var stored);
            Assert.AreSame(first, stored);
        }

        [Test]
        public void Register_NullProviderId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => SourcePatchProviderSlot.Register(null, new FakeProvider()));
        }

        [Test]
        public void Register_NullProvider_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => SourcePatchProviderSlot.Register("provider-a", null));
        }
    }
}
