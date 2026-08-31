"""P1-20/P1-30: structural, machine-readable off-mode evidence in the cell
receipt.

Reviewer verdict on P1-20: GO_WITH_GAPS. Gap #2: OFF/uninstall/package-
absent step evidence (§6 P0-80 steps 6-9) existed only as unstructured
unity.log text — the receipt's own claims never validated independently
(AI doc §10: "Exact-SHA evidence validates independently"). This is
sourced from scripts/fixtures/fsr_qualification/Editor/CycleInstrumentation.cs
(QueryOracle()'s pipe-delimited contract, domain-loads.jsonl's per-domain-
load record), already written into every cell's Unity worker Library/
folder during a real run but never previously read back by the driver.

Runs in the standard `scripts/tests` lane: no Unity, no network —
durable.call is monkeypatched; domain-loads.jsonl is a tmp_path fixture.
"""
import asyncio
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import run_fsr_qualification_cell as cell_script  # noqa: E402

# ---------------------------------------------------------------------------
# _query_oracle — one execute_code round trip to
# SourcePatchHarness.CycleInstrumentation.QueryOracle(), parsing its
# "key=value|key=value..." contract.
# ---------------------------------------------------------------------------

def test_query_oracle_parses_pipe_delimited_response(monkeypatch: pytest.MonkeyPatch):
    calls: list[tuple[str, dict]] = []

    async def _call(port, command, args):
        calls.append((command, args))
        return "instanceId=123|compute=3|implHash=456|stamp=abc|epoch=2|compiling=false|compileCount=1"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    oracle = asyncio.run(cell_script._query_oracle(9600))

    assert oracle == {
        "instanceId": "123", "compute": "3", "implHash": "456", "stamp": "abc",
        "epoch": "2", "compiling": "false", "compileCount": "1",
    }
    assert calls[0][0] == "execute_code"
    assert "QueryOracle" in calls[0][1]["code"]


def test_query_oracle_ignores_malformed_pairs(monkeypatch: pytest.MonkeyPatch):
    async def _call(port, command, args):
        return "compute=3|garbage-no-equals|epoch=1"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    oracle = asyncio.run(cell_script._query_oracle(9600))

    assert oracle == {"compute": "3", "epoch": "1"}


# ---------------------------------------------------------------------------
# _call_retrying_reload_race — Run 14 (33411347829): min-linux-x64 failed
# identically to Run 13 even after _query_oracle alone got a retry —
# on-mode-write-diagnostics.json again showed the full v1/v2/invalid/v3
# sequence already complete and no off-mode-evidence.json at all, meaning
# the SAME race can equally hit the disable call and the two legacy
# writes themselves (they trigger the very reload being raced), not only
# the oracle query that follows them. One shared retry wrapper closes the
# gap for every reload-adjacent durable.call in the off-mode phases, not
# just _query_oracle's own.
# ---------------------------------------------------------------------------

def test_call_retrying_reload_race_retries_on_transport_uncertain(monkeypatch: pytest.MonkeyPatch):
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        if calls["n"] < 3:
            raise cell_script.durable.TransportUncertain("going away")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_retrying_reload_race(9600, "editor", {"action": "mutation_mode"}, retry_delay=0.001)
    )

    assert result == "ok"
    assert calls["n"] == 3


def test_call_retrying_reload_race_raises_after_exhausting_retries(monkeypatch: pytest.MonkeyPatch):
    async def _call(port, command, args):
        raise cell_script.durable.TransportUncertain("going away")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    with pytest.raises(cell_script.durable.TransportUncertain):
        asyncio.run(
            cell_script._call_retrying_reload_race(9600, "editor", {}, retries=3, retry_delay=0.001)
        )


def test_call_retrying_reload_race_does_not_retry_on_a_genuine_runner_error(
    monkeypatch: pytest.MonkeyPatch,
):
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        raise cell_script.durable.RunnerError("boom")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    with pytest.raises(cell_script.durable.RunnerError, match="boom"):
        asyncio.run(cell_script._call_retrying_reload_race(9600, "editor", {}, retry_delay=0.001))

    assert calls["n"] == 1


def test_call_retrying_reload_race_retries_on_connection_refused(monkeypatch: pytest.MonkeyPatch):
    """Run 15 (33413847009): min-linux-x64 failed a third time, this time
    with a DIFFERENT exception entirely — "[Errno 111] Connection
    refused" (a raw ConnectionRefusedError/OSError from
    socket.create_connection inside durable.call's _call_sync, never
    wrapped as TransportUncertain at all). A domain reload can briefly
    close the TCP listener outright, not just delay a response — this
    codebase's own established retry loops (run_unity_tests.py) already
    catch (OSError, ConnectionError, asyncio.TimeoutError,
    TransportUncertain) together for exactly this reason; catching only
    TransportUncertain missed three of those four categories."""
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        if calls["n"] < 3:
            raise ConnectionRefusedError("[Errno 111] Connection refused")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_retrying_reload_race(9600, "editor", {}, retry_delay=0.001)
    )

    assert result == "ok"
    assert calls["n"] == 3


