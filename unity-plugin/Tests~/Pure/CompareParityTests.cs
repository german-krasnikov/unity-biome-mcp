using System;
using NUnit.Framework;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Playtest.Core.PureTests
{
    // D17 — permanent fence against a third Compare ever being written by
    // accident. Each row/case pins one specific divergence D13/D14 found
    // between the two now-deleted implementations (PlayerPlaytestQuery's own
    // deleted Compare, and the ad hoc handling EvaluateAssert needed before
    // D14) and Core's single surviving PlaytestParser.Compare.
    [TestFixture]
    public class CompareParityTests
    {
        // D13: Player's deleted Compare used IndexOf(..., OrdinalIgnoreCase) for
        // `contains` — case-insensitive. Core's `actual.Contains(expected)` is
        // case-sensitive. Row 1 is the case both implementations agreed on; row 2
        // is the one place they diverged (would have been TRUE under the deleted
        // Player Compare, is FALSE under Core).
        [TestCase("Hello World", "contains", "World", true)]
        [TestCase("Hello World", "contains", "world", false)]
        // D13: numeric operands take the numeric branch (with epsilon tolerance)
        // before either implementation's string branch is reached — both agreed
        // here, but nothing previously pinned the branch-order itself.
        [TestCase("5", "==", "5.0", true)]
        [TestCase("5", ">", "3", true)]
        [TestCase("3", ">", "5", false)]
        // D13: neither operand parses as a float -> string branch. `==`/`!=` stay
        // case-insensitive in both implementations (no divergence here; pinned so
        // a future edit can't silently tighten it while "fixing" the rows above).
        [TestCase("abc", "==", "ABC", true)]
        [TestCase("abc", "!=", "xyz", true)]
        public void Compare_ParityTable_MatchesExpectedResult(
            string actual, string op, string expected, bool expectedResult)
        {
            Assert.AreEqual(expectedResult, PlaytestParser.Compare(actual, op, expected));
        }

        // D13: Player's deleted Compare silently returned false for an unrecognised
        // operator; Core throws ArgumentException (caught by EvaluateAssert's
        // pre-existing try/catch, so no new unhandled-exception risk — just a more
        // informative failure message). Compare has two independent unknown-operator
        // throw sites — the numeric branch and the string branch — so both operand
        // shapes are pinned separately; a mutation fixing one silently could still
        // regress the other.
        [Test]
        public void Compare_UnknownOperator_NumericOperands_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Compare("1", "~=", "2"));
        }

        [Test]
        public void Compare_UnknownOperator_NonNumericOperands_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Compare("abc", "~=", "xyz"));
        }

        // D14: a step can arrive with Op=="" (ParseQOV's own bool-shorthand
        // fallback). Compare has no bool-shorthand branch and throws on an empty
        // operator — this is exactly why D14 added the IsNullOrEmpty(step.Op)
        // special case in EvaluateAssert instead of relying on Compare directly.
        [Test]
        public void Compare_EmptyOperator_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => PlaytestParser.Compare("True", "", "True"));
        }
    }
}
