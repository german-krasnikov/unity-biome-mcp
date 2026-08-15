using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests for PlaytestParser.ResolveQuery UIDocument 4-segment branch (Variant B).
    /// Covers: new UIDocument routing, backward compatibility, non-UIDocument 4-segment paths.
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
    }
}