def test_call_retrying_reload_race_retries_on_asyncio_timeout(monkeypatch: pytest.MonkeyPatch):
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        if calls["n"] < 2:
            raise TimeoutError()
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_retrying_reload_race(9600, "editor", {}, retry_delay=0.001)
    )

    assert result == "ok"


def test_query_oracle_retries_on_transport_uncertain(monkeypatch: pytest.MonkeyPatch):
    """Run 13 (33410330964) min-linux-x64: the very first live oracle
    query right after triggering the disable-reload raced Unity's
    "going_away" announcement and failed the whole cell —
    "Unity announced domain reload before returning the response". This
    codebase's established convention (run_unity_tests.py's own retry
    semantics; AI doc: "timeout/reload disconnect is nonterminal") is that
    a reload disconnect during exactly the reload being waited for is
    expected, not a hard failure — querying right after triggering a
    reload is supposed to occasionally race it."""
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        if calls["n"] < 3:
            raise cell_script.durable.TransportUncertain("going away")
        return "compute=3|epoch=2"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    oracle = asyncio.run(cell_script._query_oracle(9600, retry_delay=0.001))

    assert oracle == {"compute": "3", "epoch": "2"}
    assert calls["n"] == 3


def test_query_oracle_raises_after_exhausting_retries_on_transport_uncertain(
    monkeypatch: pytest.MonkeyPatch,
):
    async def _call(port, command, args):
        raise cell_script.durable.TransportUncertain("going away")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    with pytest.raises(cell_script.durable.TransportUncertain):
        asyncio.run(cell_script._query_oracle(9600, retries=3, retry_delay=0.001))


def test_query_oracle_does_not_retry_on_a_genuine_runner_error(monkeypatch: pytest.MonkeyPatch):
    """Only the transient reload-race gets retried — a real failure (e.g.
    a bad command) must still fail fast, not be silently retried away."""
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        raise cell_script.durable.RunnerError("execute_code failed: compile error")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    with pytest.raises(cell_script.durable.RunnerError, match="compile error"):
        asyncio.run(cell_script._query_oracle(9600, retry_delay=0.001))

    assert calls["n"] == 1


# ---------------------------------------------------------------------------
# _wait_for_new_domain_load / _call_effect_expecting_reload_disconnect --
# Pareto correction (coordinator, after run 17): retrying an EFFECT
# command (mutation_mode disable, a legacy .cs write) is fundamentally
# wrong -- it violates this project's own "never retry an MCP call with
# identical arguments" rule -- and was the actual root cause of run 17's
# epoch/compileCount contamination, not a settle-timing race. Reload
# confirmation and every structural field now come from
# domain-loads.jsonl (already proven reliable for this in runs 6-12),
# never from live before/after oracle snapshots bracketing a retried
# effect call.
# ---------------------------------------------------------------------------

def test_call_effect_expecting_reload_disconnect_returns_normal_response(
    monkeypatch: pytest.MonkeyPatch,
):
    async def _call(port, command, args):
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_effect_expecting_reload_disconnect(9600, "editor", {"action": "mutation_mode"})
    )

    assert result == "mutation_mode:false"


def test_call_effect_expecting_reload_disconnect_treats_disconnect_as_expected_not_a_failure(
    monkeypatch: pytest.MonkeyPatch,
):
    """The whole point: a TransportUncertain/connection-refused/timeout
    right after an effect command IS that command's own reload — it must
    never be retried (this project's own "never retry with identical
    args" rule) and must never fail the call."""
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        raise cell_script.durable.TransportUncertain("going away")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_effect_expecting_reload_disconnect(9600, "editor", {"action": "mutation_mode"})
    )

    assert "reload-disconnect" in result
    assert calls["n"] == 1  # never retried


def test_call_effect_expecting_reload_disconnect_treats_connection_refused_as_expected(
    monkeypatch: pytest.MonkeyPatch,
):
    async def _call(port, command, args):
        raise ConnectionRefusedError("[Errno 111] Connection refused")

    monkeypatch.setattr(cell_script.durable, "call", _call)

    result = asyncio.run(
        cell_script._call_effect_expecting_reload_disconnect(9600, "asset", {"action": "write_text"})
    )

    assert "reload-disconnect" in result


