// NUnit tests for FieldProjector — field projection / alias resolution.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FieldProjectorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string S =
            "[Transform]\nm_LocalPosition.x: 5\nm_LocalPosition.y: 3\n" +
            "m_LocalRotation.x: 0\nm_LocalRotation.y: 0\n" +
            "m_LocalScale.x: 1\nm_LocalScale.y: 1\n---\n" +
            "[Rigidbody]\nm_Mass: 2\nm_Drag: 0\nm_Enabled: true\n" +
            "m_IsActive: true\nm_Name: Player\nm_TagString: Untagged\nm_Layer: 0\n";

        // ── P0: Core projection ────────────────────────────────────────────────

        [Test]
        public void Project_ExactMatch_KeepsOnlyMatchedKey()
        {
            var r = FieldProjector.Project(S, "m_Mass");
            StringAssert.Contains("m_Mass: 2", r);
            StringAssert.DoesNotContain("m_Drag", r);
        }

        [Test]
        public void Project_CaseInsensitive_Matches()
        {
            var r = FieldProjector.Project(S, "M_MASS");
            StringAssert.Contains("m_Mass: 2", r);
        }

        [Test]
        public void Project_DottedPrefix_KeepsSubfields()
        {
            var r = FieldProjector.Project(S, "m_LocalPosition");
            StringAssert.Contains("m_LocalPosition.x: 5", r);
            StringAssert.Contains("m_LocalPosition.y: 3", r);
            StringAssert.DoesNotContain("m_Mass", r);
        }

        [Test]
        public void Project_ExactSubfield_KeepsOnlyThatDot()
        {
            var r = FieldProjector.Project(S, "m_LocalPosition.x");
            StringAssert.Contains("m_LocalPosition.x: 5", r);
            StringAssert.DoesNotContain("m_LocalPosition.y", r);
        }

        [Test]
        public void Project_PrefixRequiresDotBoundary_NoLooseSubstring()
        {
            var input = "[C]\nposition: (5,0,0)\npos: 1\n";
            var r = FieldProjector.Project(input, "pos");
            StringAssert.Contains("pos: 1", r);
            StringAssert.DoesNotContain("position:", r);
        }

        // ── P0: ALL 12 aliases ─────────────────────────────────────────────────

        [Test] public void Project_Alias_Position() =>
            StringAssert.Contains("m_LocalPosition.x: 5", FieldProjector.Project(S, "position"));

        [Test] public void Project_Alias_LocalPosition() =>
            StringAssert.Contains("m_LocalPosition.x: 5", FieldProjector.Project(S, "localposition"));

        [Test] public void Project_Alias_Rotation() =>
            StringAssert.Contains("m_LocalRotation.x: 0", FieldProjector.Project(S, "rotation"));

        [Test] public void Project_Alias_Scale() =>
            StringAssert.Contains("m_LocalScale.x: 1", FieldProjector.Project(S, "scale"));

        [Test] public void Project_Alias_Mass() =>
            StringAssert.Contains("m_Mass: 2", FieldProjector.Project(S, "mass"));

        [Test] public void Project_Alias_Enabled() =>
            StringAssert.Contains("m_Enabled: true", FieldProjector.Project(S, "enabled"));

        [Test] public void Project_Alias_Active() =>
            StringAssert.Contains("m_IsActive: true", FieldProjector.Project(S, "active"));

        [Test] public void Project_Alias_Name() =>
            StringAssert.Contains("m_Name: Player", FieldProjector.Project(S, "name"));

        [Test] public void Project_Alias_Tag() =>
            StringAssert.Contains("m_TagString: Untagged", FieldProjector.Project(S, "tag"));

        [Test] public void Project_Alias_Layer() =>
            StringAssert.Contains("m_Layer: 0", FieldProjector.Project(S, "layer"));

        [Test] public void Project_Alias_CaseInsensitive() =>
            StringAssert.Contains("m_LocalPosition.x: 5", FieldProjector.Project(S, "POSITION"));

        [Test]
        public void Project_MixedAliasAndCanonical()
        {
            var r = FieldProjector.Project(S, "position,m_Mass");
            StringAssert.Contains("m_LocalPosition.x: 5", r);
            StringAssert.Contains("m_Mass: 2", r);
        }

        // ── P0: Structural preservation ────────────────────────────────────────

        [Test]
        public void Project_AlwaysKeepsHeaders()
        {
            var r = FieldProjector.Project(S, "m_Mass");
            StringAssert.Contains("[Transform]", r);
            StringAssert.Contains("[Rigidbody]", r);
        }

        [Test]
        public void Project_AlwaysKeepsSeparators()
        {
            var r = FieldProjector.Project(S, "m_Mass");
            StringAssert.Contains("---", r);
        }

        [Test]
        public void Project_AlwaysKeepsMultiObjectSeparator()
        {
            var input = "[C]\nm_Mass: 2\n--- /Player ---\n[C]\nm_Mass: 3\n";
            var r = FieldProjector.Project(input, "m_Mass");
            StringAssert.Contains("--- /Player ---", r);
        }

        [Test]
        public void Project_AlwaysKeepsErrorLines()
        {
            var input = "[C]\nerr: not found\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Mass");
            StringAssert.Contains("err: not found", r);
        }

        [Test]
        public void Project_AlwaysKeepsBlankLines()
        {
            var input = "[C]\n\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Mass");
            StringAssert.Contains("\n\n", r);
        }

        // ── P0: Special characters ─────────────────────────────────────────────

        [Test]
        public void Project_SpecialChars_SquareBracketsInValue()
        {
            var input = "[Transform]\nm_Name: Object [1]\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Name");
            StringAssert.Contains("m_Name: Object [1]", r);
            StringAssert.DoesNotContain("m_Mass", r);
        }

        [Test]
        public void Project_SpecialChars_CurlyBracesInValue()
        {
            var input = "[Script]\ndata: {\"key\": \"val\"}\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "data");
            StringAssert.Contains("data: {\"key\": \"val\"}", r);
            StringAssert.DoesNotContain("m_Mass", r);
        }

        [Test]
        public void Project_SpecialChars_AngleBracketsInValue()
        {
            var input = "[Script]\ngenericType: List<int>\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "genericType");
            StringAssert.Contains("genericType: List<int>", r);
        }

        [Test]
        public void Project_SpecialChars_BracketsInFieldName()
        {
            var input = "[Transform]\nm_Children[0]: /Child\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Children[0]");
            StringAssert.Contains("m_Children[0]: /Child", r);
        }

        [Test]
        public void Project_SpecialChars_HeaderLikeLine_NotConfused()
        {
            // Lines starting with [ are always structural — never filtered out
            var input = "[Transform]\n[0]: first item\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Mass");
            StringAssert.Contains("[0]: first item", r);
            StringAssert.Contains("m_Mass: 2", r);
        }

        [Test]
        public void Project_SpecialChars_QuotesInValue()
        {
            var input = "[Script]\nm_Name: \"Player (Clone)\"\nm_Mass: 2\n";
            var r = FieldProjector.Project(input, "m_Name");
            StringAssert.Contains("m_Name: \"Player (Clone)\"", r);
        }

        // ── P1: Edge cases ─────────────────────────────────────────────────────

        [Test] public void Project_NullFields_ReturnsOriginal() =>
            Assert.AreEqual(S, FieldProjector.Project(S, null));

        [Test] public void Project_EmptyFields_ReturnsOriginal() =>
            Assert.AreEqual(S, FieldProjector.Project(S, ""));

        [Test] public void Project_NullText_ReturnsNull() =>
            Assert.IsNull(FieldProjector.Project(null, "mass"));

        [Test] public void Project_EmptyText_ReturnsEmpty() =>
            Assert.AreEqual("", FieldProjector.Project("", "mass"));

        [Test] public void Project_WhitespaceFields_ReturnsOriginal() =>
            Assert.AreEqual(S, FieldProjector.Project(S, "  ,  "));

        [Test]
        public void Project_MultipleFields()
        {
            var r = FieldProjector.Project(S, "m_Mass,m_LocalPosition");
            StringAssert.Contains("m_Mass: 2", r);
            StringAssert.Contains("m_LocalPosition.x: 5", r);
        }

        [Test] public void Project_TrailingComma() =>
            StringAssert.Contains("m_Mass: 2", FieldProjector.Project(S, "mass,"));

        [Test]
        public void Project_NoMatch_OnlyStructural()
        {
            var input = "[C]\nm_Mass: 2\n---\n";
            var r = FieldProjector.Project(input, "nonexistent");
            StringAssert.Contains("[C]", r);
            StringAssert.Contains("---", r);
            StringAssert.DoesNotContain("m_Mass", r);
        }
    }
}
