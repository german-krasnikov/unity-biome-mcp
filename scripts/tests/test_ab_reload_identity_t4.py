"""Offline tests for gauntlet.ab_reload_compile_recovery.run_t4 (N3 T4:
compile-error recovery). No Unity, no network -- fake call/sync/break/repair
seams only. Covers: break -> FAIL:CS-shaped verdict, old nonce still
executes (never the injected-but-uncompiled one), A's MVID unchanged while
broken, B untouched; repair -> sync clean, the repaired nonce executes, A's
MVID changes, B still untouched. See
Plans/N3-T3-T4-live-reload-identity.md Part 2.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import gauntlet.ab_reload_compile_recovery as t4  # noqa: E402

PORT_A, PORT_B = 9620, 9630
PROJECT_A = "/tmp/worker-a"
OLD_NONCE, INJECTED_NONCE, REPAIRED_NONCE, NONCE_B = "A-old", "A-broken", "A-repaired", "B-fixed"
MVID_A_BEFORE, MVID_A_AFTER, MVID_B = "MVID-A0", "MVID-A1", "MVID-B0"


def diagnose_text(mvid: str) -> str:
    return f"mvid={mvid}\ncompile=idle\n"


class ScriptedCall:
    def __init__(self, script: dict[tuple, list[str]]) -> None:
        self._script = {key: list(values) for key, values in script.items()}
        self.log: list[tuple] = []

    async def __call__(self, port: int, cmd: str, args: dict) -> str:
        key = (port, cmd, args.get("code", ""))
        self.log.append(("call", *key))
        queue = self._script.get(key)
        if not queue:
            raise AssertionError(f"no scripted response left for {key}")
        return queue.pop(0)


class ScriptedSync:
    def __init__(self, verdicts: list[str]) -> None:
        self._verdicts = list(verdicts)
        self.log: list[tuple] = []

    async def __call__(self, port: int, project_path: str) -> str:
        self.log.append(("sync", port, project_path))
        if not self._verdicts:
            raise AssertionError("no scripted sync verdict left")
        return self._verdicts.pop(0)


def make_action(log: list[str], name: str):
    async def run() -> None:
        log.append(name)
    return run


def make_config(**overrides: object) -> t4.T4Config:
    fields = {
        "port_a": PORT_A, "port_b": PORT_B, "project_a": PROJECT_A,
        "injected_nonce_a": INJECTED_NONCE, "repaired_nonce_a": REPAIRED_NONCE, "nonce_b": NONCE_B,
    }
    fields.update(overrides)
    return t4.T4Config(**fields)


def make_seams(
    *, break_verdict: str = "FAIL:CS1002", repair_verdict: str = "sync clean",
    nonce_a_during_break: str = OLD_NONCE, mvid_a_during_break: str = MVID_A_BEFORE,
    nonce_a_after_repair: str = REPAIRED_NONCE, mvid_a_after_repair: str = MVID_A_AFTER,
    nonce_b_during_break: str = NONCE_B, nonce_b_after_repair: str = NONCE_B,
) -> tuple[t4.T4Seams, ScriptedCall, ScriptedSync, list[str]]:
    from gauntlet.ab_reload_identity import NONCE_READ_CODE

    script = {
        (PORT_A, "execute_code", NONCE_READ_CODE): [OLD_NONCE, nonce_a_during_break, nonce_a_after_repair],
        (PORT_A, "diagnose", ""): [
            diagnose_text(MVID_A_BEFORE), diagnose_text(mvid_a_during_break), diagnose_text(mvid_a_after_repair),
        ],
        (PORT_B, "execute_code", NONCE_READ_CODE): [NONCE_B, nonce_b_during_break, nonce_b_after_repair],
        (PORT_B, "diagnose", ""): [diagnose_text(MVID_B), diagnose_text(MVID_B), diagnose_text(MVID_B)],
    }
    call = ScriptedCall(script)
    sync = ScriptedSync([break_verdict, repair_verdict])
    actions: list[str] = []
    seams = t4.T4Seams(
        call=call, sync_unity=sync,
        break_compile=make_action(actions, "break"), repair_compile=make_action(actions, "repair"),
    )
    return seams, call, sync, actions


# --- happy path ---------------------------------------------------------


@pytest.mark.asyncio
async def test_run_t4_happy_path() -> None:
    seams, _, _, actions = make_seams()
    result = await t4.run_t4(make_config(), seams)
    error = result["t4_compile_error"]
    assert error["break_verdict"] == "FAIL:CS1002"
    assert error["nonce_during_break"] == OLD_NONCE
    assert error["repair_verdict"] == "sync clean"
    assert error["nonce_after_repair"] == REPAIRED_NONCE
    assert error["mvid_before"] == MVID_A_BEFORE
    assert error["mvid_after_repair"] == MVID_A_AFTER
    sentinel = result["t4_sentinel"]
    assert sentinel["nonce_b_before"] == NONCE_B
    assert sentinel["nonce_b_during_break"] == NONCE_B
    assert sentinel["nonce_b_after_repair"] == NONCE_B
    assert actions == ["break", "repair"], "break must run before repair, exactly once each"


# --- break phase negative controls ---------------------------------------


@pytest.mark.asyncio
async def test_run_t4_raises_when_break_verdict_is_not_a_failure() -> None:
    seams, *_ = make_seams(break_verdict="sync clean")
    with pytest.raises(t4.ABReloadIdentityError, match="expected a compile-failure verdict"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_injected_nonce_presented_during_break() -> None:
    """The never-compiled nonce must be unreachable while compilation is broken."""
    seams, *_ = make_seams(nonce_a_during_break=INJECTED_NONCE)
    with pytest.raises(t4.ABReloadIdentityError, match="presented while compilation is broken"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_mvid_changes_during_break() -> None:
    seams, *_ = make_seams(mvid_a_during_break="MVID-SNUCK-IN")
    with pytest.raises(t4.ABReloadIdentityError, match="MVID changed during a compile break"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_b_drifts_during_break() -> None:
    seams, *_ = make_seams(nonce_b_during_break="B-MUTATED")
    with pytest.raises(t4.ABReloadIdentityError, match="B sentinel changed"):
        await t4.run_t4(make_config(), seams)


# --- repair phase negative controls ---------------------------------------


@pytest.mark.asyncio
async def test_run_t4_raises_when_repair_verdict_not_clean() -> None:
    seams, *_ = make_seams(repair_verdict="FAIL:CS1002")
    with pytest.raises(t4.ABReloadIdentityError, match="expected a clean sync verdict"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_repaired_nonce_not_executed() -> None:
    seams, *_ = make_seams(nonce_a_after_repair=OLD_NONCE)
    with pytest.raises(t4.ABReloadIdentityError, match="old code presented as new"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_mvid_unchanged_after_repair() -> None:
    seams, *_ = make_seams(mvid_a_after_repair=MVID_A_BEFORE)
    with pytest.raises(t4.ABReloadIdentityError, match="MVID unchanged after repair"):
        await t4.run_t4(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t4_raises_when_b_drifts_after_repair() -> None:
    seams, *_ = make_seams(nonce_b_after_repair="B-MUTATED-LATE")
    with pytest.raises(t4.ABReloadIdentityError, match="B sentinel changed"):
        await t4.run_t4(make_config(), seams)


# --- standalone oracle/helper functions -----------------------------------


def test_is_compile_failure_verdict_recognizes_all_shapes() -> None:
    assert t4.is_compile_failure_verdict("FAIL:CS1002")
    assert t4.is_compile_failure_verdict("FAIL:CS0103")
    assert t4.is_compile_failure_verdict("compile failed: details unavailable")
    assert not t4.is_compile_failure_verdict("FAIL:stale-dll")
    assert not t4.is_compile_failure_verdict("FAIL:unknown")
    assert not t4.is_compile_failure_verdict("sync clean")
    assert not t4.is_compile_failure_verdict("sync clean (no compile needed)")


def test_check_new_code_unavailable_during_break_passes_when_observed_is_old() -> None:
    t4.check_new_code_unavailable_during_break(INJECTED_NONCE, OLD_NONCE, OLD_NONCE)  # no raise


def test_check_new_code_unavailable_during_break_raises_on_unrelated_nonce() -> None:
    with pytest.raises(t4.ABReloadIdentityError, match="unexpected nonce during compile break"):
        t4.check_new_code_unavailable_during_break(INJECTED_NONCE, "SOMETHING-ELSE", OLD_NONCE)
