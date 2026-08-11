// E2E test suite for ChipKindRegistry — Part 2: pipeline, symmetry, reload re-bind.
// Tests (c), (d), (e), (n) from the F11 build brief TEST PLAN.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipKindRegistryPipelineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(ChipKindRegistry.PreserveStateForTests().Dispose);
            ChipKindRegistry.ResetForTests();
        }

        // (c) Full pipeline: ResolveAllTyped → FormatPayload emits [custom_widget:...]
        [Test]
        public void Pipeline_ResolveAllTyped_EmitsCustomKindBracket()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var chips = new List<ChipData>
            {
                new ChipData("custom_widget", "Assets/w.fbx", "Widget", 0)
            };
            var result = ChipContextResolver.ResolveAllTyped(chips, new ChipConfig());
            StringAssert.Contains("[custom_widget:Assets/w.fbx]", result);
        }

        // (d) ResponseTagInliner renders custom color + chip:custom_widget: linkId
        [Test]
        public void ResponseTagInliner_CustomKind_CustomColor()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var result = ResponseTagInliner.Apply("[custom_widget:Assets/w.fbx]");
            StringAssert.Contains("#ff00ff", result);
            StringAssert.Contains("chip:custom_widget:Assets/w.fbx", result);
            StringAssert.DoesNotContain("[custom_widget:Assets/w.fbx]", result);
        }

        // (e) SYMMETRY: send-path payload == show-as-text payload byte-for-byte
        [Test]
        public void Symmetry_EmitTyped_EqualsFormatPayloadDirect()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var chip = new ChipData("custom_widget", "Assets/w.fbx", "Widget", 0);
            var cfg  = new ChipConfig();

            var sendPath = ChipContextResolver.ResolveAllTyped(new List<ChipData> { chip }, cfg);

            var provider = ChipKindRegistry.ForKey("custom_widget");
            var depth    = cfg.DepthFor("custom_widget");
            var ctx      = new ChipPayloadContext(depth, "");
            var showPath = provider.FormatPayload(chip, ctx);

            Assert.AreEqual(showPath, sendPath, "send-path and show-as-text must be byte-for-byte equal");
        }

        // (n) PendingTurnState v4 roundtrip preserves KindKey
        [Test]
        public void PendingTurnState_V4_RoundTrip_KindKeyPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: "s", pendingText: "t",
                chipPaths: new[] { "Assets/w.fbx" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { "custom_widget" });

            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(1,               rt.Value.KindKeys.Length);
            Assert.AreEqual("custom_widget", rt.Value.KindKeys[0]);
        }

        // (n) Back-compat: v3 data → empty KindKey → re-detect
        [Test]
        public void PendingTurnState_V3_BackCompat_EmptyKindKey()
        {
            var pathB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Assets/foo.fbx"));
            var textB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            var raw     = $"sess|{textB64}|0||{actB64}|1|5|100|0\n{pathB64}";
            var rt = PendingTurnState.Deserialize(raw);
            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo.fbx", rt.Value.ChipPaths[0]);
            Assert.AreEqual("",               rt.Value.KindKeys[0]);
        }

        // H14b: "Show LLM payload" menu and send path share the same seam (ResolveAllTyped).
        // Verify a built-in Script chip produces identical bytes from menu reveal vs the
        // underlying provider.FormatPayload path (two distinct code paths must converge).
        [Test]
        public void Symmetry_MenuReveal_MatchesSendPath_ForScriptChip()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            var cfg  = new ChipConfig();

            // Send/menu-reveal path: aggregator pipeline
            var menuPayload = ChipContextResolver.ResolveAllTyped(new List<ChipData> { chip }, cfg);

            // Provider path: invoke provider.FormatPayload directly (the underlying seam)
            var provider = ChipKindRegistry.ForKey(ChipKindKeys.Script);
            Assert.IsNotNull(provider, "Script provider must be registered");
            var depth    = cfg.DepthFor(ChipKindKeys.Script);
            var ctx      = new ChipPayloadContext(depth, "");
            var sendPayload = provider.FormatPayload(chip, ctx);

            Assert.AreEqual(sendPayload, menuPayload,
                "menu reveal (ResolveAllTyped) and provider.FormatPayload must converge");
            StringAssert.Contains("[script:Assets/Foo.cs]", menuPayload,
                "script chip must emit [script:path] bracket with no extra wrapping");
            Assert.AreEqual("[script:Assets/Foo.cs]", menuPayload.Trim());
        }

        // Version increments on register/unregister
        [Test]
        public void Version_IncrementsOnRegisterAndUnregister()
        {
            var v0 = ChipKindRegistry.Version;
            ChipKindRegistry.Register(new FakeProvider());
            Assert.Greater(ChipKindRegistry.Version, v0);

            var v1 = ChipKindRegistry.Version;
            ChipKindRegistry.Unregister("custom_widget");
            Assert.Greater(ChipKindRegistry.Version, v1);
        }

        // ForKey returns null for unknown key
        [Test]
        public void ForKey_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(ChipKindRegistry.ForKey("definitely_not_a_key"));
        }

        // Invalid key rejected
        [Test]
        public void Register_InvalidKey_ReturnsFalse()
        {
            var bad = new BadKeyProvider();
            var result = ChipKindRegistry.Register(bad);
            Assert.IsFalse(result);
            Assert.IsNull(ChipKindRegistry.ForKey("INVALID KEY!"));
        }

        [Test]
        public void PreserveStateForTests_RestoresProviderIdentityAndVersionExactly()
        {
            var provider = new FakeProvider();
            ChipKindRegistry.Register(provider);
            var version = ChipKindRegistry.Version;

            using (ChipKindRegistry.PreserveStateForTests())
            {
                ChipKindRegistry.ResetForTests();
                Assert.IsNull(ChipKindRegistry.ForKey(provider.Key));
            }

            Assert.AreEqual(version, ChipKindRegistry.Version);
            Assert.AreSame(provider, ChipKindRegistry.ForKey(provider.Key));
        }

        private sealed class BadKeyProvider : IChipKindProvider
        {
            public string Key          => "INVALID KEY!";
            public int    Priority     => 10;
            public string IconName     => "";
            public string HexColor     => "#000";
            public string DefaultDepth => "path";
            public string[] BarePathExtensions => System.Array.Empty<string>();
            public bool   CanHandle(UnityEngine.Object o, string p) => false;
            public ChipData Create(UnityEngine.Object o, string p) => default;
            public string FormatPayload(ChipData c, ChipPayloadContext x) => "";
            public void   Navigate(string r) { }
            public void   Ping(string r) { }
            public void   AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string r) { }
        }
    }
}