def test_wait_for_new_domain_load_polls_until_a_new_record_appears(tmp_path: Path):
    path = tmp_path / "Library" / "UnityMCP" / "FsrQualificationCell" / "fsr-qualification" / "domain-loads.jsonl"
    path.parent.mkdir(parents=True)
    path.write_text('{"pid": 111, "epoch": 0}\n', encoding="utf-8")

    async def _append_after_delay():
        await asyncio.sleep(0.05)
        with path.open("a", encoding="utf-8") as handle:
            handle.write('{"pid": 111, "epoch": 1}\n')

    async def _run():
        appender = asyncio.ensure_future(_append_after_delay())
        records = await cell_script._wait_for_new_domain_load(
            tmp_path, after_count=1, timeout=2.0, poll_interval=0.01
        )
        await appender
        return records

    records = asyncio.run(_run())

    assert records == [{"pid": 111, "epoch": 1}]


def test_wait_for_new_domain_load_raises_when_nothing_new_appears_by_deadline(tmp_path: Path):
    path = tmp_path / "Library" / "UnityMCP" / "FsrQualificationCell" / "fsr-qualification" / "domain-loads.jsonl"
    path.parent.mkdir(parents=True)
    path.write_text('{"pid": 111, "epoch": 0}\n', encoding="utf-8")

    with pytest.raises(cell_script.FsrQualificationCellError, match="No new domain-load record"):
        asyncio.run(
            cell_script._wait_for_new_domain_load(tmp_path, after_count=1, timeout=0.02, poll_interval=0.01)
        )


def test_wait_for_new_domain_load_returns_multiple_records_if_more_than_one_appeared(tmp_path: Path):
    path = tmp_path / "Library" / "UnityMCP" / "FsrQualificationCell" / "fsr-qualification" / "domain-loads.jsonl"
    path.parent.mkdir(parents=True)
    path.write_text(
        '{"pid": 111, "epoch": 0}\n{"pid": 111, "epoch": 1}\n{"pid": 111, "epoch": 2}\n',
        encoding="utf-8",
    )

    records = asyncio.run(
        cell_script._wait_for_new_domain_load(tmp_path, after_count=1, timeout=1.0, poll_interval=0.01)
    )

    assert records == [{"pid": 111, "epoch": 1}, {"pid": 111, "epoch": 2}]

# ---------------------------------------------------------------------------
# _read_domain_loads — reads Library/UnityMCP/FsrQualificationCell/
# fsr-qualification/domain-loads.jsonl, written by CycleInstrumentation's
# [InitializeOnLoad] static constructor on every domain load.
# ---------------------------------------------------------------------------

def _domain_loads_path(project: Path) -> Path:
    return project / "Library" / "UnityMCP" / "FsrQualificationCell" / "fsr-qualification" / "domain-loads.jsonl"


def test_read_domain_loads_parses_jsonl(tmp_path: Path):
    path = _domain_loads_path(tmp_path)
    path.parent.mkdir(parents=True)
    path.write_text(
        '{"pid": 111, "epoch": 1, "assemblies": []}\n'
        '{"pid": 111, "epoch": 2, "assemblies": [{"name": "Harmony"}]}\n',
        encoding="utf-8",
    )

    records = cell_script._read_domain_loads(tmp_path)

    assert len(records) == 2
    assert records[0]["pid"] == 111
    assert records[1]["assemblies"] == [{"name": "Harmony"}]


def test_read_domain_loads_returns_empty_list_when_file_absent(tmp_path: Path):
    assert cell_script._read_domain_loads(tmp_path) == []


def test_read_domain_loads_skips_malformed_lines(tmp_path: Path):
    path = _domain_loads_path(tmp_path)
    path.parent.mkdir(parents=True)
    path.write_text('{"pid": 1}\nnot json\n{"pid": 2}\n', encoding="utf-8")

    records = cell_script._read_domain_loads(tmp_path)

    assert [r["pid"] for r in records] == [1, 2]


# ---------------------------------------------------------------------------
# _manifest_matches_pre_pin — step 7 (§6 P0-80): "remove optional package
# offline only after Off/zero lease" — proves the manifest is restored
# exactly, not merely "no exception during rewrite_manifest_pin".
# ---------------------------------------------------------------------------

def test_manifest_matches_pre_pin_true_when_identical(tmp_path: Path):
    manifest = tmp_path / "Packages" / "manifest.json"
    manifest.parent.mkdir(parents=True)
    manifest.write_text('{"dependencies": {}}', encoding="utf-8")

    assert cell_script._manifest_matches_pre_pin(tmp_path, '{"dependencies": {}}') is True


def test_manifest_matches_pre_pin_false_when_different(tmp_path: Path):
    manifest = tmp_path / "Packages" / "manifest.json"
    manifest.parent.mkdir(parents=True)
    manifest.write_text('{"dependencies": {"x": "1.0.0"}}', encoding="utf-8")

    assert cell_script._manifest_matches_pre_pin(tmp_path, '{"dependencies": {}}') is False


