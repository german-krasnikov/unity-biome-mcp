"""A6a: mutation regression lane cell driver (scripts/run_mutation_regression_cell.py)
and its pure helpers (scripts/gauntlet/mutation_regression.py).

Runs in the standard `scripts/tests` lane: no Unity, no network -- every
Unity-facing/subprocess call is monkeypatched. See
Plans/MUTATION-REGRESSION-MODULE.md section 8.
"""
import asyncio
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import gauntlet.mutation_regression as regression  # noqa: E402
import run_mutation_regression_cell as cell_script  # noqa: E402

SHA_A = "a" * 40
SHA_B = "b90a5c3fd7cfa452f23e8a807cc7bd61dc934bbf"
SHA_C = "c" * 40


class _FakeProcess:
    def poll(self):
        return None


def _write_pin(path: Path, *, ref: str, ref_kind: str | None = None) -> None:
    payload = {
        "package_name": "com.handzlikchris.fastscriptreload",
        "git_url": "https://example.invalid/fsr.git",
        "ref": ref,
    }
    if ref_kind is not None:
        payload["ref_kind"] = ref_kind
    path.write_text(json.dumps(payload), encoding="utf-8")


def _stub_happy_launch(monkeypatch: pytest.MonkeyPatch) -> list:
    """Stubs every phase up through a clean-compile launch. Returns the
    list `terminate_workers` calls land in, so callers can assert cleanup
    ran even on a later-phase failure."""

    def _create_worker_stub(source, dest, **kwargs):
        (dest / "Packages").mkdir(parents=True, exist_ok=True)
        (dest / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")
        (dest / "Packages" / "packages-lock.json").write_text(
            json.dumps({"dependencies": {"com.handzlikchris.fastscriptreload": {"hash": "abc123"}}}),
            encoding="utf-8",
        )

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script, "_apply_preseed", lambda *a, **k: {"applied": True})
    monkeypatch.setattr(cell_script, "_launch", lambda **k: _FakeProcess())
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)

    async def _call(port, command, args):
        return "No compilation errors"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(cell_script, "git_head_sha", lambda: SHA_A)

    terminate_calls: list = []
    monkeypatch.setattr(cell_script, "terminate_workers", lambda procs: terminate_calls.append(procs))
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(
        cell_script.provider_ref, "resolve_provider_ref",
        lambda *a, **k: pytest.fail("resolve_provider_ref must not be called for a SHA pin"),
    )
    return terminate_calls


# ---------------------------------------------------------------------------
# (a) argparse defaults/validation
# ---------------------------------------------------------------------------

def test_parse_args_defaults():
    args = cell_script.parse_args(["--worker-dir", "/tmp/w", "--mode", "pilot", "--receipt", "/tmp/r.json"])
    assert args.port == 9610
    assert args.project_root == cell_script.REPO_ROOT / "unity-test-project"
    assert args.provider_pin == cell_script.SCRIPTS / "source_patch_provider_pin.json"
    assert args.unity == cell_script.worker.DEFAULT_UNITY
    assert args.provider_resolved is None
    assert args.timeout_seconds == 420.0
    assert args.keep_worker is False


def test_parse_args_keep_worker_flag():
    args = cell_script.parse_args(
        ["--worker-dir", "/tmp/w", "--mode", "full", "--receipt", "/tmp/r.json", "--keep-worker"]
    )
    assert args.keep_worker is True


def test_parse_args_requires_worker_dir():
    with pytest.raises(SystemExit):
        cell_script.parse_args(["--mode", "pilot", "--receipt", "/tmp/r.json"])


def test_parse_args_requires_mode():
    with pytest.raises(SystemExit):
        cell_script.parse_args(["--worker-dir", "/tmp/w", "--receipt", "/tmp/r.json"])


def test_parse_args_requires_receipt():
    with pytest.raises(SystemExit):
        cell_script.parse_args(["--worker-dir", "/tmp/w", "--mode", "pilot"])


def test_parse_args_rejects_unknown_mode():
    with pytest.raises(SystemExit):
        cell_script.parse_args(
            ["--worker-dir", "/tmp/w", "--mode", "bogus", "--receipt", "/tmp/r.json"]
        )


