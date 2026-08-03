using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class FieldChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        FieldChipProvider _provider;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipKindRegistry.Register(new FieldChipProvider());
            _provider = ChipKindRegistry.ForKey(ChipKindKeys.Field) as FieldChipProvider;
            Assert.IsNotNull(_provider);
            // Inject null override so no scene query happens in unit tests
            FieldChipProvider.FindObjectOverride = _ => null;
        }

        [TearDown]
        public void TearDown()
        {
            FieldChipProvider.FindObjectOverride = null;
            ChipKindRegistry.ResetToBuiltIns();
        }

        // ── Registration ──────────────────────────────────────────────────────

        [Test] public void Key_Is_field()
            => Assert.AreEqual("field", _provider.Key);

        [Test] public void Priority_Is_130()
            => Assert.AreEqual(130, _provider.Priority);

        [Test] public void HexColor_IsAmber()
            => Assert.AreEqual("#f59e0b", _provider.HexColor);

        [Test] public void DefaultDepth_IsSummary()
            => Assert.AreEqual("summary", _provider.DefaultDepth);

        [Test] public void CanHandle_AlwaysFalse()
            => Assert.IsFalse(_provider.CanHandle(null, "any"));

        [Test] public void BarePathExtensions_IsEmpty()
            => Assert.AreEqual(0, _provider.BarePathExtensions.Length);

        // ── FormatPayload depth=none ──────────────────────────────────────────

        [Test]
        public void FormatPayload_DepthNone_ReturnsEmpty()
        {
            var chip   = new ChipData(ChipKindKeys.Field, "Root|Transform|m_localPosition", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("none", ""));
            Assert.AreEqual("", result);
        }

        // ── FormatPayload depth=path ──────────────────────────────────────────

        [Test]
        public void FormatPayload_DepthPath_ReturnsBracketOnly()
        {
            var chip   = new ChipData(ChipKindKeys.Field, "Root|Transform|m_localPosition", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[field:Root|Transform|m_localPosition]", result);
        }

        // ── FormatPayload invalid path ────────────────────────────────────────

        [Test]
        public void FormatPayload_InvalidPath_ReturnsInvalidMessage()
        {
            var chip   = new ChipData(ChipKindKeys.Field, "only_one_segment", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(invalid field path)", result);
        }

        [Test]
        public void FormatPayload_TwoSegmentsOnly_ReturnsInvalidMessage()
        {
            var chip   = new ChipData(ChipKindKeys.Field, "Root|Transform", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(invalid field path)", result);
        }

        // ── FormatPayload missing GO ──────────────────────────────────────────

        [Test]
        public void FormatPayload_GoNotFound_ReturnsObjectNotFound()
        {
            // FindObjectOverride returns null → GO not found
            var chip   = new ChipData(ChipKindKeys.Field, "MissingGO|Rigidbody|mass", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(object not found)", result);
            StringAssert.Contains("[field:MissingGO|Rigidbody|mass]", result);
        }

        // ── Navigate no-throw ─────────────────────────────────────────────────

        [Test]
        public void Navigate_NullGo_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate("Missing|Transform|m_localPosition"));

        [Test]
        public void Navigate_EmptyReference_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate(""));

        [Test]
        public void Navigate_NullReference_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate(null));
    }
}
