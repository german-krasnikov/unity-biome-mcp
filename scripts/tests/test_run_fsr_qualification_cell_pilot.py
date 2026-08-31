"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: run_pilot's
target-version threading and exception handling.

Run 3 showed max-linux-x64/max-macos-arm64 pilots failing while their
min-* counterparts passed: run_pilot never called
create_worker(..., target_unity_version=..., target_unity_revision=...),
so every pilot worker declared U_MIN (6000.0.65f1) regardless of which
window's Editor actually launched it — for a max-* cell that is a headed
6000.5.10f1 Editor opening a 6000.0.65f1-declared project, which risks
Unity's interactive version-mismatch dialog (it blocks indefinitely outside
batchmode). Separately, HostedConformanceError (RuntimeError-based, not
OSError) was missing from run_pilot's except clause, so a real port-wait
timeout wasn't classified as INFRASTRUCTURE_BLOCKED and its message was
lost.

Runs in the standard `scripts/tests` lane: no Unity, no network —
create_worker/_launch/wait_for_port/durable.call/_stop are all
monkeypatched.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import run_fsr_qualification_cell as cell_script  # noqa: E402


class _FakeProcess:
    def poll(self):
        return None


def _stub_common(monkeypatch: pytest.MonkeyPatch, *, create_worker_calls: list):
    monkeypatch.setattr(
        cell_script.worker,
        "create_worker",
        lambda *a, **k: create_worker_calls.append(k),
    )
    monkeypatch.setattr(cell_script, "_launch", lambda **k: _FakeProcess())
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)


def test_run_pilot_forwards_target_unity_version_to_create_worker(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            unity_version="6000.5.10f1",
            unity_revision="3bd4f66ad299",
        )
    )

    assert len(calls) == 1
    assert calls[0]["target_unity_version"] == "6000.5.10f1"
    assert calls[0]["target_unity_revision"] == "3bd4f66ad299"


def test_run_pilot_defaults_target_version_to_none_when_no_window(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
        )
    )

    assert calls[0]["target_unity_version"] is None
    assert calls[0]["target_unity_revision"] is None


def test_run_pilot_classifies_hosted_conformance_timeout_as_infrastructure_blocked(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    def _wait_for_port(**kwargs):
        raise cell_script.HostedConformanceError(
            "Timed out waiting for Unity MCP port 127.0.0.1:9600: timed out"
        )

    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", _wait_for_port)

    written = {}

    def _write_pilot_evidence(evidence_out, **kwargs):
        written.update(kwargs)

    monkeypatch.setattr(cell_script.fq, "write_pilot_evidence", _write_pilot_evidence)

    import asyncio

    with pytest.raises(cell_script.HostedConformanceError):
        asyncio.run(
            cell_script.run_pilot(
                unity=tmp_path / "Unity",
                source_project=tmp_path / "source",
                work_root=tmp_path / "work",
                port=9600,
                startup_timeout=1.0,
                evidence_out=tmp_path / "evidence",
            )
        )

    assert written["outcome"] == "INFRASTRUCTURE_BLOCKED"
    assert "Timed out" in written["error"]


# ---------------------------------------------------------------------------
# _launch's force_d3d11 wiring (Windows-only diagnostic experiment)
# ---------------------------------------------------------------------------

def test_launch_forces_d3d11_only_on_windows(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    captured: dict = {}

    def _fake_build(unity, project, log, *, force_d3d11=False):
        captured["force_d3d11"] = force_d3d11
        return [str(unity)]

    monkeypatch.setattr(cell_script.fq, "build_headed_unity_command", _fake_build)
    monkeypatch.setattr(cell_script, "write_mcp_project_settings", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.subprocess, "Popen", lambda *a, **k: _FakeProcess())
    # CREATE_NEW_PROCESS_GROUP only exists on a real Windows Python build;
    # inject it so the os.name == "nt" branch is exercisable on any dev/CI
    # platform, matching test_hosted_conformance_runner.py's
    # test_windows_signal_process_does_not_require_sigkill precedent for
    # testing this same Windows-only branch cross-platform.
    monkeypatch.setattr(cell_script.subprocess, "CREATE_NEW_PROCESS_GROUP", 0x200, raising=False)

    monkeypatch.setattr(cell_script.os, "name", "nt")
    cell_script._launch(unity=tmp_path / "Unity", project=tmp_path / "p", port=9600, log=tmp_path / "l")
    assert captured["force_d3d11"] is True

    monkeypatch.setattr(cell_script.os, "name", "posix")
    cell_script._launch(unity=tmp_path / "Unity", project=tmp_path / "p", port=9600, log=tmp_path / "l")
    assert captured["force_d3d11"] is False


# ---------------------------------------------------------------------------
# preseed wiring — Run 4 root cause (FSR's first-run modal dialog blocks
# ProcessInitializeOnLoadAttributes on a headed Editor). Defense-in-depth:
# preseed runs before the Editor launches, and a failed preseed must never
# abort the pilot.
# ---------------------------------------------------------------------------

def test_run_pilot_calls_preseed_before_launch_and_embeds_receipt_on_success(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: {"mechanism": "linux_prefs_xml", "applied": True, "keys": []},
    )

    written = {}
    monkeypatch.setattr(
        cell_script.fq,
        "write_pilot_evidence",
        lambda evidence_out, **kwargs: written.update(kwargs),
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Linux",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert written["preseed"]["mechanism"] == "linux_prefs_xml"
    assert written["preseed"]["applied"] is True


def test_run_pilot_survives_a_failed_preseed(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """Preseed is defense-in-depth, never the primary fix — a preseed
    failure must not abort the pilot, only be recorded honestly."""
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    def _fail(project, *, os_name):
        raise cell_script.preseed.EditorPrefsPreseedError("no ProjectSettings.asset")

    monkeypatch.setattr(cell_script.preseed, "preseed_editor_prefs", _fail)

    written = {}
    monkeypatch.setattr(
        cell_script.fq,
        "write_pilot_evidence",
        lambda evidence_out, **kwargs: written.update(kwargs),
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Windows",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert written["preseed"]["applied"] is False
    assert "no ProjectSettings.asset" in written["preseed"]["error"]