# ---------------------------------------------------------------------------
# _clear_domain_loads_evidence — Run 16 (33416142578): both required cells
# failed step 6's same_pid check deterministically, 100% of the time —
# CycleInstrumentation.cs's [InitializeOnLoad] static constructor only
# ever appends to domain-loads.jsonl (File.AppendAllText), never clears
# it, and run_full's own worker directory (Library/) persists across ALL
# of a cell's launches (steps1-2, steps4-6, steps8-9 all share the same
# `project` path) — so by the time steps4-6 reaches step 6, the file
# already carries at least one record from steps1-2's own, entirely
# separate Unity process (a different pid). Clearing it immediately
# before each launch whose own evidence matters (steps4-6, steps8-9)
# scopes _read_domain_loads to that launch alone.
# ---------------------------------------------------------------------------

def test_clear_domain_loads_evidence_removes_existing_file(tmp_path: Path):
    path = cell_script.Path(tmp_path) / cell_script.DOMAIN_LOADS_REL
    path.parent.mkdir(parents=True)
    path.write_text('{"pid": 1}\n', encoding="utf-8")

    cell_script._clear_domain_loads_evidence(tmp_path)

    assert not path.exists()


def test_clear_domain_loads_evidence_noop_when_absent(tmp_path: Path):
    cell_script._clear_domain_loads_evidence(tmp_path)  # must not raise


def test_manifest_matches_pre_pin_false_when_missing(tmp_path: Path):
    assert cell_script._manifest_matches_pre_pin(tmp_path, '{"dependencies": {}}') is False


# ---------------------------------------------------------------------------
# _phase_off_disable_evidence / _phase_final_restore — steps 6 and 8-9
# (§6 P0-80). File-oracle design (coordinator correction after run 17):
# the disable/write EFFECT commands are each called exactly once, never
# retried; the reload and every structural field come from
# domain-loads.jsonl records, hermetically constructed here as fixtures
# -- never from a live before/after oracle sequence bracketing a
# possibly-retried effect call. The only live oracle call left in either
# phase is a single, read-only query for `compute` after the reload is
# already confirmed via the file.
# ---------------------------------------------------------------------------

def _write_domain_loads(project: Path, records: list[dict]) -> None:
    path = _domain_loads_path(project)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(json.dumps(r) for r in records) + "\n", encoding="utf-8")


def _append_domain_load(project: Path, record: dict) -> None:
    path = _domain_loads_path(project)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record) + "\n")


def _oracle_sequence(monkeypatch: pytest.MonkeyPatch, snapshots: list[dict]) -> None:
    it = iter(snapshots)

    async def _fake(port):
        return next(it)

    monkeypatch.setattr(cell_script, "_query_oracle", _fake)


def test_phase_off_disable_evidence_happy_path(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])

    async def _call(port, command, args):
        assert command == "editor" and args == {"action": "mutation_mode", "enable": False}
        _append_domain_load(tmp_path, {"pid": 111, "epoch": 1, "compileStartedCount": 1})
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])

    evidence = asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert evidence["disable_result"] == "mutation_mode:false"
    assert evidence["epoch_before"] == 0
    assert evidence["epoch_after"] == 1
    assert evidence["epoch_delta"] == 1
    assert evidence["epoch_delta_is_one"] is True
    assert evidence["compile_started_count"] == 1
    assert evidence["compile_started_count_is_one"] is True
    assert evidence["compute_after_disable"] == "3"
    assert evidence["compute_after_disable_is_3"] is True
    assert evidence["same_pid"] is True
    assert evidence["editor_pid"] == 111


def test_phase_off_disable_evidence_never_retries_the_disable_call(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """The whole point of the redesign: a disconnect right after the
    disable call is that call's own reload, never a reason to resend it."""
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        _append_domain_load(tmp_path, {"pid": 111, "epoch": 1, "compileStartedCount": 1})
        raise cell_script.durable.TransportUncertain("going away")

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])

    evidence = asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert calls["n"] == 1
    assert "reload-disconnect" in evidence["disable_result"]
    assert evidence["epoch_delta_is_one"] is True


def test_phase_off_disable_evidence_raises_when_epoch_delta_is_not_one(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])

    async def _call(port, command, args):
        _append_domain_load(tmp_path, {"pid": 111, "epoch": 2, "compileStartedCount": 1})  # +2, not +1
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert exc_info.value.off_mode_evidence["epoch_delta_is_one"] is False


def test_phase_off_disable_evidence_raises_when_compute_is_not_3(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])

    async def _call(port, command, args):
        _append_domain_load(tmp_path, {"pid": 111, "epoch": 1, "compileStartedCount": 1})
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "2"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert exc_info.value.off_mode_evidence["compute_after_disable_is_3"] is False


