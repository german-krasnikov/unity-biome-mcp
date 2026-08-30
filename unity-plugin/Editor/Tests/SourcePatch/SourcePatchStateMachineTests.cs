using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchStateMachineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // §3.3 exact 8-edge legal set. Every other one of the 36 ordered
        // pairs over the 6 states — including every self-transition and
        // OnReady -> Recovery (uncertainty/drift always pass through Busy
        // inside the coordinator; a stale-receipt Recovery is a domain-start
        // INITIAL state, never a transition into it) — is forbidden.
        private static readonly HashSet<(SourcePatchState, SourcePatchState)> ExpectedLegal =
            new HashSet<(SourcePatchState, SourcePatchState)>
        {
            (SourcePatchState.Unavailable, SourcePatchState.Off),
            (SourcePatchState.Off, SourcePatchState.OnReady),
            (SourcePatchState.OnReady, SourcePatchState.Busy),
            (SourcePatchState.Busy, SourcePatchState.OnReady),
            (SourcePatchState.Busy, SourcePatchState.Recovery),
            (SourcePatchState.OnReady, SourcePatchState.Disabling),
            (SourcePatchState.Disabling, SourcePatchState.Off),
            (SourcePatchState.Disabling, SourcePatchState.Recovery),
        };

        [Test]
        public void LegalTransitions_MatchExactExpectedSetAcrossAllPairs()
        {
            var states = (SourcePatchState[])Enum.GetValues(typeof(SourcePatchState));
            var actualLegal = new HashSet<(SourcePatchState, SourcePatchState)>();

            foreach (var from in states)
            {
                foreach (var to in states)
                {
                    if (SourcePatchStateMachine.IsLegalTransition(from, to))
                    {
                        actualLegal.Add((from, to));
                    }
                }
            }

            CollectionAssert.AreEquivalent(ExpectedLegal, actualLegal);
        }

        [Test]
        public void TryTransition_LegalMove_UpdatesCurrentAndReturnsTrue()
        {
            var machine = new SourcePatchStateMachine(SourcePatchState.Off);

            var moved = machine.TryTransition(SourcePatchState.OnReady);

            Assert.IsTrue(moved);
            Assert.AreEqual(SourcePatchState.OnReady, machine.Current);
        }

        [Test]
        public void TryTransition_IllegalMove_LeavesCurrentUnchangedAndReturnsFalse()
        {
            var machine = new SourcePatchStateMachine(SourcePatchState.Off);

            var moved = machine.TryTransition(SourcePatchState.Busy);

            Assert.IsFalse(moved);
            Assert.AreEqual(SourcePatchState.Off, machine.Current);
        }

        [Test]
        public void FromRecovery_NoOutgoingTransitionIsLegal()
        {
            var states = (SourcePatchState[])Enum.GetValues(typeof(SourcePatchState));

            foreach (var to in states)
            {
                Assert.IsFalse(
                    SourcePatchStateMachine.IsLegalTransition(SourcePatchState.Recovery, to),
                    $"Recovery -> {to} must not be legal");
            }
        }

        [Test]
        public void Constructor_NoArgument_DefaultsToUnavailable()
        {
            var machine = new SourcePatchStateMachine();

            Assert.AreEqual(SourcePatchState.Unavailable, machine.Current);
        }
    }
}