# ---------------------------------------------------------------------------
# (b) pilot happy path
# ---------------------------------------------------------------------------

def test_run_cell_pilot_happy_path_produces_pass_receipt(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)
    fixture_calls: list = []
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: fixture_calls.append(1))

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=None,
            mode="pilot",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert receipt["outcome"] == "PASS"
    assert receipt["mode"] == "pilot"
    assert receipt["failed_phase"] is None
    assert receipt["unity_version"] == cell_script.worker.UNITY_VERSION
    assert receipt["provider"]["resolved_sha"] == SHA_B
    assert receipt["provider"]["upm_lock_hash"] == "abc123"
    assert receipt["python_lane"] is None
    assert receipt["csharp_lane"] is None
    assert fixture_calls == []  # pilot never installs the fixture

    on_disk = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert on_disk == receipt


# ---------------------------------------------------------------------------
# (c) full happy path
# ---------------------------------------------------------------------------

def test_run_cell_full_happy_path_runs_both_lanes(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)
    fixture_calls: list = []
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: fixture_calls.append(1))
    monkeypatch.setattr(
        cell_script, "_run_python_lane",
        lambda **k: {"passed": 9, "failed": 0, "skipped": 0, "exit_code": 0},
    )
    csharp_calls: list = []
    monkeypatch.setattr(
        cell_script, "_run_csharp_lane",
        lambda **k: csharp_calls.append(k) or {
            "passed": 3, "failed": 0, "expected_count": 3, "run_id": "run-xyz", "exit_code": 0,
        },
    )

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=None,
            mode="full",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert receipt["outcome"] == "PASS"
    assert fixture_calls == [1]
    assert receipt["python_lane"] == {"passed": 9, "failed": 0, "skipped": 0, "exit_code": 0}
    assert receipt["csharp_lane"]["run_id"] == "run-xyz"
    assert len(csharp_calls) == 1


# ---------------------------------------------------------------------------
# (d) a failing phase -> FAIL + failed_phase, cleanup still runs
# ---------------------------------------------------------------------------