def test_phase_off_disable_evidence_raises_when_pid_changed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])

    async def _call(port, command, args):
        _append_domain_load(tmp_path, {"pid": 222, "epoch": 1, "compileStartedCount": 1})  # respawned
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert exc_info.value.off_mode_evidence["same_pid"] is False


def test_phase_off_disable_evidence_raises_when_compile_started_count_is_not_one(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    _write_domain_loads(tmp_path, [{"pid": 111, "epoch": 0, "compileStartedCount": 0}])

    async def _call(port, command, args):
        _append_domain_load(tmp_path, {"pid": 111, "epoch": 1, "compileStartedCount": 2})
        return "mutation_mode:false"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_off_disable_evidence(port=9600, project=tmp_path))

    assert exc_info.value.off_mode_evidence["compile_started_count_is_one"] is False


# ---------------------------------------------------------------------------
# _phase_final_restore — steps 8-9 (§6 P0-80): "8. fresh package-absent
# Editor: provider assemblies absent, base compile clean, another legacy
# `.cs` write/reload; 9. exact source/meta/mtime/project settings
# restoration." Two writes within the same fresh launch: v4 first (proves
# legacy compile still works cleanly with the package gone, and
# CycleInstrumentation's own denylist scan of AppDomain assemblies
# confirms none of the provider's names are loaded), then v0 (restores the
# pristine baseline, verified by sha256 — not merely "no exception").
# ---------------------------------------------------------------------------

def test_phase_final_restore_happy_path(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    target = tmp_path / "target.cs"
    write_calls: list[tuple[str, dict]] = []

    async def _call(port, command, args):
        write_calls.append((command, args))
        target.write_bytes(args["content"].encode("utf-8"))
        if "return 4" in args["content"]:
            _append_domain_load(tmp_path, {"pid": 111, "assemblies": [{"name": "UnityEngine.CoreModule"}]})
        else:
            _append_domain_load(tmp_path, {"pid": 111, "assemblies": []})
        return "ok:" + args["content"][:10]

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "4"}])

    evidence = asyncio.run(
        cell_script._phase_final_restore(port=9600, project=tmp_path, target_path=target)
    )

    assert len(write_calls) == 2
    assert cell_script.harness.target_body("v4") in write_calls[0][1]["content"]
    assert cell_script.harness.target_body("v0") in write_calls[1][1]["content"]
    assert evidence["compute_after_legacy_write"] == "4"
    assert evidence["compute_after_legacy_write_is_4"] is True
    assert evidence["assembly_needles_found"] == []
    assert evidence["assembly_needles_absent"] is True
    assert evidence["restore_sha_matches"] is True
    assert evidence["restored_sha256"] == evidence["pristine_sha256"]


def test_phase_final_restore_never_retries_the_write_calls(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    target = tmp_path / "target.cs"
    calls = {"n": 0}

    async def _call(port, command, args):
        calls["n"] += 1
        target.write_bytes(args["content"].encode("utf-8"))
        _append_domain_load(tmp_path, {"pid": 111, "assemblies": []})
        raise ConnectionRefusedError("[Errno 111] Connection refused")

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "4"}])

    evidence = asyncio.run(
        cell_script._phase_final_restore(port=9600, project=tmp_path, target_path=target)
    )

    assert calls["n"] == 2  # exactly one attempt per write, never retried
    assert "reload-disconnect" in evidence["legacy_write_result"]
    assert "reload-disconnect" in evidence["restore_write_result"]


def test_phase_final_restore_raises_when_denylist_assembly_present(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    target = tmp_path / "target.cs"

    async def _call(port, command, args):
        target.write_bytes(args["content"].encode("utf-8"))
        _append_domain_load(tmp_path, {"pid": 111, "assemblies": [{"name": "0Harmony"}]})
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "4"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_final_restore(port=9600, project=tmp_path, target_path=target))

    assert exc_info.value.off_mode_evidence["assembly_needles_absent"] is False
    assert "0Harmony" in exc_info.value.off_mode_evidence["assembly_needles_found"]


def test_phase_final_restore_raises_when_compute_after_legacy_write_is_not_4(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    target = tmp_path / "target.cs"

    async def _call(port, command, args):
        target.write_bytes(args["content"].encode("utf-8"))
        _append_domain_load(tmp_path, {"pid": 111, "assemblies": []})
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "3"}])  # stale — legacy write did not take effect

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_final_restore(port=9600, project=tmp_path, target_path=target))

    assert exc_info.value.off_mode_evidence["compute_after_legacy_write_is_4"] is False


