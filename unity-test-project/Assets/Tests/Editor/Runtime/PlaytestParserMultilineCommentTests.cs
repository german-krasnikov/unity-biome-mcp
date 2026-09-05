using NUnit.Framework;
using System;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class PlaytestParserMultilineCommentTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // 1. Basic COMMENT...END_COMMENT block is stripped; no steps emitted for its contents
        [Test]
        public void Parse_CommentBlock_Stripped()
        {
            var steps = PlaytestParser.Parse(
                "COMMENT\nLOG hidden\nEND_COMMENT\nASSERT_CONSOLE_CLEAN");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        // 2. "COMMENT <title>" on opening line is still stripped
        [Test]
        public void Parse_CommentWithTitle_Stripped()
        {
            var steps = PlaytestParser.Parse(
                "COMMENT Test case: door damage\nLOG hidden\nEND_COMMENT\nASSERT_CONSOLE_CLEAN");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        // 3. $sigil inside COMMENT block does NOT produce an unresolved-sigil warning
        [Test]
        public void Parse_CommentBlock_SigilNoWarn()
        {
            var result = PlaytestParser.Parse(
                "COMMENT\nASSERT /Player|Health|hp == $missingVal\nEND_COMMENT\nASSERT_CONSOLE_CLEAN");
            Assert.That(result.Warnings, Is.Null.Or.Empty);
        }

        // 4. MACRO inside COMMENT is not registered; CALL to it throws
        [Test]
        public void Parse_CommentBlock_MacroNotRegistered()
        {
            var script =
                "COMMENT\n" +
                "MACRO HiddenMacro\nWAIT 1.0\nEND_MACRO\n" +
                "END_COMMENT\n" +
                "CALL HiddenMacro\n" +
                "ASSERT_CONSOLE_CLEAN";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // 5. Missing END_COMMENT throws ArgumentException containing "END_COMMENT"
        [Test]
        public void Parse_CommentBlock_MissingEnd_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("COMMENT\nLOG hidden\nASSERT_CONSOLE_CLEAN"));
            StringAssert.Contains("END_COMMENT", ex.Message);
        }

        // 6. Stray END_COMMENT with no matching COMMENT → no exception, steps unaffected
        [Test]
        public void Parse_StrayEndComment_NoException()
        {
            var steps = PlaytestParser.Parse("ASSERT_CONSOLE_CLEAN\nEND_COMMENT");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        // 7. COMMENT block inside an INCLUDEd file is stripped
        [Test]
        public void Parse_CommentInIncludeFile_Stripped()
        {
            var steps = PlaytestParser.Parse(
                "INCLUDE defs.playtest\nASSERT_CONSOLE_CLEAN",
                _ => "COMMENT\nVAL $hp /Player|Health|hp\nEND_COMMENT\n");
            Assert.That(steps.Count, Is.EqualTo(1));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        // 8. Steps before and after COMMENT are both emitted in original order
        [Test]
        public void Parse_CommentBetweenSteps_OrderPreserved()
        {
            var script =
                "WAIT 1.0\n" +
                "COMMENT\nLOG skipped\nEND_COMMENT\n" +
                "WAIT 2.0\n" +
                "ASSERT_CONSOLE_CLEAN";
            var steps = PlaytestParser.Parse(script);
            Assert.That(steps.Count, Is.EqualTo(3));
            Assert.That(steps[0].Delay, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(steps[1].Delay, Is.EqualTo(2.0f).Within(0.001f));
            Assert.That(steps[2].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }

        // 9. ALIAS inside COMMENT is not registered (substitution does not apply)
        [Test]
        public void Parse_AliasInsideComment_NotRegistered()
        {
            // If ALIAS were applied, query "NPC|Health|hp" would become "/Enemy|Health|hp"
            var script =
                "COMMENT\nALIAS NPC /Enemy\nEND_COMMENT\n" +
                "ASSERT NPC|Health|hp > 0";
            var steps = PlaytestParser.Parse(script);
            Assert.That(steps[0].Query, Is.EqualTo("NPC|Health|hp"));
        }

        // 10. COMMENT block inside a MACRO body is stripped on CALL expansion
        [Test]
        public void Parse_CommentInMacroBody_Stripped()
        {
            var script =
                "MACRO CheckHealth\n" +
                "WAIT 0.5\n" +
                "COMMENT skip debug\n" +
                "LOG debug info\n" +
                "END_COMMENT\n" +
                "ASSERT /Player|Health|hp > 0\n" +
                "END_MACRO\n" +
                "CALL CheckHealth\n" +
                "ASSERT_CONSOLE_CLEAN";
            var steps = PlaytestParser.Parse(script);
            // WAIT + ASSERT (from macro) + ASSERT_CONSOLE_CLEAN
            Assert.That(steps.Count, Is.EqualTo(3));
            Assert.That(steps[0].Type, Is.EqualTo(StepType.Wait));
            Assert.That(steps[1].Type, Is.EqualTo(StepType.Assert));
            Assert.That(steps[2].Type, Is.EqualTo(StepType.AssertConsoleClean));
        }
    }
}
