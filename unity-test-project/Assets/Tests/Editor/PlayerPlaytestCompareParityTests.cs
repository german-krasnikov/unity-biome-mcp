// TDD: D13 — deletes PlayerPlaytestQuery's own private Compare and routes
// ASSERT through the shared UnityMCP.Playtest.Core.PlaytestParser.Compare
// instead. These are the "3 specific rows" the plan calls for now, in
// miniature, ahead of D17's full pure-lane parity table. Rows 1 and 3 flip
// between RED and GREEN across the swap (traced by hand against both Compare
// implementations); row 2 is a non-discriminating sanity companion proving
// the swap does not regress the exact-case contains path.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Playtest;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlayerPlaytestCompareParityTests : UnityMCP.Editor.Tests.SceneTestBase
    {
        private GameObject CreateNamed(string name)
        {
            var go = new GameObject(name);
            TrackOwnedObject(go);
            return go;
        }

        private static PlayerPlaytestRunner.StepResult EvaluateNameAssert(
            GameObject go, string op, string value)
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/" + go.name + "|name",
                Op = op,
                Value = value,
                RawLine = $"ASSERT /{go.name}|name {op} {value}",
            };
            return PlayerPlaytestRunner.EvaluateAssert(step);
        }

        [Test]
        public void EvaluateAssert_ContainsLowercase_IsCaseSensitiveAfterCoreSwap()
        {
            // Used to differ: Player's own (now-deleted) Compare did
            // actual.IndexOf(expected, OrdinalIgnoreCase) — case-insensitive.
            // Core's Compare uses actual.Contains(expected) — case-sensitive.
            var go = CreateNamed("Hello World");
            var result = EvaluateNameAssert(go, "contains", "hello");
            Assert.IsFalse(result.Passed);
        }

        [Test]
        public void EvaluateAssert_ContainsExactCase_StillPasses()
        {
            var go = CreateNamed("Hello World");
            var result = EvaluateNameAssert(go, "contains", "World");
            Assert.IsTrue(result.Passed);
        }

        [Test]
        public void EvaluateAssert_UnknownOperator_FailsWithCoreThrowMessage()
        {
            // Used to differ: Player's own (now-deleted) Compare silently
            // returned false (`_ => false`) for an unrecognised operator. Core's
            // Compare throws ArgumentException, caught by EvaluateAssert's
            // existing try/catch and surfaced as the step's failure message.
            var go = CreateNamed("Hello World");
            var result = EvaluateNameAssert(go, "~=", "anything");
            Assert.IsFalse(result.Passed);
            StringAssert.Contains("requires numeric values", result.Message);
        }
    }
}
