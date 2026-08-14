// TDD: ProfilerChipProvider — chip kind metadata and FormatPayload.
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ProfilerChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Key_IsProfile() =>
            Assert.AreEqual("profile", new ProfilerChipProvider().Key);

        [Test]
        public void CanHandle_AlwaysReturnsFalse() =>
            Assert.IsFalse(new ProfilerChipProvider().CanHandle(null, "p1"));

        [Test]
        public void FormatPayload_CallsSerializer_ReturnsStatsText()
        {
#if UNITY_INCLUDE_TESTS
            ProfileContextSerializer.GetOverride = sid => $"session:{sid} 5.0s 300frames\nfps avg=60.0";
            RegisterCleanup(() => ProfileContextSerializer.GetOverride = null);
#endif
            var chip = new ChipData(ChipKindKeys.Profile, "p1", "Profile: p1", "");
            var ctx  = new ChipPayloadContext("full", "");
            string result = new ProfilerChipProvider().FormatPayload(chip, ctx);
            StringAssert.Contains("fps avg=60.0", result);
        }

        [Test]
        public void FormatPayload_SessionNotFound_ReturnsErrorString()
        {
#if UNITY_INCLUDE_TESTS
            ProfileContextSerializer.GetOverride = sid => $"error: session not found: {sid}";
            RegisterCleanup(() => ProfileContextSerializer.GetOverride = null);
#endif
            var chip = new ChipData(ChipKindKeys.Profile, "x999", "Profile: x999", "");
            var ctx  = new ChipPayloadContext("full", "");
            string result = new ProfilerChipProvider().FormatPayload(chip, ctx);
            StringAssert.StartsWith("error:", result);
        }
    }
}
