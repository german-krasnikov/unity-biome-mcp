using NUnit.Framework;
using UnityMCP.Editor;
using static UnityMCP.Editor.MCPStatusModel;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BiomeMenuPathTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void PinTextMode() => SetEditorPrefBool("MCPPlugin_UseEmojiLabel", false);

        // ── Status bar pill no longer says "MCP" ────────────────────────────

        [Test]
        public void GetPill_Down_DoesNotContainMcp()
        {
            var text = GetPill(State.Down, 9500);
            StringAssert.DoesNotContain("MCP", text);
        }

        [Test]
        public void GetPill_Listen_DoesNotContainMcp()
        {
            var text = GetPill(State.Listen, 9500);
            StringAssert.DoesNotContain("MCP", text);
        }

        [Test]
        public void GetPill_Up_DoesNotContainMcp()
        {
            var text = GetPill(State.Up, 9500);
            StringAssert.DoesNotContain("MCP", text);
        }

        [Test]
        public void GetPill_ChatActive_DoesNotContainMcp()
        {
            var text = GetPill(State.ChatActive, 9500);
            StringAssert.DoesNotContain("MCP", text);
        }

        // ── Pill uses "Biome" prefix ─────────────────────────────────────────

        [Test]
        public void GetPill_Down_ContainsBiome()
            => StringAssert.Contains("Biome", GetPill(State.Down, 9500));

        [Test]
        public void GetPill_Up_ContainsBiome()
            => StringAssert.Contains("Biome", GetPill(State.Up, 9500));
    }
}