def test_phase_final_restore_raises_when_restored_content_does_not_match_pristine(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    target = tmp_path / "target.cs"

    async def _call(port, command, args):
        if "return 0" not in args["content"]:
            target.write_bytes(args["content"].encode("utf-8"))
        else:
            target.write_bytes(b"corrupted, not the pristine v0 content")
        _append_domain_load(tmp_path, {"pid": 111, "assemblies": []})
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    _oracle_sequence(monkeypatch, [{"compute": "4"}])

    with pytest.raises(cell_script.FsrQualificationCellError) as exc_info:
        asyncio.run(cell_script._phase_final_restore(port=9600, project=tmp_path, target_path=target))

    assert exc_info.value.off_mode_evidence["restore_sha_matches"] is False



# ---------------------------------------------------------------------------
# run_full integration — proves the real (non-stubbed) phase functions wire
# together correctly end-to-end: off_mode_evidence assembled across steps
# 6/7/8-9 lands in receipt.json exactly as build_runtime_receipt embeds it.
# ---------------------------------------------------------------------------

def _lock_and_pin(tmp_path: Path) -> tuple[Path, Path]:
    lock_path = tmp_path / "lock.json"
    lock_path.write_text(
        '{"base_product_sha": "' + "a" * 40 + '", "final_fsr_adapter_sha": "' + "b" * 40
        + '", "cells": {"u_min": {"unity_version": "6000.0.65f1", '
        '"unity_revision": "a18e2220bd50", "utf_version": "1.6.0"}, '
        '"u_max": {"unity_version": "6000.5.10f1", "unity_revision": "3bd4f66ad299", '
        '"utf_version": "1.6.0"}}}',
        encoding="utf-8",
    )
    pin_path = tmp_path / "pin.json"
    pin_path.write_text("{}", encoding="utf-8")
    return lock_path, pin_path


class _FakeProcessIntegration:
    def poll(self):
        return None


def test_run_full_embeds_off_mode_evidence_in_receipt_end_to_end(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    project = tmp_path / "work" / "worker"

    def _create_worker_stub(source_project, dest, **k):
        (dest / "Packages").mkdir(parents=True, exist_ok=True)
        (dest / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)

    def _launch_stub(**k):
        # Simulates Unity's own cold-boot domain-load record for whichever
        # fresh launch this is -- in a real run this always exists by the
        # time _phase_off_disable_evidence/_phase_final_restore start
        # (CycleInstrumentation's [InitializeOnLoad] fires on cold start
        # too); _clear_domain_loads_evidence wipes any prior launch's
        # record right before this call, so this becomes the sole
        # "previous" baseline for the launch that follows.
        _append_domain_load(project, {"pid": 111, "epoch": 0, "compileStartedCount": 0})
        return _FakeProcessIntegration()

    monkeypatch.setattr(cell_script, "_launch", _launch_stub)
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    monkeypatch.setattr(cell_script, "_git_head_sha", lambda: "a" * 40)
    monkeypatch.setattr(cell_script, "_git_changed_paths", lambda base_sha: [])

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)

    # Exactly two live oracle reads across the whole off-mode flow now:
    # one for step 6's compute check, one for step 8's (after the v4 write).
    oracle_responses = iter(["compute=3", "compute=4"])

    async def _call(port, command, args):
        if command == "execute_code":
            return next(oracle_responses)
        if command == "editor" and args.get("action") == "mutation_mode" and args.get("enable") is False:
            _append_domain_load(project, {"pid": 111, "epoch": 1, "compileStartedCount": 1})
            return "mutation_mode:false"
        if command == "asset" and args.get("action") == "write_text":
            (project / cell_script.REL_TARGET).parent.mkdir(parents=True, exist_ok=True)
            (project / cell_script.REL_TARGET).write_bytes(args["content"].encode("utf-8"))
            _append_domain_load(project, {"pid": 111, "assemblies": []})
            return "ok"
        if command == "source_patch_write":
            if "System.Func<int>" in args.get("content", ""):
                raise cell_script.durable.RunnerError(
                    "source_patch_write failed: STATE: source patch rejected the "
                    "replacement body; no effect"
                )
            (project / cell_script.REL_TARGET).parent.mkdir(parents=True, exist_ok=True)
            (project / cell_script.REL_TARGET).write_bytes(args["content"].encode("utf-8"))
            return "ok"
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    lock_path, pin_path = _lock_and_pin(tmp_path)

    receipt = asyncio.run(
        cell_script.run_full(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            window="u_min",
            lock_path=lock_path,
            provider_pin=pin_path,
            evidence_out=tmp_path / "evidence",
            port=9600,
            startup_timeout=1.0,
            cell_name="min-linux-x64",
            os_name="Linux",
            arch="x64",
        )
    )

    assert receipt["outcome"] == "PASS"
    evidence = receipt["off_mode_evidence"]
    assert evidence["step6_disable"]["epoch_delta_is_one"] is True
    assert evidence["step6_disable"]["compute_after_disable_is_3"] is True
    assert evidence["step6_disable"]["same_pid"] is True
    assert evidence["step7_manifest_restore"]["manifest_matches_pre_pin"] is True
    assert evidence["step8_9_final_restore"]["compute_after_legacy_write_is_4"] is True
    assert evidence["step8_9_final_restore"]["assembly_needles_absent"] is True
    assert evidence["step8_9_final_restore"]["restore_sha_matches"] is True

    on_disk_receipt = json.loads((tmp_path / "evidence" / "receipt.json").read_text(encoding="utf-8"))
    assert on_disk_receipt["off_mode_evidence"]["step6_disable"]["same_pid"] is True

    off_mode_file = json.loads((tmp_path / "evidence" / "off-mode-evidence.json").read_text(encoding="utf-8"))
    assert off_mode_file["step7_manifest_restore"]["manifest_matches_pre_pin"] is True


def test_run_full_fails_and_records_evidence_when_manifest_restore_is_wrong(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Step 7 restoring the wrong manifest content must fail the cell, not
    just be silently noted — matches the fail-fast pattern every other
    off-mode check already uses."""
    project = tmp_path / "work" / "worker"

    def _create_worker_stub(source_project, dest, **k):
        (dest / "Packages").mkdir(parents=True, exist_ok=True)
        (dest / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")

    def _corrupt_manifest_pin(dest, pin_path, *, install):
        if not install:
            (dest / "Packages" / "manifest.json").write_text('{"dependencies": {"stray": "1.0.0"}}', encoding="utf-8")

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", _corrupt_manifest_pin)
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)

    def _launch_stub(**k):
        _append_domain_load(project, {"pid": 111, "epoch": 0, "compileStartedCount": 0})
        return _FakeProcessIntegration()

    monkeypatch.setattr(cell_script, "_launch", _launch_stub)
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    monkeypatch.setattr(cell_script, "_git_head_sha", lambda: "a" * 40)
    monkeypatch.setattr(cell_script, "_git_changed_paths", lambda base_sha: [])

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)

    oracle_responses = iter(["compute=3"])

    async def _call(port, command, args):
        if command == "execute_code":
            return next(oracle_responses)
        if command == "editor" and args.get("action") == "mutation_mode" and args.get("enable") is False:
            _append_domain_load(project, {"pid": 111, "epoch": 1, "compileStartedCount": 1})
            return "mutation_mode:false"
        if command in ("asset", "source_patch_write"):
            if "System.Func<int>" in args.get("content", ""):
                raise cell_script.durable.RunnerError(
                    "source_patch_write failed: STATE: source patch rejected the "
                    "replacement body; no effect"
                )
            (project / cell_script.REL_TARGET).parent.mkdir(parents=True, exist_ok=True)
            (project / cell_script.REL_TARGET).write_bytes(args["content"].encode("utf-8"))
            return "ok"
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    lock_path, pin_path = _lock_and_pin(tmp_path)

    with pytest.raises(cell_script.FsrQualificationCellError, match="manifest"):
        asyncio.run(
            cell_script.run_full(
                unity=tmp_path / "Unity",
                source_project=tmp_path / "source",
                work_root=tmp_path / "work",
                window="u_min",
                lock_path=lock_path,
                provider_pin=pin_path,
                evidence_out=tmp_path / "evidence",
                port=9600,
                startup_timeout=1.0,
                cell_name="min-linux-x64",
                os_name="Linux",
                arch="x64",
            )
        )

    on_disk_receipt = json.loads((tmp_path / "evidence" / "receipt.json").read_text(encoding="utf-8"))
    assert on_disk_receipt["outcome"] == "FAIL"
    assert on_disk_receipt["off_mode_evidence"]["step7_manifest_restore"]["manifest_matches_pre_pin"] is False


def test_run_full_clears_domain_loads_evidence_before_steps4_6_and_steps8_9_launches(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Regression for the same_pid cross-launch-contamination bug: clearing
    must happen immediately before EACH of the two launches whose own
    off-mode evidence matters (steps4-6, steps8-9), not just once."""
    order: list[str] = []

    def _create_worker_stub(source_project, dest, **k):
        (dest / "Packages").mkdir(parents=True, exist_ok=True)
        (dest / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)
    monkeypatch.setattr(
        cell_script, "_launch",
        lambda **k: order.append("_launch") or _FakeProcessIntegration(),
    )
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    monkeypatch.setattr(cell_script, "_git_head_sha", lambda: "a" * 40)
    monkeypatch.setattr(cell_script, "_git_changed_paths", lambda base_sha: [])
    monkeypatch.setattr(
        cell_script, "_clear_domain_loads_evidence",
        lambda project: order.append("_clear_domain_loads_evidence"),
    )

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)

    async def _off_disable_stub(*, port, project):
        return {"epoch_delta_is_one": True}

    monkeypatch.setattr(cell_script, "_phase_off_disable_evidence", _off_disable_stub)
    monkeypatch.setattr(cell_script, "_manifest_matches_pre_pin", lambda project, pre_pin: True)

    async def _final_restore_stub(*, port, project, target_path):
        return {"restore_sha_matches": True}

    monkeypatch.setattr(cell_script, "_phase_final_restore", _final_restore_stub)

    async def _call(port, command, args):
        if "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError(
                "source_patch_write failed: STATE: source patch rejected the "
                "replacement body; no effect"
            )
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    lock_path, pin_path = _lock_and_pin(tmp_path)

    asyncio.run(
        cell_script.run_full(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            window="u_min",
            lock_path=lock_path,
            provider_pin=pin_path,
            evidence_out=tmp_path / "evidence",
            port=9600,
            startup_timeout=1.0,
            cell_name="min-linux-x64",
            os_name="Linux",
            arch="x64",
        )
    )

    # steps1-2's launch has no preceding clear (its own evidence is never
    # read); steps4-6 and steps8-9 each clear immediately before their launch.
    assert order == [
        "_launch",
        "_clear_domain_loads_evidence", "_launch",
        "_clear_domain_loads_evidence", "_launch",
    ]


