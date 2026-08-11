using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Key_IsAgent() =>
            Assert.AreEqual("agent", new AgentChipProvider().Key);

        [Test]
        public void Priority_IsFive() =>
            Assert.AreEqual(5, new AgentChipProvider().Priority);

        [Test]
        public void CanHandle_AlwaysFalse() =>
            Assert.IsFalse(new AgentChipProvider().CanHandle(null, "/any/path"));

        [Test]
        public void FormatPayload_Default()
        {
            var chip = new ChipData(ChipKindKeys.Agent, "senior-developer", "senior-developer", 0);
            var ctx  = new ChipPayloadContext("path", "");
            Assert.AreEqual("[agent:senior-developer]", new AgentChipProvider().FormatPayload(chip, ctx));
        }

        [Test]
        public void FormatPayload_DepthNone_ReturnsEmpty()
        {
            var chip = new ChipData(ChipKindKeys.Agent, "senior-developer", "senior-developer", 0);
            var ctx  = new ChipPayloadContext("none", "");
            Assert.AreEqual("", new AgentChipProvider().FormatPayload(chip, ctx));
        }
    }
}
