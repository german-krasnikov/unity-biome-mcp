using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ComponentChipProviderTests
    {
        ComponentChipProvider _provider;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _provider = ChipKindRegistry.ForKey(ChipKindKeys.Component) as ComponentChipProvider;
            Assert.IsNotNull(_provider);
            ComponentChipProvider.FindObjectOverride = _ => null;
        }

        [TearDown]
        public void TearDown()
        {
            ComponentChipProvider.FindObjectOverride = null;
            ChipKindRegistry.ResetToBuiltIns();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        // ── Registration ──────────────────────────────────────────────────────

        [Test] public void Key_Is_component()
            => Assert.AreEqual("component", _provider.Key);

        [Test] public void Priority_Is_125()
            => Assert.AreEqual(125, _provider.Priority);

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
            var chip   = new ChipData(ChipKindKeys.Component, "Root|Transform", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("none", ""));
            Assert.AreEqual("", result);
        }

        // ── FormatPayload depth=path ──────────────────────────────────────────

        [Test]
        public void FormatPayload_DepthPath_ReturnsBracketOnly()
        {
            var chip   = new ChipData(ChipKindKeys.Component, "Root|Transform", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[component:Root|Transform]", result);
        }

        // ── FormatPayload invalid path ────────────────────────────────────────

        [Test]
        public void FormatPayload_InvalidPath_OneSegment_ReturnsInvalidMessage()
        {
            var chip   = new ChipData(ChipKindKeys.Component, "only_one_segment", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(invalid component path)", result);
        }

        // ── FormatPayload object not found ────────────────────────────────────

        [Test]
        public void FormatPayload_DepthSummary_ObjectNotFound()
        {
            // FindObjectOverride returns null → GO not found
            var chip   = new ChipData(ChipKindKeys.Component, "MissingGO|Rigidbody", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("[component:MissingGO|Rigidbody]", result);
            StringAssert.Contains("(object not found)", result);
        }

        // ── FormatPayload with real Component (summary) ───────────────────────

        [Test]
        public void FormatPayload_DepthSummary_RealRigidbody_ContainsFields()
        {
            var go = new GameObject("TestGO");
            go.AddComponent<Rigidbody>();
            ComponentChipProvider.FindObjectOverride = path => path == "TestGO" ? go : null;

            var chip   = new ChipData(ChipKindKeys.Component, "TestGO|Rigidbody", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));

            StringAssert.Contains("[component:TestGO|Rigidbody]", result);
            // Result must contain at least one field (newline + name=value)
            StringAssert.Contains("\n", result);

            ComponentChipProvider.FindObjectOverride = _ => null;
            Object.DestroyImmediate(go);
        }

        // ── FormatPayload with real Component (full) ──────────────────────────

        [Test]
        public void FormatPayload_DepthFull_RealRigidbody_BracketPresent()
        {
            var go = new GameObject("TestGO2");
            go.AddComponent<Rigidbody>();
            ComponentChipProvider.FindObjectOverride = path => path == "TestGO2" ? go : null;

            var chip   = new ChipData(ChipKindKeys.Component, "TestGO2|Rigidbody", "label", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("full", ""));

            StringAssert.Contains("[component:TestGO2|Rigidbody]", result);

            ComponentChipProvider.FindObjectOverride = _ => null;
            Object.DestroyImmediate(go);
        }

        // ── Navigate ──────────────────────────────────────────────────────────

        [Test]
        public void Navigate_NullGo_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate("Missing|Transform"));

        [Test]
        public void Navigate_EmptyReference_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate(""));

        [Test]
        public void Navigate_NullReference_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate(null));
    }
}
