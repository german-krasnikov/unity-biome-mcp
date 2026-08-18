"""Deterministic state-model generation and failure minimization tests."""


import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from gauntlet.state_model import (  # noqa: E402
    ModelError,
    SequencePlan,
    StateModel,
    Transition,
    minimize_failure,
)


def _model() -> StateModel:
    return StateModel(
        initial_state="stopped_clean",
        transitions=(
            Transition("read-status", "stopped_clean", "stopped_clean", "status-read"),
            Transition("enter-play", "stopped_clean", "playing", "editor-play"),
            Transition("runtime-read", "playing", "playing", "runtime-read"),
            Transition("exit-play", "playing", "stopped_clean", "editor-stop"),
        ),
    )


def test_generation_is_seeded_replayable_and_legal() -> None:
    model = _model()

    first = model.generate(seed=42, max_steps=12)
    second = model.generate(seed=42, max_steps=12)
    different = model.generate(seed=7, max_steps=12)

    assert first == second
    assert first != different
    assert len(first.transition_ids) == 12
    assert model.replay(first) in {"stopped_clean", "playing"}


def test_replay_rejects_illegal_or_unknown_transition() -> None:
    model = _model()

    with pytest.raises(ModelError, match="not legal"):
        model.replay(SequencePlan(1, "stopped_clean", ("exit-play",)))
    with pytest.raises(ModelError, match="unknown"):
        model.replay(SequencePlan(1, "stopped_clean", ("missing",)))


def test_minimizer_preserves_failure_and_legal_state_edges() -> None:
    model = _model()
    plan = SequencePlan(
        seed=9,
        initial_state="stopped_clean",
        transition_ids=(
            "read-status",
            "read-status",
            "enter-play",
            "runtime-read",
            "runtime-read",
            "exit-play",
        ),
    )

    minimized = minimize_failure(
        model,
        plan,
        lambda candidate: "runtime-read" in candidate.transition_ids,
    )

    assert minimized.transition_ids == ("enter-play", "runtime-read")
    assert model.replay(minimized) == "playing"


def test_minimizer_requires_an_observed_failure() -> None:
    model = _model()
    plan = model.generate(seed=1, max_steps=2)

    with pytest.raises(ModelError, match="does not reproduce"):
        minimize_failure(model, plan, lambda _: False)


@pytest.mark.parametrize(
    "transition",
    [
        Transition("", "a", "b", "contract"),
        Transition("id", "", "b", "contract"),
        Transition("id", "a", "b", ""),
    ],
)
def test_transition_fields_are_required(transition: Transition) -> None:
    with pytest.raises(ModelError):
        StateModel("a", (transition,))

