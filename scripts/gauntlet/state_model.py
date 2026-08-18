"""Seeded legal-transition generation and deterministic repro minimization."""


import random
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass, field


class ModelError(ValueError):
    """Raised when a generated or replayed transition sequence is invalid."""


@dataclass(frozen=True, slots=True)
class Transition:
    transition_id: str
    source_state: str
    target_state: str
    contract_id: str


@dataclass(frozen=True, slots=True)
class SequencePlan:
    seed: int
    initial_state: str
    transition_ids: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class StateModel:
    initial_state: str
    transitions: tuple[Transition, ...]
    _by_id: dict[str, Transition] = field(init=False, repr=False, compare=False)
    _outgoing: dict[str, tuple[Transition, ...]] = field(
        init=False,
        repr=False,
        compare=False,
    )

    def __post_init__(self) -> None:
        if not self.initial_state:
            raise ModelError("initial state must not be empty")
        if not self.transitions:
            raise ModelError("state model must contain transitions")

        by_id: dict[str, Transition] = {}
        outgoing_lists: dict[str, list[Transition]] = {}
        for transition in self.transitions:
            _validate_transition(transition)
            if transition.transition_id in by_id:
                raise ModelError(f"duplicate transition ID: {transition.transition_id}")
            by_id[transition.transition_id] = transition
            outgoing_lists.setdefault(transition.source_state, []).append(transition)
        if self.initial_state not in outgoing_lists:
            raise ModelError("initial state has no outgoing transition")
        outgoing = {
            state: tuple(sorted(items, key=lambda item: item.transition_id))
            for state, items in outgoing_lists.items()
        }
        object.__setattr__(self, "_by_id", by_id)
        object.__setattr__(self, "_outgoing", outgoing)

    def generate(self, *, seed: int, max_steps: int) -> SequencePlan:
        if isinstance(max_steps, bool) or not isinstance(max_steps, int) or max_steps <= 0:
            raise ModelError("maximum step count must be a positive integer")
        randomizer = random.Random(seed)
        state = self.initial_state
        selected: list[str] = []
        for _ in range(max_steps):
            choices = self._outgoing.get(state, ())
            if not choices:
                break
            transition = choices[randomizer.randrange(len(choices))]
            selected.append(transition.transition_id)
            state = transition.target_state
        return SequencePlan(seed, self.initial_state, tuple(selected))

    def replay(self, plan: SequencePlan) -> str:
        if plan.initial_state != self.initial_state:
            raise ModelError("sequence initial state does not match the model")
        state = plan.initial_state
        for transition_id in plan.transition_ids:
            try:
                transition = self._by_id[transition_id]
            except KeyError as exc:
                raise ModelError(f"unknown transition: {transition_id}") from exc
            if transition.source_state != state:
                raise ModelError(
                    f"transition {transition_id} is not legal from state {state}"
                )
            state = transition.target_state
        return state


def minimize_failure(
    model: StateModel,
    plan: SequencePlan,
    reproduces: Callable[[SequencePlan], bool],
) -> SequencePlan:
    """Shrink a failing legal plan without inventing transitions or state."""
    model.replay(plan)
    if not reproduces(plan):
        raise ModelError("input sequence does not reproduce the failure")

    current = _shortest_failing_window(model, plan, reproduces)
    changed = True
    while changed and len(current.transition_ids) > 1:
        changed = False
        for index in range(len(current.transition_ids)):
            ids = current.transition_ids[:index] + current.transition_ids[index + 1 :]
            candidate = SequencePlan(current.seed, current.initial_state, ids)
            if not _is_legal(model, candidate):
                continue
            if reproduces(candidate):
                current = candidate
                changed = True
                break
    return current


def _shortest_failing_window(
    model: StateModel,
    plan: SequencePlan,
    reproduces: Callable[[SequencePlan], bool],
) -> SequencePlan:
    identifiers = plan.transition_ids
    for size in range(1, len(identifiers)):
        for start in range(0, len(identifiers) - size + 1):
            candidate = SequencePlan(
                plan.seed,
                plan.initial_state,
                identifiers[start : start + size],
            )
            if _is_legal(model, candidate) and reproduces(candidate):
                return candidate
    return plan


def _is_legal(model: StateModel, plan: SequencePlan) -> bool:
    try:
        model.replay(plan)
    except ModelError:
        return False
    return True


def _validate_transition(transition: Transition) -> None:
    if not transition.transition_id:
        raise ModelError("transition ID must not be empty")
    if not transition.source_state or not transition.target_state:
        raise ModelError("transition states must not be empty")
    if not transition.contract_id:
        raise ModelError("transition contract ID must not be empty")

