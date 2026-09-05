// TDD: D14 — fixes 3 gaps D11's step-based rewrite left behind (PlayerPlaytestQuery
// itself is byte-for-byte untouched by D11/D12; only how its inputs are obtained
// changed). Each test is a genuine double-red, traced by hand against the current
// (pre-fix) code before writing this file.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Playtest;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlayerPlaytestQueryTests : UnityMCP.Editor.Tests.SceneTestBase
    {
        private class VectorHolderBehaviour : MonoBehaviour
        {
            public Vector3 Position;
        }

        private class CombinerBehaviour : MonoBehaviour
        {
            public string Combine(string a, string b) => a + "|" + b;
        }

        [Test]
        public void EvaluateAssert_BareOperatorQuery_TreatsAsImplicitTrue()
        {
            // A step can legitimately arrive with Op=="" (e.g. ParseQOV's own
            // bool-shorthand fallback). Today Compare(actual, "", value) throws
            // ("Operator '' requires numeric values"), caught as a Fail. This
            // hand-builds that step shape directly — the only real DSL line that
            // reaches EvaluateAssert with Op=="" also pollutes Query via an
            // unrelated pre-existing Core parser quirk, out of scope for this
            // Player-only file.
            var go = new GameObject("BareTruthy");
            TrackOwnedObject(go);

            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/BareTruthy|activeSelf",
                Op = "",
                Value = "",
                RawLine = "ASSERT /BareTruthy|activeSelf",
            };
            var result = PlayerPlaytestRunner.EvaluateAssert(step);
            Assert.IsTrue(result.Passed, result.Message);
        }

        [Test]
        public void ExecuteSet_Vector3Field_ParsesCommaSeparatedFloats()
        {
            var go = new GameObject("VecHolder");
            TrackOwnedObject(go);
            var holder = go.AddComponent<VectorHolderBehaviour>();

            var step = new PlaytestStep
            {
                Type = StepType.Set,
                Path = "/VecHolder",
                Component = "VectorHolderBehaviour",
                Method = "Position",
                Args = "1,2,3",
                RawLine = "SET /VecHolder VectorHolderBehaviour Position 1,2,3",
            };
            var result = PlayerPlaytestRunner.ExecuteSet(step);
            Assert.IsTrue(result.Passed, result.Message);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), holder.Position);
        }

        [Test]
        public void ExecuteInvoke_BracketedMultiWordArg_SplitsViaSplitTokens()
        {
            var go = new GameObject("Combiner");
            TrackOwnedObject(go);
            go.AddComponent<CombinerBehaviour>();

            // Args round-trips through the top-level tokenizer's rejoin (see
            // PlaytestParser.Internals.cs's INVOKE case) — only bracket-protected
            // multi-word tokens survive that round-trip; quoted ones would
            // collapse ambiguously. Hence brackets here, not quotes.
            var parsed = PlaytestParser.Parse(
                "INVOKE /Combiner CombinerBehaviour Combine [hello there] world");
            Assert.AreEqual(1, parsed.Count);

            var result = PlayerPlaytestRunner.ExecuteInvoke(parsed[0]);
            Assert.IsTrue(result.Passed, result.Message);
            Assert.AreEqual("[hello there]|world", result.Message);
        }
    }
}