def test_run_full_captures_error_and_evidence_when_phase_off_disable_evidence_raises(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Run 16 (33416142578): both required cells failed with "FAILED:
    step 6 off-mode evidence check(s) failed: [...]" printed to stderr by
    main()'s OUTER except tuple, but receipt.json had no "error" key and
    no off-mode-evidence.json was written at all — run_full's OWN except
    tuple listed fq.FsrQualificationError (gauntlet.fsr_qualification's
    class) but never FsrQualificationCellError (run_fsr_qualification_cell's
    own, completely unrelated class despite the similar name) — the
    exact class every off-mode phase function raises. The exception
    skipped run_full's error_message/off_mode_evidence capture entirely,
    only being caught much later, one level up. Every prior test of this
    failure path happened to set off_mode_evidence directly in run_full's
    own try block before raising (step 7's manifest check) rather than
    relying on the except block's getattr(error, "off_mode_evidence", ...)
    merge — never exercising this exact gap."""
    project = tmp_path / "work" / "worker"

    def _create_worker_stub(source_project, dest, **k):
        (dest / "Packages").mkdir(parents=True, exist_ok=True)
        (dest / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)

    def _launch_stub(**k):
        _append_domain_load(project, {"pid": 111, "epoch": 0, "compileStartedCount": 0})
        return _FakeProcessIntegration()

    monkeypatch.setattr(cell_script, "_launch", _launch_stub)
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    monkeypatch.setattr(cell_script, "_git_head_sha", lambda: "a" * 40)
    monkeypatch.setattr(cell_script, "_git_changed_paths", lambda base_sha: [])

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)

    oracle_responses = iter(["compute=3"])

    async def _call(port, command, args):
        if command == "execute_code":
            return next(oracle_responses)
        if command == "editor" and args.get("action") == "mutation_mode" and args.get("enable") is False:
            _append_domain_load(project, {"pid": 111, "epoch": 2, "compileStartedCount": 1})  # +2, not +1
            return "mutation_mode:false"
        if command in ("asset", "source_patch_write"):
            if "System.Func<int>" in args.get("content", ""):
                raise cell_script.durable.RunnerError(
                    "source_patch_write failed: STATE: source patch rejected the "
                    "replacement body; no effect"
                )
            (project / cell_script.REL_TARGET).parent.mkdir(parents=True, exist_ok=True)
            (project / cell_script.REL_TARGET).write_bytes(args["content"].encode("utf-8"))
            return "ok"
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    lock_path, pin_path = _lock_and_pin(tmp_path)

    with pytest.raises(cell_script.FsrQualificationCellError, match="step 6"):
        asyncio.run(
            cell_script.run_full(
                unity=tmp_path / "Unity",
                source_project=tmp_path / "source",
                work_root=tmp_path / "work",
                window="u_min",
                lock_path=lock_path,
                provider_pin=pin_path,
                evidence_out=tmp_path / "evidence",
                port=9600,
                startup_timeout=1.0,
                cell_name="min-linux-x64",
                os_name="Linux",
                arch="x64",
            )
        )

    on_disk_receipt = json.loads((tmp_path / "evidence" / "receipt.json").read_text(encoding="utf-8"))
    assert on_disk_receipt["outcome"] == "FAIL"
    assert "step 6" in on_disk_receipt["error"]
    assert on_disk_receipt["off_mode_evidence"]["step6_disable"]["epoch_delta_is_one"] is False
    assert on_disk_receipt["off_mode_evidence"]["step6_disable"]["same_pid"] is True

    off_mode_file = json.loads((tmp_path / "evidence" / "off-mode-evidence.json").read_text(encoding="utf-8"))
    assert off_mode_file["step6_disable"]["epoch_delta"] == 2
