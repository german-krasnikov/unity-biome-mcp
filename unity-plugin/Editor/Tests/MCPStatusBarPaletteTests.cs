using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPStatusBarPaletteTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Get_ThreeStates_HaveDistinctChipBgColors_ProSkin()
        {
            var up     = MCPStatusBarPalette.Get(MCPStatusModel.State.Up,     true);
            var listen = MCPStatusBarPalette.Get(MCPStatusModel.State.Listen, true);
            var down   = MCPStatusBarPalette.Get(MCPStatusModel.State.Down,   true);

            Assert.AreNotEqual(up.ChipBg, listen.ChipBg);
            Assert.AreNotEqual(up.ChipBg, down.ChipBg);
            Assert.AreNotEqual(listen.ChipBg, down.ChipBg);
        }

        [Test]
        public void Get_ThreeStates_HaveDistinctChipBgColors_LightSkin()
        {
            var up     = MCPStatusBarPalette.Get(MCPStatusModel.State.Up,     false);
            var listen = MCPStatusBarPalette.Get(MCPStatusModel.State.Listen, false);
            var down   = MCPStatusBarPalette.Get(MCPStatusModel.State.Down,   false);

            Assert.AreNotEqual(up.ChipBg, listen.ChipBg);
            Assert.AreNotEqual(up.ChipBg, down.ChipBg);
            Assert.AreNotEqual(listen.ChipBg, down.ChipBg);
        }

        [Test]
        public void Get_Down_HaloAlphaIsZero()
        {
            // DOWN spec: halo alpha = 0 (invisible) for both skins
            var pro   = MCPStatusBarPalette.Get(MCPStatusModel.State.Down, true);
            var light = MCPStatusBarPalette.Get(MCPStatusModel.State.Down, false);

            Assert.AreEqual(0f, pro.HaloRgb.a,   1e-4f);
            Assert.AreEqual(0f, light.HaloRgb.a, 1e-4f);
        }

        [Test]
        public void Get_UpAndListen_HaloAlphaIsOne()
        {
            // UP and LISTEN: halo rgb stored with alpha=1 (tick controls runtime alpha)
            Assert.AreEqual(1f, MCPStatusBarPalette.Get(MCPStatusModel.State.Up,     true).HaloRgb.a, 1e-4f);
            Assert.AreEqual(1f, MCPStatusBarPalette.Get(MCPStatusModel.State.Listen, true).HaloRgb.a, 1e-4f);
        }

        [Test]
        public void Get_ChipBg_IsOpaque()
        {
            // Opaque plate is the legibility fix — alpha must be 1.0
            foreach (var state in new[] { MCPStatusModel.State.Up, MCPStatusModel.State.Listen, MCPStatusModel.State.Down })
            {
                Assert.AreEqual(1f, MCPStatusBarPalette.Get(state, true).ChipBg.a,  1e-4f, $"ProSkin {state}");
                Assert.AreEqual(1f, MCPStatusBarPalette.Get(state, false).ChipBg.a, 1e-4f, $"LightSkin {state}");
            }
        }
    }
}
