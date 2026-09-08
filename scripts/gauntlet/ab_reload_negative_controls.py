"""N3 Task 5: T3 negative controls. After the positive T3 pass, on the SAME
still-running A/B workers, prove the oracles that a real regression would
need to catch actually catch it -- not a fresh scripted example, a live
mutation. Pure phase logic over injected call/sync/mutate seams; the live
file mutation itself lives in scripts/run_ab_reload_identity.py. See
Plans/N3-T3-T4-live-reload-identity.md Task 5.

b_mutated_red: really mutate + recompile B's nonce, confirm
check_sentinel_unchanged raises against the live before/after reads, then
restore B to a fresh, known-clean (third, distinct) nonce.

old_code_not_new: an oracle-level control, no I/O -- feed check_new_code_executed
the live OLD nonce of A (already superseded by T3's positive recompile) as
the observed value and confirm it raises 'old code presented as new'.
"""

from collections.abc import Awaitable, Callable  # noqa: TC003
from dataclasses import dataclass

from gauntlet.ab_reload_identity import (
    ABReloadIdentityError,
    CallFn,
    check_new_code_executed,
    check_sentinel_unchanged,
    read_nonce,
)


@dataclass
class NegativeControlsConfig:
    port_b: int
    project_b: str
    old_nonce_a: str  # A's nonce before T3's positive recompile
    new_nonce_a: str  # A's nonce T3 already confirmed live after its recompile


@dataclass
class NegativeControlsSeams:
    call: CallFn
    sync_unity: Callable[[int, str], Awaitable[str]]  # (port, project_path) -> verdict string
    mutate_nonce_b: Callable[[], Awaitable[None]]   # writes a new, distinct nonce into B's fixture on disk
    restore_nonce_b: Callable[[], Awaitable[None]]  # writes a third, distinct nonce into B's fixture on disk


def check_old_code_not_new(new_nonce_a: str, old_nonce_a: str) -> str:
    """Oracle-level negative control: feeding the stale old nonce as the
    observed value must raise. Returns the captured message; re-raises as a
    harness failure if the oracle stayed green (double-red: this control
    must fail for the right, specific reason, not by accident)."""
    try:
        check_new_code_executed(new_nonce_a, old_nonce_a, old_nonce_a)
    except ABReloadIdentityError as error:
        return str(error)
    raise ABReloadIdentityError(
        "negative control failed: check_new_code_executed did not raise "
        "when fed the stale old nonce as the observed value"
    )


async def run_negative_controls(
    config: NegativeControlsConfig, seams: NegativeControlsSeams
) -> dict[str, object]:
    nonce_b_before = await read_nonce(seams.call, config.port_b)

    await seams.mutate_nonce_b()
    mutate_verdict = await seams.sync_unity(config.port_b, config.project_b)
    nonce_b_mutated = await read_nonce(seams.call, config.port_b)
    try:
        check_sentinel_unchanged(nonce_b_before, nonce_b_mutated)
    except ABReloadIdentityError as error:
        oracle_message = str(error)
    else:
        raise ABReloadIdentityError(
            "negative control failed: check_sentinel_unchanged did not raise after a real live B mutation"
        )

    await seams.restore_nonce_b()
    restore_verdict = await seams.sync_unity(config.port_b, config.project_b)
    nonce_b_restored = await read_nonce(seams.call, config.port_b)
    if nonce_b_restored in (nonce_b_before, nonce_b_mutated):
        raise ABReloadIdentityError(
            f"restore did not produce a fresh third nonce: got {nonce_b_restored!r}, "
            f"which repeats before={nonce_b_before!r} or mutated={nonce_b_mutated!r}"
        )

    b_mutated_red = {
        "nonce_b_before": nonce_b_before,
        "nonce_b_mutated": nonce_b_mutated,
        "mutate_sync_verdict": mutate_verdict,
        "oracle_message": oracle_message,
        "nonce_b_restored": nonce_b_restored,
        "restore_sync_verdict": restore_verdict,
    }
    old_code_not_new = {
        "old_nonce_a": config.old_nonce_a,
        "new_nonce_a": config.new_nonce_a,
        "oracle_message": check_old_code_not_new(config.new_nonce_a, config.old_nonce_a),
    }
    return {"t3_negative_controls": {"b_mutated_red": b_mutated_red, "old_code_not_new": old_code_not_new}}
