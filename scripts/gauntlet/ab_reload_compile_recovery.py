"""N3 T4: compile-error recovery phase logic. Pure functions over injected
call/sync/break/repair seams -- no Unity lifecycle, no filesystem (that
lives in scripts/run_ab_reload_identity.py). See
Plans/N3-T3-T4-live-reload-identity.md Part 2.
"""

from collections.abc import Awaitable, Callable  # noqa: TC003
from dataclasses import dataclass

from gauntlet.ab_reload_identity import (
    ABReloadIdentityError,
    CallFn,
    check_new_code_executed,
    check_sentinel_unchanged,
    read_mvid,
    read_nonce,
)


def is_compile_failure_verdict(verdict: str) -> bool:
    """True only for a verdict that names a specific compiler rejection:
    'FAIL:CS####' (unity_mcp.tools.diagnose._verdict, lines 198/252 --
    f"FAIL:{cs}" where cs is an extracted CS#### code) or 'compile failed...'
    (unity_mcp.tools.sync._get_errors, line 55 -- 'compile failed (details
    unavailable)'), or a raw 'error CS' fragment.

    False for 'FAIL:stale-dll' (diagnose.py:262, sync.py:73 -- a DLL
    checksum/output mismatch, not a compiler rejection of new source) and
    'FAIL:unknown' (diagnose.py:198,253 -- the CS code could not be
    extracted). Both are DLL-integrity/unclassified verdicts, not proof the
    compiler rejected the injected syntax error; unity_mcp.tools.testing.py:310
    already treats them as recoverable, distinct from a genuine CS failure."""
    if verdict.startswith("FAIL:stale-dll") or verdict.startswith("FAIL:unknown"):
        return False
    return verdict.startswith("FAIL:CS") or verdict.startswith("compile failed") or "error CS" in verdict


def check_compile_failure_verdict(verdict: str) -> None:
    if not is_compile_failure_verdict(verdict):
        raise ABReloadIdentityError(
            f"expected a compile-failure verdict after injecting a syntax error, got {verdict!r}"
        )


def check_sync_clean_verdict(verdict: str) -> None:
    if not verdict.startswith("sync clean"):
        raise ABReloadIdentityError(f"expected a clean sync verdict after repair, got {verdict!r}")


def check_new_code_unavailable_during_break(injected_nonce: str, observed_nonce: str, old_nonce: str) -> None:
    """T4 break-phase oracle. Unlike T3's check_new_code_executed --  where
    observed == old is the failure -- here observed == old_nonce is the
    *expected*, correct outcome: a broken compile never replaces the loaded
    assembly, so the never-compiled injected_nonce (source text only) must
    stay unreachable through execute_code."""
    if observed_nonce == injected_nonce:
        raise ABReloadIdentityError(
            f"new nonce {injected_nonce!r} was presented while compilation is broken -- "
            "source text for a never-compiled assembly must never execute"
        )
    if observed_nonce != old_nonce:
        raise ABReloadIdentityError(
            f"unexpected nonce during compile break: observed {observed_nonce!r}, expected old {old_nonce!r}"
        )


@dataclass
class T4Config:
    port_a: int
    port_b: int
    project_a: str
    injected_nonce_a: str   # written into the broken .cs before run_t4(); must never execute
    repaired_nonce_a: str   # written into the repaired .cs; the post-repair new-code proof
    nonce_b: str            # B's fixed sentinel, never touched by A's break/repair


@dataclass
class T4Seams:
    call: CallFn
    sync_unity: Callable[[int, str], Awaitable[str]]  # (port, project_path) -> verdict string
    break_compile: Callable[[], Awaitable[None]]   # writes the injected nonce + a syntax error to A's fixture
    repair_compile: Callable[[], Awaitable[None]]  # writes a clean file with the repaired nonce


async def run_t4(config: T4Config, seams: T4Seams) -> dict[str, object]:
    """Setup -> break -> inspect-break -> repair -> inspect-repair, in that
    order. B is read fresh before, during the break, and after repair, and
    must never move -- A's fault is confined to A.
    """
    old_nonce_a = await read_nonce(seams.call, config.port_a)
    mvid_a_before = await read_mvid(seams.call, config.port_a)
    nonce_b_before = await read_nonce(seams.call, config.port_b)
    mvid_b_before = await read_mvid(seams.call, config.port_b)

    await seams.break_compile()
    break_verdict = await seams.sync_unity(config.port_a, config.project_a)
    check_compile_failure_verdict(break_verdict)

    nonce_a_during_break = await read_nonce(seams.call, config.port_a)
    check_new_code_unavailable_during_break(config.injected_nonce_a, nonce_a_during_break, old_nonce_a)
    mvid_a_during_break = await read_mvid(seams.call, config.port_a)
    if mvid_a_during_break != mvid_a_before:
        raise ABReloadIdentityError(
            f"A's MVID changed during a compile break: {mvid_a_before!r} -> {mvid_a_during_break!r} "
            "-- a broken compile must never present a new assembly as loaded"
        )

    nonce_b_during_break = await read_nonce(seams.call, config.port_b)
    check_sentinel_unchanged(config.nonce_b, nonce_b_during_break)
    mvid_b_during_break = await read_mvid(seams.call, config.port_b)
    check_sentinel_unchanged(mvid_b_before, mvid_b_during_break)

    await seams.repair_compile()
    repair_verdict = await seams.sync_unity(config.port_a, config.project_a)
    check_sync_clean_verdict(repair_verdict)

    nonce_a_after_repair = await read_nonce(seams.call, config.port_a)
    check_new_code_executed(config.repaired_nonce_a, nonce_a_after_repair, old_nonce_a)
    mvid_a_after_repair = await read_mvid(seams.call, config.port_a)
    if mvid_a_after_repair == mvid_a_before:
        raise ABReloadIdentityError("MVID unchanged after repair -- old assembly still loaded, not new code")

    nonce_b_after_repair = await read_nonce(seams.call, config.port_b)
    check_sentinel_unchanged(config.nonce_b, nonce_b_after_repair)
    mvid_b_after_repair = await read_mvid(seams.call, config.port_b)
    check_sentinel_unchanged(mvid_b_before, mvid_b_after_repair)

    t4_compile_error = {
        "old_nonce": old_nonce_a,
        "injected_nonce": config.injected_nonce_a,
        "mvid_before": mvid_a_before,
        "break_verdict": break_verdict,
        "nonce_during_break": nonce_a_during_break,
        "mvid_during_break": mvid_a_during_break,
        "repaired_nonce": config.repaired_nonce_a,
        "repair_verdict": repair_verdict,
        "nonce_after_repair": nonce_a_after_repair,
        "mvid_after_repair": mvid_a_after_repair,
    }
    t4_sentinel = {
        "nonce_b_before": nonce_b_before,
        "nonce_b_during_break": nonce_b_during_break,
        "nonce_b_after_repair": nonce_b_after_repair,
        "mvid_b_before": mvid_b_before,
        "mvid_b_during_break": mvid_b_during_break,
        "mvid_b_after_repair": mvid_b_after_repair,
    }
    return {"t4_compile_error": t4_compile_error, "t4_sentinel": t4_sentinel}
