// TDD: Blocker 2 — PlayerPlaytestRunner's step-execution loop had no try/catch, so a step
// handler exception propagated out of Run()'s coroutine uncaught. Unity swallows an unhandled
// coroutine exception inside its own driving code (this is why one never crashes Play Mode or
// the Player — it just aborts that coroutine silently), so a plain try/catch wrapped around
// `yield return Execute(step);` in Run() could never have caught it anyway: by the time a step
// throws, control is inside Unity's own internal driving of the nested enumerator, not inside
// Run()'s own call stack (and C# disallows `yield return` inside a try block that has a catch
// clause regardless). WriteReceipts()/Application.Quit() were only ever reached after the loop
// completed normally — no receipt, no exit, on any step exception.
//
// There is no live-Player build available locally to exercise this end-to-end (same class of
// gap AssemblyInfo.cs documents for the internal step-handler methods), so this is a structural
// regression guard reading the actual source: it asserts a helper drives the step enumerator's
// MoveNext() inside a real try/catch — the only place that can actually intercept the exception —
// and that WriteReceipts() is reached unconditionally after the step loop, not only on its
// success path.
using NUnit.Framework;
using UnityMCP.Playtest;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlayerPlaytestRunnerReceiptDurabilityTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static string Source =>
            ReadRequiredPackageSource(typeof(PlayerPlaytestRunner), "Runtime/Playtest/PlayerPlaytestRunner.cs");

        [Test]
        public void Run_StepLoop_NeverYieldReturnsTheRawStepEnumeratorDirectly()
        {
            var runBody = ExtractMethodBody(Source, "private IEnumerator Run()");
            Assert.That(runBody, Does.Not.Contain("yield return Execute(step)"),
                "Run()'s step loop must not yield-return the raw step enumerator directly — Unity's " +
                "coroutine engine would then drive it on its own internal stack, past any try/catch " +
                "written in Run() (and C# disallows yield return inside a try block with a catch " +
                "clause anyway). Route through a helper that drives MoveNext() itself instead.");
        }

        [Test]
        public void ExecuteStepSafely_WrapsStepMoveNextInATryCatch()
        {
            var safeBody = ExtractMethodBody(Source, "IEnumerator ExecuteStepSafely(");
            Assert.That(safeBody, Does.Match(@"try\s*\{[^}]*MoveNext"),
                "The step enumerator's MoveNext() call must be wrapped in a try block — that is the " +
                "only place an exception thrown by a step handler can actually be caught.");
            Assert.That(safeBody, Does.Contain("catch"),
                "A catch clause must record the exception instead of letting it propagate out of the coroutine.");
        }

        [Test]
        public void Run_WriteReceiptsCall_IsReachableAfterTheStepLoopRegardlessOfAStepException()
        {
            var runBody = ExtractMethodBody(Source, "private IEnumerator Run()");
            var loopBlockStart = runBody.IndexOf("if (!hasUnsupported)", System.StringComparison.Ordinal);
            Assert.That(loopBlockStart, Is.GreaterThanOrEqualTo(0), "Expected the step-loop guard block");
            var afterLoop = runBody.Substring(FindMatchingBraceEnd(runBody, loopBlockStart));
            Assert.That(afterLoop, Does.Contain("WriteReceipts("),
                "WriteReceipts() must be reachable after the step-loop block regardless of whether a " +
                "step handler threw mid-run — not only nested inside the loop's own success path.");
        }

        private static string ExtractMethodBody(string source, string signatureContains)
        {
            var sigIndex = source.IndexOf(signatureContains, System.StringComparison.Ordinal);
            Assert.That(sigIndex, Is.GreaterThanOrEqualTo(0),
                $"Could not find a method whose signature contains '{signatureContains}'");
            var braceStart = source.IndexOf('{', sigIndex);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0));
            return source.Substring(braceStart, FindMatchingBraceEnd(source, braceStart - 1) - braceStart);
        }

        /// <summary>Returns the index just past the closing brace matching the '{' at or after fromIndex.</summary>
        private static int FindMatchingBraceEnd(string text, int fromIndex)
        {
            var braceStart = text.IndexOf('{', fromIndex);
            Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), "Expected an opening brace");
            var depth = 0;
            for (var i = braceStart; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }
            Assert.Fail("Unbalanced braces while scanning source");
            return -1;
        }
    }
}
