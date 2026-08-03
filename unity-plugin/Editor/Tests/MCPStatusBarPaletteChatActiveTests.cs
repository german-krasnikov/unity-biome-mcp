using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.4 — MCPStatusBarPalette ChatActive coverage ─────────────────

    [TestFixture]
    public class MCPStatusBarPaletteChatActiveTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Get_ChatActive_ChipBg_IsOpaque_ProSkin()
        {
            var entry = MCPStatusBarPalette.Get(MCPStatusModel.State.ChatActive, true);
            Assert.AreEqual(1f, entry.ChipBg.a, 1e-4f, "ProSkin ChatActive ChipBg must be opaque");
        }

        [Test]
        public void Get_ChatActive_ChipBg_IsOpaque_LightSkin()
        {
            var entry = MCPStatusBarPalette.Get(MCPStatusModel.State.ChatActive, false);
            Assert.AreEqual(1f, entry.ChipBg.a, 1e-4f, "LightSkin ChatActive ChipBg must be opaque");
        }

        [Test]
        public void Get_ChatActive_HaloAlpha_IsOne_ProSkin()
        {
            var entry = MCPStatusBarPalette.Get(MCPStatusModel.State.ChatActive, true);
            Assert.AreEqual(1f, entry.HaloRgb.a, 1e-4f, "ProSkin ChatActive HaloRgb.a must be 1");
        }

        [Test]
        public void Get_ChatActive_HaloAlpha_IsOne_LightSkin()
        {
            var entry = MCPStatusBarPalette.Get(MCPStatusModel.State.ChatActive, false);
            Assert.AreEqual(1f, entry.HaloRgb.a, 1e-4f, "LightSkin ChatActive HaloRgb.a must be 1");
        }

        [Test]
        public void Get_AllFourStates_ChipBg_IsOpaque()
        {
            var allStates = (MCPStatusModel.State[])Enum.GetValues(typeof(MCPStatusModel.State));
            foreach (var state in allStates)
            {
                Assert.AreEqual(1f, MCPStatusBarPalette.Get(state, true).ChipBg.a,  1e-4f, $"ProSkin {state}");
                Assert.AreEqual(1f, MCPStatusBarPalette.Get(state, false).ChipBg.a, 1e-4f, $"LightSkin {state}");
            }
        }
    }
}
