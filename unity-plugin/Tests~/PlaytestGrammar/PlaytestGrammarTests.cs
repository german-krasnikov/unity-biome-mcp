using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    public class PlaytestGrammarTests
    {
        // The 19 verbs of the 25 tracked .playtest files (dsl/playtest/oracle.rows, taken from the hand parser before any construct existed).
        private static readonly string[] VerbsOfTheScripts =
        {
            "ASSERT", "INVOKE", "WAIT_UNTIL", "INCLUDE", "ASSERT_CONSOLE_CLEAN", "LOG", "TIMESCALE", "SET", "VAL", "MCP",
            "WAIT", "VAR", "CLICK", "FILL", "FOCUS", "SETUP", "TEARDOWN", "SNAPSHOT", "SECTION",
        };

        private static IEnumerable<Element> Walk(IEnumerable<Element> elements) =>
            elements.SelectMany(e => e.Kind == ElementKind.Group ? Walk(e.Items) : new[] { e });

        private static IEnumerable<Element> Slots(Form form) =>
            Walk(form.Elements.Concat(form.Items.SelectMany(i => i))).Where(e => e.Kind == ElementKind.Slot);

        private static int IndexOf(string construct) => PlaytestGrammar.Forms.Select(f => f.Construct).ToList().IndexOf(construct);

        [Test]
        public void Keywords_HoldEveryVerbOfTheTrackedScripts() =>
            Assert.That(PlaytestGrammar.Keywords, Is.SupersetOf(VerbsOfTheScripts));

        [Test]
        public void Keywords_AreUniqueUpperCaseAndInOrdinalOrder()
        {
            Assert.That(PlaytestGrammar.Keywords, Is.Unique);
            Assert.That(PlaytestGrammar.Keywords, Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal));
            Assert.That(PlaytestGrammar.Keywords, Has.All.Matches("^[A-Z_]+$"));
        }

        [Test]
        public void EveryForm_StartsWithAWord_AndKeywordsAreExactlyTheVerbsOfTheForms()
        {
            foreach (var form in PlaytestGrammar.Forms)
            {
                Assert.That(form.Elements[0].Kind, Is.EqualTo(ElementKind.Word), form.Construct);
                Assert.That(form.Verb, Is.EqualTo(form.Elements[0].Text), form.Construct);
            }
            Assert.That(PlaytestGrammar.Forms.Select(f => f.Verb).Distinct(), Is.EquivalentTo(PlaytestGrammar.Keywords));
        }

        [Test]
        public void Constructs_AreUnique_AndCarryTheCodePrefix()
        {
            Assert.That(PlaytestGrammar.Forms.Select(f => f.Construct), Is.Unique);
            Assert.That(PlaytestGrammar.Forms.Select(f => f.Construct), Has.All.StartsWith(PlaytestGrammar.CodePrefix + "_"));
        }

        [Test]
        public void ASlotName_IsUsedOnceInAForm()
        {
            foreach (var form in PlaytestGrammar.Forms)
                Assert.That(Slots(form).Select(s => s.Text), Is.Unique, form.Construct);
        }

        [Test]
        public void EverySlot_CarriesWhatItsTypeNeeds()
        {
            foreach (var form in PlaytestGrammar.Forms)
                foreach (var slot in Slots(form))
                {
                    var where = form.Construct + "." + slot.Text;
                    var takesArg = slot.Type == SlotType.Match || slot.Type == SlotType.Enum;
                    Assert.That(slot.Arg != null, Is.EqualTo(takesArg), where);
                    Assert.That(!slot.Lazy || slot.Type == SlotType.Text, where);
                    Assert.That(slot.Code == null || slot.Code.StartsWith(PlaytestGrammar.CodePrefix + "_"), where);
                }
        }

        // The expressions are those of a JavaScript RegExp (the reader of the SURFACE): the host takes them in the mode of .NET that reads \d, \s and \w as JavaScript does.
        [Test]
        public void EveryMatchExpression_CompilesInEcmaScriptMode_AsTheWholeTokenRegex()
        {
            foreach (var form in PlaytestGrammar.Forms)
                foreach (var slot in Slots(form).Where(s => s.Type == SlotType.Match))
                    Assert.DoesNotThrow(() => new Regex("^(?:" + slot.Arg + ")$", RegexOptions.ECMAScript), form.Construct + "." + slot.Text);
        }

        [Test]
        public void TheFormsKeepTheOrderOfTheSurface_TheFirstThatReadsALineWins()
        {
            Assert.That(PlaytestGrammar.Ordered, Is.True);
            Assert.That(IndexOf("PT_MCP_INTO"), Is.InRange(0, IndexOf("PT_MCP") - 1));
            Assert.That(IndexOf("PT_ASSERT"), Is.InRange(0, IndexOf("PT_ASSERT_FLAG") - 1));
            Assert.That(IndexOf("PT_WAIT_UNTIL"), Is.InRange(0, IndexOf("PT_WAIT_UNTIL_FLAG") - 1));
        }

        [Test]
        public void OnlyAssertBatchIsABlock_AndItHasItsLine()
        {
            Assert.That(PlaytestGrammar.Forms.Where(f => f.IsBlock).Select(f => f.Construct), Is.EqualTo(new[] { "PT_ASSERT_BATCH" }));
            var batch = PlaytestGrammar.Forms[IndexOf("PT_ASSERT_BATCH")];
            Assert.That(batch.Items, Has.Count.EqualTo(1));
            Assert.That(batch.Items[0][0].Text, Is.EqualTo("ASSERT"));
            Assert.That(PlaytestGrammar.Forms.Where(f => !f.IsBlock).Select(f => f.Items.Count), Has.All.EqualTo(0));
        }

        [Test]
        public void Wait_IsTheWordAndANumberSlotThatRefusesWithPtNumber()
        {
            var wait = PlaytestGrammar.Forms[IndexOf("PT_WAIT")];
            Assert.That(wait.Elements, Has.Count.EqualTo(2));
            Assert.That((wait.Elements[0].Kind, wait.Elements[0].Text), Is.EqualTo((ElementKind.Word, "WAIT")));
            Assert.That((wait.Elements[1].Kind, wait.Elements[1].Type, wait.Elements[1].Code), Is.EqualTo((ElementKind.Slot, SlotType.Number, "PT_NUMBER")));
        }
    }
}
