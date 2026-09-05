using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Testing;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for PlaytestParser.ResolveQuery UIDocument 4-segment branch (Variant B)
    /// and PlaytestRunner CLICK UIDocument branch (Phase 2).
    /// </summary>
    [TestFixture]
    public class PlaytestUIToolkitTests : UnityMcpTestBase
    {
        // 1. New feature: 4-segment UIDocument path packs "selector|veField" into field.
        [Test]
        public void ResolveQuery_FourSegments_UIDocument_ReturnsSelectorField()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/HUD|UIDocument|health-label|text", null);
            Assert.That(path,  Is.EqualTo("/HUD"),              "path must keep leading slash");
            Assert.That(comp,  Is.EqualTo("UIDocument"),        "comp must be UIDocument");
            Assert.That(field, Is.EqualTo("health-label|text"), "field packs selector|veField");
        }

        // 2. Existing 3-segment path is not affected by the new UIDocument branch.
        [Test]
        public void ResolveQuery_ThreeSegments_NotUIDocument_Unchanged()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/Player|Health|hp", null);
            Assert.That(path,  Is.EqualTo("/Player"));
            Assert.That(comp,  Is.EqualTo("Health"));
            Assert.That(field, Is.EqualTo("hp"));
        }

        // 3. 4-segment path with parts[1] != "UIDocument" falls through to existing >= 3 branch.
        [Test]
        public void ResolveQuery_FourSegments_NotUIDocument_Unchanged()
        {
            // /A|B|C|D — B != "UIDocument" → existing >= 3 branch fires, D dropped silently
            var (path, comp, field) = PlaytestParser.ResolveQuery("/A|B|C|D", null);
            Assert.That(path,  Is.EqualTo("/A"), "path unchanged");
            Assert.That(comp,  Is.EqualTo("B"),  "comp unchanged");
            Assert.That(field, Is.EqualTo("C"),  "4th segment dropped same as before");
        }

        // 4. Backward-compat regression: well-known 3-segment query unchanged.
        [Test]
        public void ResolveQuery_ExistingThreeSegment_Unaffected()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/Player|Health|hp", null);
            Assert.That(path,  Is.EqualTo("/Player"));
            Assert.That(comp,  Is.EqualTo("Health"));
            Assert.That(field, Is.EqualTo("hp"));
        }

        // ── CLICK UIDocument branch (Phase 2, Session 10) ──────────────────────

        // 5. CLICK UIDocument path: parser stores 3-segment path; Contains("|UIDocument|") is the
        //    discriminator the Steps.cs branch uses to route to UIElementHelper.SimulateClick.
        //    Breaks if the path format changes or Contains() check is removed.
        [Test]
        public void Click_UIDocument_CallsSimulateClick()
        {
            var step = PlaytestParser.Parse("CLICK /HUD|UIDocument|submitBtn").Steps[0];
            Assert.That(step.Type, Is.EqualTo(StepType.Click),           "step type must be Click");
            Assert.That(step.Path, Is.EqualTo("/HUD|UIDocument|submitBtn"), "path stored verbatim");
            Assert.That(step.Path.Contains("|UIDocument|"), Is.True,     "path must contain UIDocument discriminator");
            var parts = step.Path.Split('|');
            Assert.That(parts.Length, Is.EqualTo(3),          "3 segments: goPath|UIDocument|selector");
            Assert.That(parts[0],     Is.EqualTo("/HUD"),      "segment 0 is GO path");
            Assert.That(parts[1],     Is.EqualTo("UIDocument"), "segment 1 is UIDocument discriminator");
            Assert.That(parts[2],     Is.EqualTo("submitBtn"),  "segment 2 is element selector");
        }

        // 6. GO with no UIDocument component: SimulateClick returns false (→ Steps.cs generates FAIL hint).
        //    Breaks if null guard is removed from UIElementHelper.SimulateClick.
        [Test]
        public void Click_UIDocument_MissingDoc_ReturnsNoClick()
        {
            var go = TrackOwnedObject(new GameObject("TestGO_NoUIDoc"));
            bool result = UIElementHelper.SimulateClick(go, "submitBtn");
            Assert.That(result, Is.False,
                "SimulateClick must return false when UIDocument component is missing — " +
                "Steps.cs will report FAIL with inspect_uitk hint");
        }

        // 7. Plain GO path (no |UIDocument|) must NOT be routed through the UIDocument branch.
        //    Breaks if the Contains() check is made too broad.
        [Test]
        public void Click_NonUIDocument_Unchanged()
        {
            var step = PlaytestParser.Parse("CLICK /Player").Steps[0];
            Assert.That(step.Path.Contains("|UIDocument|"), Is.False,
                "plain GO path must not trigger UIDocument branch");
        }

        // ── FILL / FOCUS UIDocument branch (Phase 2, Session 11) ──────────────

        // 8. FILL stores StepType.Fill, full 3-segment path, and value.
        //    Breaks if Fill is not added to StepType or parser.
        [Test]
        public void Fill_UIDocument_SetsStepTypeAndValue()
        {
            var step = PlaytestParser.Parse("FILL /HUD|UIDocument|emailInput hello@x.com").Steps[0];
            Assert.That(step.Type,  Is.EqualTo(StepType.Fill),                     "step type must be Fill");
            Assert.That(step.Path,  Is.EqualTo("/HUD|UIDocument|emailInput"),       "path stored verbatim");
            Assert.That(step.Value, Is.EqualTo("hello@x.com"),                      "value stored in step.Value");
        }

        // 9. FOCUS stores StepType.Focus and full 3-segment path.
        //    Breaks if Focus is not added to StepType or parser.
        [Test]
        public void Focus_UIDocument_SetsStepType()
        {
            var step = PlaytestParser.Parse("FOCUS /HUD|UIDocument|submitBtn").Steps[0];
            Assert.That(step.Type, Is.EqualTo(StepType.Focus),                  "step type must be Focus");
            Assert.That(step.Path, Is.EqualTo("/HUD|UIDocument|submitBtn"),      "path stored verbatim");
        }

        // 10. FILL with a plain (non-UIDocument) path is still parsed as Fill;
        //     Steps.cs handler will produce FAIL/ERR for non-UIDocument paths.
        //     Breaks if parser rejects plain paths or changes step type.
        [Test]
        public void Fill_NonUIDocument_StepTypeIsStillFill()
        {
            var step = PlaytestParser.Parse("FILL /Player/Input hello").Steps[0];
            Assert.That(step.Type,  Is.EqualTo(StepType.Fill),    "parser always sets Fill regardless of path form");
            Assert.That(step.Path,  Is.EqualTo("/Player/Input"),   "path stored verbatim");
            Assert.That(step.Value, Is.EqualTo("hello"),           "value stored");
        }

        // ── Issue #54: NormalizeUIHostPath ─────────────────────────────────────

        // 11. |PanelRenderer| token normalized to |UIDocument| at parse time.
        [Test]
        public void NormalizeUIHostPath_PanelRenderer_NormalizedToUIDocument()
        {
            var result = PlaytestParser.NormalizeUIHostPath("/HUD|PanelRenderer|btn");
            Assert.That(result, Is.EqualTo("/HUD|UIDocument|btn"));
        }

        // 12. |UI| token normalized to |UIDocument| at parse time.
        [Test]
        public void NormalizeUIHostPath_UI_NormalizedToUIDocument()
        {
            var result = PlaytestParser.NormalizeUIHostPath("/HUD|UI|btn");
            Assert.That(result, Is.EqualTo("/HUD|UIDocument|btn"));
        }

        // 13. Plain uGUI paths must not be affected.
        [Test]
        public void NormalizeUIHostPath_PlainPath_Unchanged()
        {
            var result = PlaytestParser.NormalizeUIHostPath("/Player|Health|hp");
            Assert.That(result, Is.EqualTo("/Player|Health|hp"),
                "uGUI paths must not be intercepted by normalization");
        }

        // 14. CLICK with PanelRenderer path: after normalization the step path contains |UIDocument|.
        [Test]
        public void Click_PanelRendererPath_StepPathContainsUIDocumentToken()
        {
            var step = PlaytestParser.Parse("CLICK /HUD|PanelRenderer|btn").Steps[0];
            Assert.That(step.Path, Does.Contain("|UIDocument|"),
                "PanelRenderer token must be normalized to UIDocument");
            Assert.That(step.Path, Does.Not.Contain("|PanelRenderer|"),
                "PanelRenderer token must not remain in stored path");
        }

        // 15. ResolveQuery: 4-segment PanelRenderer path normalizes comp to UIDocument.
        [Test]
        public void ResolveQuery_PanelRendererToken_NormalizesToUIDocument()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/HUD|PanelRenderer|health-label|text", null);
            Assert.That(comp,  Is.EqualTo("UIDocument"), "comp must be normalized to UIDocument");
            Assert.That(field, Is.EqualTo("health-label|text"), "field packs selector|veField");
        }

        // 16. ResolveQuery: 4-segment |UI| alias also normalizes to UIDocument.
        [Test]
        public void ResolveQuery_UIAlias_NormalizesToUIDocument()
        {
            var (path, comp, field) = PlaytestParser.ResolveQuery("/HUD|UI|health-label|text", null);
            Assert.That(comp, Is.EqualTo("UIDocument"), "UI alias must normalize to UIDocument");
        }
    }
}
