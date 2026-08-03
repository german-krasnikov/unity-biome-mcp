// NUnit tests for DefaultStripper — token-saving default-value removal.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DefaultStripperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Build a two-field snippet so we can verify removal + retention separately.
        private static string Line(string kv) => $"[C]\n{kv}\nother: keep\n";

        private static void AssertStripped(string kv) =>
            StringAssert.DoesNotContain(kv, DefaultStripper.Strip(Line(kv)));

        private static void AssertKept(string kv) =>
            StringAssert.Contains(kv, DefaultStripper.Strip(Line(kv)));

        // ── P0: Global defaults ────────────────────────────────────────────────

        [Test] public void Strip_RemovesZero()        => AssertStripped("speed: 0");
        [Test] public void Strip_RemovesZeroFloat()   => AssertStripped("speed: 0.0");
        [Test] public void Strip_RemovesFalse()       => AssertStripped("isKinematic: false");
        [Test] public void Strip_RemovesNull()        => AssertStripped("target: null");
        [Test] public void Strip_RemovesNone()        => AssertStripped("parent: None");
        [Test] public void Strip_RemovesEmptyString() => AssertStripped("tag: \"\"");
        [Test] public void Strip_RemovesZeroVector3()      => AssertStripped("pos: (0, 0, 0)");
        [Test] public void Strip_RemovesZeroVector3Float() => AssertStripped("pos: (0.0, 0.0, 0.0)");
        [Test] public void Strip_RemovesIdentityQuat()      => AssertStripped("rot: (0, 0, 0, 1)");
        [Test] public void Strip_RemovesIdentityQuatFloat() => AssertStripped("rot: (0.0, 0.0, 0.0, 1.0)");
        [Test] public void Strip_RemovesUnitScale()      => AssertStripped("scale: (1, 1, 1)");
        [Test] public void Strip_RemovesUnitScaleFloat() => AssertStripped("scale: (1.0, 1.0, 1.0)");
        [Test] public void Strip_RemovesEmptyList()         => AssertStripped("events: []");
        [Test] public void Strip_RemovesTransparentColor()  => AssertStripped("color: #00000000");
        [Test] public void Strip_RemovesWhiteColor()        => AssertStripped("color: #FFFFFFFF");
        [Test] public void Strip_RemovesUntagged()          => AssertStripped("tag: Untagged");
        [Test] public void Strip_RemovesDefaultLayerName()  => AssertStripped("layer: Default");
        [Test] public void Strip_RemovesZeroVector2()  => AssertStripped("offset: (0, 0)");
        [Test] public void Strip_RemovesZeroVector4()  => AssertStripped("pad: (0, 0, 0, 0)");

        // ── P0: Field-specific defaults ────────────────────────────────────────

        [Test] public void Strip_RemovesM_MassOne()      => AssertStripped("m_mass: 1");
        [Test] public void Strip_RemovesM_MassOneFloat() => AssertStripped("m_mass: 1.0");

        [Test]
        public void Strip_KeepsMassOne_UserField()
        {
            // "mass" key is NOT in FieldDefaults; "1" is not a global default
            AssertKept("mass: 1");
        }

        [Test]
        public void Strip_KeepsHealthOne() => AssertKept("health: 1");

        [Test] public void Strip_RemovesM_LayerZero() => AssertStripped("m_layer: 0");

        [Test] public void Strip_RemovesM_IsStaticFalse() => AssertStripped("m_isstatic: false");

        [Test]
        public void Strip_RemovesM_IsStaticFalsePascal()
        {
            // "False" (Pascal) is in FieldDefaults["m_isstatic"] but NOT in global Defaults
            AssertStripped("m_isstatic: False");
        }

        // ── P0: Structural preservation ────────────────────────────────────────

        [Test]
        public void Strip_KeepsHeaders()
        {
            var r = DefaultStripper.Strip("[Transform]\nspeed: 0\n");
            StringAssert.Contains("[Transform]", r);
        }

        [Test]
        public void Strip_KeepsSeparators()
        {
            var r = DefaultStripper.Strip("[C]\nspeed: 0\n---\n");
            StringAssert.Contains("---", r);
        }

        [Test]
        public void Strip_KeepsMultiObjectSeparator()
        {
            var r = DefaultStripper.Strip("[C]\nspeed: 0\n--- /Player ---\n");
            StringAssert.Contains("--- /Player ---", r);
        }

        [Test]
        public void Strip_KeepsErrorLines()
        {
            var r = DefaultStripper.Strip("[C]\nerr: something\nspeed: 0\n");
            StringAssert.Contains("err: something", r);
        }

        [Test]
        public void Strip_KeepsBlankLines()
        {
            var r = DefaultStripper.Strip("[C]\n\nspeed: 5\n");
            StringAssert.Contains("\n\n", r);
        }

        // ── P0: Special characters ─────────────────────────────────────────────

        [Test]
        public void Strip_SpecialChars_BracketsInValue()
        {
            var r = DefaultStripper.Strip("[C]\nm_Name: Object [1]\nspeed: 0\n");
            StringAssert.Contains("m_Name: Object [1]", r);
            StringAssert.DoesNotContain("speed: 0", r);
        }

        [Test]
        public void Strip_SpecialChars_CurlyBracesInValue()
        {
            // "{}" is not in Defaults — kept; "speed: 0" stripped
            var r = DefaultStripper.Strip("[C]\ndata: {}\nspeed: 0\n");
            StringAssert.Contains("data: {}", r);
            StringAssert.DoesNotContain("speed: 0", r);
        }

        [Test]
        public void Strip_SpecialChars_AngleBracketsInValue()
        {
            var r = DefaultStripper.Strip("[C]\ntype: List<int>\nspeed: 0\n");
            StringAssert.Contains("type: List<int>", r);
            StringAssert.DoesNotContain("speed: 0", r);
        }

        [Test]
        public void Strip_SpecialChars_QuotesInValue()
        {
            // "Player" is not the empty-string default → kept
            var r = DefaultStripper.Strip("[C]\nm_Name: \"Player\"\nspeed: 0\n");
            StringAssert.Contains("m_Name: \"Player\"", r);
        }

        [Test]
        public void Strip_SpecialChars_EmptyQuotesStripped()
        {
            // "" IS the empty-string default → stripped; speed:5 is kept
            var r = DefaultStripper.Strip("[C]\nm_Name: \"\"\nspeed: 5\n");
            StringAssert.DoesNotContain("m_Name: \"\"", r);
            StringAssert.Contains("speed: 5", r);
        }

        // ── P1: Edge cases ─────────────────────────────────────────────────────

        [Test] public void Strip_NullReturnsNull()   => Assert.IsNull(DefaultStripper.Strip(null));
        [Test] public void Strip_EmptyReturnsEmpty() => Assert.AreEqual("", DefaultStripper.Strip(""));

        [Test]
        public void Strip_LineWithoutColon_Kept()
        {
            // No ": " separator → ShouldStrip returns false → line kept
            var r = DefaultStripper.Strip("[C]\nTransform\nspeed: 0\n");
            StringAssert.Contains("Transform", r);
        }

        [Test] public void Strip_KeepsNonDefaultValue() => AssertKept("speed: 5");
    }
}