def test_run_cell_launch_failure_marks_phase_and_still_cleans_up(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    terminate_calls = _stub_happy_launch(monkeypatch)

    def _wait_fails(**kwargs):
        raise cell_script.fq.HostedConformanceError("timed out waiting for port")

    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", _wait_fails)

    with pytest.raises(cell_script.fq.HostedConformanceError):
        asyncio.run(
            cell_script.run_cell(
                project_root=tmp_path / "source",
                worker_dir=worker_dir,
                port=9610,
                unity=tmp_path / "Unity",
                provider_pin=pin_path,
                provider_resolved=None,
                mode="full",
                receipt_path=receipt_path,
                timeout_seconds=1.0,
            )
        )

    on_disk = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert on_disk["outcome"] == "FAIL"
    assert on_disk["failed_phase"] == "launch"
    assert "timed out" in on_disk["error"]
    assert len(terminate_calls) == 1
    assert len(terminate_calls[0]) == 1  # the launched fake process was still passed to terminate_workers


# ---------------------------------------------------------------------------
# (e) python lane failure -> stop before csharp lane, cleanup still runs
# ---------------------------------------------------------------------------

def test_run_cell_python_lane_failure_stops_before_csharp_lane(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    terminate_calls = _stub_happy_launch(monkeypatch)
    monkeypatch.setattr(
        cell_script, "_run_python_lane",
        lambda **k: {"passed": 1, "failed": 2, "skipped": 0, "exit_code": 1},
    )
    csharp_calls: list = []
    monkeypatch.setattr(
        cell_script, "_run_csharp_lane",
        lambda **k: csharp_calls.append(k) or {"exit_code": 0},
    )

    with pytest.raises(cell_script.MutationRegressionCellError):
        asyncio.run(
            cell_script.run_cell(
                project_root=tmp_path / "source",
                worker_dir=worker_dir,
                port=9610,
                unity=tmp_path / "Unity",
                provider_pin=pin_path,
                provider_resolved=None,
                mode="full",
                receipt_path=receipt_path,
                timeout_seconds=1.0,
            )
        )

    assert csharp_calls == []  # never attempted -- stop on first lane failure
    on_disk = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert on_disk["outcome"] == "FAIL"
    assert on_disk["failed_phase"] == "python_lane"
    assert on_disk["csharp_lane"] is None
    assert len(terminate_calls) == 1


# ---------------------------------------------------------------------------
# (f) branch pin resolution wiring
# ---------------------------------------------------------------------------

def test_run_cell_branch_pin_resolves_exactly_once(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref="master", ref_kind="branch")
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)  # its resolve-forbidding stub is overridden immediately below

    resolve_calls: list = []

    def _fake_resolve(pin, out):
        resolve_calls.append((pin, out))
        out.parent.mkdir(parents=True, exist_ok=True)
        payload = {"ref": SHA_C, "requested_ref": "master"}
        out.write_text(json.dumps(payload), encoding="utf-8")
        return payload

    monkeypatch.setattr(cell_script.provider_ref, "resolve_provider_ref", _fake_resolve)

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=None,
            mode="pilot",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert len(resolve_calls) == 1
    assert resolve_calls[0][1] == worker_dir.parent / "resolved-pin.json"
    assert receipt["provider"]["resolved_sha"] == SHA_C


def test_run_cell_explicit_resolved_pin_skips_resolution(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref="master", ref_kind="branch")
    resolved_path = tmp_path / "resolved.json"
    resolved_path.write_text(json.dumps({"ref": SHA_C, "requested_ref": "master"}), encoding="utf-8")
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=resolved_path,
            mode="pilot",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert receipt["provider"]["resolved_sha"] == SHA_C


# ---------------------------------------------------------------------------
# Reviewer minor #2: warn-only drift check against the frozen qualification
# lock's final_fsr_adapter_sha -- the pin intentionally floats on master, so
# a mismatch is informational only, never fatal.
# ---------------------------------------------------------------------------

def test_run_cell_records_lock_sha_mismatch_when_resolved_sha_differs(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)
    lock_path = tmp_path / "lock.json"
    lock_path.write_text(json.dumps({"final_fsr_adapter_sha": SHA_C}), encoding="utf-8")
    monkeypatch.setattr(regression, "LOCK_PATH", lock_path)

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=None,
            mode="pilot",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert receipt["provider"]["resolved_sha"] == SHA_B
    assert receipt["provider"]["lock_sha_mismatch"] == SHA_C


def test_run_cell_omits_lock_sha_mismatch_when_matching(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    worker_dir = tmp_path / "work" / "worker"
    pin_path = tmp_path / "pin.json"
    _write_pin(pin_path, ref=SHA_B)
    receipt_path = tmp_path / "receipt.json"

    _stub_happy_launch(monkeypatch)
    lock_path = tmp_path / "lock.json"
    lock_path.write_text(json.dumps({"final_fsr_adapter_sha": SHA_B}), encoding="utf-8")
    monkeypatch.setattr(regression, "LOCK_PATH", lock_path)

    receipt = asyncio.run(
        cell_script.run_cell(
            project_root=tmp_path / "source",
            worker_dir=worker_dir,
            port=9610,
            unity=tmp_path / "Unity",
            provider_pin=pin_path,
            provider_resolved=None,
            mode="pilot",
            receipt_path=receipt_path,
            timeout_seconds=1.0,
        )
    )

    assert "lock_sha_mismatch" not in receipt["provider"]


# ---------------------------------------------------------------------------
# gauntlet.mutation_regression pure helpers
# ---------------------------------------------------------------------------

def test_resolve_provider_pin_passthrough_for_sha_pin(tmp_path: Path):
    pin = tmp_path / "pin.json"
    _write_pin(pin, ref=SHA_A)

    resolved = regression.resolve_provider_pin(pin, None, tmp_path / "worker")

    assert resolved.resolved_path is None
    assert resolved.resolved_sha == SHA_A
    assert resolved.requested_ref == SHA_A


def test_resolve_provider_pin_uses_given_resolved_file(tmp_path: Path):
    pin = tmp_path / "pin.json"
    _write_pin(pin, ref="master", ref_kind="branch")
    resolved_file = tmp_path / "resolved.json"
    resolved_file.write_text(json.dumps({"ref": SHA_B, "requested_ref": "master"}), encoding="utf-8")

    resolved = regression.resolve_provider_pin(pin, resolved_file, tmp_path / "worker")

    assert resolved.resolved_path == resolved_file
    assert resolved.resolved_sha == SHA_B


def test_resolve_provider_pin_resolves_branch_next_to_worker_dir(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    pin = tmp_path / "pin.json"
    _write_pin(pin, ref="master", ref_kind="branch")
    worker_dir = tmp_path / "work" / "worker"

    def _fake_resolve(pin_path, out_path):
        out_path.parent.mkdir(parents=True, exist_ok=True)
        payload = {"ref": SHA_C, "requested_ref": "master"}
        out_path.write_text(json.dumps(payload), encoding="utf-8")
        return payload

    monkeypatch.setattr(regression.provider_ref, "resolve_provider_ref", _fake_resolve)

    resolved = regression.resolve_provider_pin(pin, None, worker_dir)

    assert resolved.resolved_path == worker_dir.parent / "resolved-pin.json"
    assert resolved.resolved_sha == SHA_C


def test_parse_junit_counts_reads_testsuite_wrapped_in_testsuites(tmp_path: Path):
    path = tmp_path / "junit.xml"
    path.write_text(
        '<testsuites><testsuite tests="5" failures="1" errors="0" skipped="1"></testsuite></testsuites>',
        encoding="utf-8",
    )
    assert regression.parse_junit_counts(path) == {"passed": 3, "failed": 1, "skipped": 1}


def test_parse_junit_counts_reads_bare_testsuite_root(tmp_path: Path):
    path = tmp_path / "junit.xml"
    path.write_text('<testsuite tests="2" failures="0" errors="0" skipped="0"></testsuite>', encoding="utf-8")
    assert regression.parse_junit_counts(path) == {"passed": 2, "failed": 0, "skipped": 0}


def test_parse_junit_counts_missing_file_returns_none(tmp_path: Path):
    assert regression.parse_junit_counts(tmp_path / "absent.xml") == {
        "passed": None, "failed": None, "skipped": None,
    }


def test_parse_junit_counts_malformed_xml_returns_none(tmp_path: Path):
    path = tmp_path / "junit.xml"
    path.write_text("not xml at all <<<", encoding="utf-8")
    assert regression.parse_junit_counts(path) == {"passed": None, "failed": None, "skipped": None}


def test_extract_json_blob_finds_trailing_object():
    text = 'project=/x\nport=9610\nrun_id=run-abc\n{\n  "passed": 3,\n  "failed": 0\n}\n'
    assert regression.extract_json_blob(text) == {"passed": 3, "failed": 0}


def test_extract_json_blob_returns_none_without_json():
    assert regression.extract_json_blob("no json here") is None


def test_extract_run_id_finds_the_line():
    text = "project=/x\nport=9610\nrun_id=run-abc123\n"
    assert regression.extract_run_id(text) == "run-abc123"


def test_extract_run_id_returns_none_when_absent():
    assert regression.extract_run_id("nothing here") is None


def test_read_upm_lock_hash_present(tmp_path: Path):
    lock = tmp_path / "Packages" / "packages-lock.json"
    lock.parent.mkdir(parents=True)
    lock.write_text(json.dumps({"dependencies": {"com.foo": {"hash": "deadbeef"}}}), encoding="utf-8")
    assert regression.read_upm_lock_hash(tmp_path, "com.foo") == "deadbeef"


def test_read_upm_lock_hash_missing_file(tmp_path: Path):
    assert regression.read_upm_lock_hash(tmp_path, "com.foo") is None


def test_read_upm_lock_hash_missing_package(tmp_path: Path):
    lock = tmp_path / "Packages" / "packages-lock.json"
    lock.parent.mkdir(parents=True)
    lock.write_text(json.dumps({"dependencies": {}}), encoding="utf-8")
    assert regression.read_upm_lock_hash(tmp_path, "com.foo") is None


def test_read_upm_lock_hash_malformed_json(tmp_path: Path):
    lock = tmp_path / "Packages" / "packages-lock.json"
    lock.parent.mkdir(parents=True)
    lock.write_text("not json", encoding="utf-8")
    assert regression.read_upm_lock_hash(tmp_path, "com.foo") is None


def test_build_receipt_omits_error_key_when_none():
    receipt = regression.build_receipt(
        checkout_sha=SHA_A, provider={}, unity_version="6000.0.65f1", port=9610,
        mode="pilot", outcome="PASS", python_lane=None, csharp_lane=None,
        timeline=[], failed_phase=None,
    )
    assert "error" not in receipt


def test_build_receipt_includes_error_key_when_given():
    receipt = regression.build_receipt(
        checkout_sha=SHA_A, provider={}, unity_version="6000.0.65f1", port=9610,
        mode="full", outcome="FAIL", python_lane=None, csharp_lane=None,
        timeline=[], failed_phase="launch", error="boom",
    )
    assert receipt["error"] == "boom"


def test_run_python_lane_builds_expected_command_and_env(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    captured: dict = {}

    def _fake_run(cmd, **kwargs):
        captured["cmd"] = cmd
        captured["kwargs"] = kwargs

        class _Result:
            returncode = 0

        return _Result()

    monkeypatch.setattr(regression.subprocess, "run", _fake_run)
    fake_interpreter = "/fake/interpreter/python-does-not-exist"
    monkeypatch.setattr(regression.sys, "executable", fake_interpreter)

    project = tmp_path / "work" / "worker"
    result = regression.run_python_lane(host="127.0.0.1", port=9610, project=project)

    # argv[0] must track sys.executable dynamically (matches run_csharp_lane's
    # pattern), not a hardcoded server/.venv/bin/python path that does not
    # exist in CI (pip install -e installs into the runner's system Python).
    assert captured["cmd"][0] == fake_interpreter
    assert "tests/mutation" in captured["cmd"]
    assert "live and mutation_live" in captured["cmd"]
    assert captured["kwargs"]["env"]["UNITY_MCP_RUN_MUTATION_LIVE"] == "1"
    assert captured["kwargs"]["env"]["UNITY_MCP_PORT"] == "9610"
    assert captured["kwargs"]["cwd"] == regression.REPO_ROOT / "server"
    assert result["exit_code"] == 0


def test_run_csharp_lane_parses_json_on_success(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    stdout = "project=/x\nport=9610\nrun_id=run-abc\n" + json.dumps(
        {"passed": 3, "failed": 0, "expected_count": 3, "run_id": "run-abc"}
    )

    def _fake_run(cmd, **kwargs):
        class _Result:
            returncode = 0

        result = _Result()
        result.stdout = stdout
        result.stderr = ""
        return result

    monkeypatch.setattr(regression.subprocess, "run", _fake_run)

    result = regression.run_csharp_lane(worker_dir=tmp_path / "worker", port=9610)

    assert result == {"passed": 3, "failed": 0, "expected_count": 3, "run_id": "run-abc", "exit_code": 0}


def test_run_csharp_lane_records_run_id_and_exit_code_on_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    stdout = "project=/x\nport=9610\nrun_id=run-def\n"

    def _fake_run(cmd, **kwargs):
        class _Result:
            returncode = 1

        result = _Result()
        result.stdout = stdout
        result.stderr = "FAILED: test run completed with outcome=failed, failed=2, invalid=0"
        return result

    monkeypatch.setattr(regression.subprocess, "run", _fake_run)

    result = regression.run_csharp_lane(worker_dir=tmp_path / "worker", port=9610)

    assert result["passed"] is None
    assert result["expected_count"] is None
    assert result["run_id"] == "run-def"
    assert result["exit_code"] == 1
    assert "failed=2" in result["error"]
