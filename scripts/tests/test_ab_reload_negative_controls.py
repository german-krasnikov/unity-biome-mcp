"""Offline tests for gauntlet.ab_reload_negative_controls.run_negative_controls
(N3 Task 5: T3 negative controls). No Unity, no network -- fake call/sync/
mutate/restore seams only. Covers: b_mutated_red (a real B mutation the
sentinel oracle must catch, then a fresh third-nonce restore) and
old_code_not_new (an oracle-level control with no I/O), plus the harness's
own double-red guards -- each control must itself fail loudly if the oracle
it is proving stays green. See Plans/N3-T3-T4-live-reload-identity.md Task 5.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import gauntlet.ab_reload_negative_controls as nc  # noqa: E402

PORT_B = 9630
PROJECT_B = "/tmp/worker-b"
NONCE_B_BEFORE, NONCE_B_MUTATED, NONCE_B_RESTORED = "B-fixed", "B-mutated", "B-restored"
OLD_NONCE_A, NEW_NONCE_A = "A-old", "A-new"


class ScriptedCall:
    """Returns the next queued nonce for port_b's read_nonce (execute_code) on
    each call -- read_nonce always hits the actual wire, so the queue models
    each successive fresh read across mutate/restore."""

    def __init__(self, nonce_sequence: list[str]) -> None:
        self._queue = list(nonce_sequence)
        self.calls = 0

    async def __call__(self, port: int, cmd: str, args: dict) -> str:
        assert port == PORT_B
        assert cmd == "execute_code"
        self.calls += 1
        if not self._queue:
            raise AssertionError("no scripted nonce left")
        return self._queue.pop(0)


def make_seams(
    *, nonce_sequence: list[str], mutate_calls: list[str], restore_calls: list[str],
    sync_verdict: str = "sync clean",
) -> tuple[nc.NegativeControlsSeams, ScriptedCall]:
    call = ScriptedCall(nonce_sequence)

    async def sync_unity(port: int, project: str) -> str:
        assert port == PORT_B
        assert project == PROJECT_B
        return sync_verdict

    async def mutate_nonce_b() -> None:
        mutate_calls.append("mutate")

    async def restore_nonce_b() -> None:
        restore_calls.append("restore")

    seams = nc.NegativeControlsSeams(
        call=call, sync_unity=sync_unity,
        mutate_nonce_b=mutate_nonce_b, restore_nonce_b=restore_nonce_b,
    )
    return seams, call


def make_config() -> nc.NegativeControlsConfig:
    return nc.NegativeControlsConfig(
        port_b=PORT_B, project_b=PROJECT_B, old_nonce_a=OLD_NONCE_A, new_nonce_a=NEW_NONCE_A,
    )


@pytest.mark.asyncio
async def test_run_negative_controls_returns_both_slices_with_evidence() -> None:
    mutate_calls: list[str] = []
    restore_calls: list[str] = []
    seams, call = make_seams(
        nonce_sequence=[NONCE_B_BEFORE, NONCE_B_MUTATED, NONCE_B_RESTORED],
        mutate_calls=mutate_calls, restore_calls=restore_calls,
    )
    result = await nc.run_negative_controls(make_config(), seams)

    controls = result["t3_negative_controls"]
    b_mutated = controls["b_mutated_red"]
    assert b_mutated["nonce_b_before"] == NONCE_B_BEFORE
    assert b_mutated["nonce_b_mutated"] == NONCE_B_MUTATED
    assert b_mutated["nonce_b_restored"] == NONCE_B_RESTORED
    assert "B sentinel changed" in b_mutated["oracle_message"]
    assert b_mutated["mutate_sync_verdict"] == "sync clean"
    assert b_mutated["restore_sync_verdict"] == "sync clean"
    assert mutate_calls == ["mutate"]
    assert restore_calls == ["restore"]

    old_code = controls["old_code_not_new"]
    assert old_code["old_nonce_a"] == OLD_NONCE_A
    assert old_code["new_nonce_a"] == NEW_NONCE_A
    assert "old code presented as new" in old_code["oracle_message"]
    assert call.calls == 3  # before, after-mutate, after-restore -- every read hit the wire


@pytest.mark.asyncio
async def test_run_negative_controls_raises_when_mutation_does_not_move_b() -> None:
    """Double-red guard: if the fake mutate seam is a no-op, B's nonce never
    actually changes, so check_sentinel_unchanged would stay green -- the
    harness must catch that and fail loudly, not silently record a passing
    control that proved nothing."""
    seams, _ = make_seams(
        nonce_sequence=[NONCE_B_BEFORE, NONCE_B_BEFORE, NONCE_B_RESTORED],
        mutate_calls=[], restore_calls=[],
    )
    with pytest.raises(nc.ABReloadIdentityError, match="did not raise after a real live B mutation"):
        await nc.run_negative_controls(make_config(), seams)


@pytest.mark.asyncio
async def test_run_negative_controls_raises_when_restore_reuses_before_nonce() -> None:
    seams, _ = make_seams(
        nonce_sequence=[NONCE_B_BEFORE, NONCE_B_MUTATED, NONCE_B_BEFORE],
        mutate_calls=[], restore_calls=[],
    )
    with pytest.raises(nc.ABReloadIdentityError, match="did not produce a fresh third nonce"):
        await nc.run_negative_controls(make_config(), seams)


@pytest.mark.asyncio
async def test_run_negative_controls_raises_when_restore_reuses_mutated_nonce() -> None:
    seams, _ = make_seams(
        nonce_sequence=[NONCE_B_BEFORE, NONCE_B_MUTATED, NONCE_B_MUTATED],
        mutate_calls=[], restore_calls=[],
    )
    with pytest.raises(nc.ABReloadIdentityError, match="did not produce a fresh third nonce"):
        await nc.run_negative_controls(make_config(), seams)


# --- oracle-level control (b): no I/O, proves the oracle itself discriminates ---


def test_check_old_code_not_new_returns_message_when_oracle_raises() -> None:
    message = nc.check_old_code_not_new(NEW_NONCE_A, OLD_NONCE_A)
    assert "old code presented as new" in message
    assert OLD_NONCE_A in message


def test_check_old_code_not_new_raises_harness_failure_when_oracle_stays_green() -> None:
    """Double-red guard: if new == old (a degenerate config), the oracle has
    nothing to distinguish and never raises -- the negative control itself
    must fail loudly rather than report a false pass."""
    with pytest.raises(nc.ABReloadIdentityError, match="did not raise when fed the stale old nonce"):
        nc.check_old_code_not_new("SAME", "SAME")
