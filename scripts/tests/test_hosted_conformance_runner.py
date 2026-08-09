from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    import pytest

SCRIPTS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(SCRIPTS))

from gauntlet import hosted_conformance as hosted  # noqa: E402


def test_unity_launch_command_uses_batchmode_without_quit(tmp_path: Path) -> None:
    command = hosted.build_unity_command(
        Path("/opt/unity/Editor/Unity"),
        tmp_path / "ProjectA",
        tmp_path / "unity-a.log",
    )

    assert "-batchmode" in command
    assert "-nographics" in command
    assert "-projectPath" in command
    assert str(tmp_path / "ProjectA") in command
    assert "-logFile" in command
    assert str(tmp_path / "unity-a.log") in command
    assert "-quit" not in command


def test_worker_environment_enables_batchmode_server_and_optional_read_only(
    tmp_path: Path,
) -> None:
    env = hosted.build_unity_environment(
        os.environ,
        port=9699,
        project=tmp_path / "ProjectB",
        read_only=True,
    )

    assert env["UNITY_MCP_PORT"] == "9699"
    assert env["UNITY_MCP_PROJECT_PATH"] == str((tmp_path / "ProjectB").resolve())
    assert env["UNITY_MCP_ENABLE_BATCHMODE"] == "1"
    assert env["UNITY_MCP_BOOTSTRAP_SCENE"] == "Assets/Scenes/GridTest.unity"
    assert env["UNITY_MCP_READ_ONLY"] == "1"
    assert env["UNITY_MCP_BUDGET"] == "0"
    assert env["UNITY_MCP_HINTS"] == "0"
    assert env["UNITY_MCP_DISTILL"] == "0"


def test_write_project_settings_sets_port_chat_and_read_only(tmp_path: Path) -> None:
    project = tmp_path / "ProjectB"

    hosted.write_mcp_project_settings(project, port=9699, read_only=True)

    settings = (project / "ProjectSettings" / "MCPSettings.json").read_text(
        encoding="utf-8"
    )
    assert '"port":9699' in settings
    assert '"chatPort":9700' in settings
    assert '"readOnly":true' in settings


def test_run_conformance_invokes_single_then_dual_with_expected_markers(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    calls: list[list[str]] = []

    def fake_run(command: list[str], **_kwargs: object) -> subprocess.CompletedProcess:
        calls.append(command)
        return subprocess.CompletedProcess(command, 0)

    monkeypatch.setattr(subprocess, "run", fake_run)

    rc = hosted.run_conformance_profiles(
        project_a=tmp_path / "ProjectA",
        port_a=9600,
        project_b=tmp_path / "ProjectB",
        port_b=9699,
        reports=tmp_path / "reports",
        timeout=300,
        verbose=True,
    )

    assert rc == 0
    assert len(calls) == 2
    assert "scripts/conformance_runner.py" in " ".join(calls[0])
    assert "--markers" not in calls[0]
    assert "--second-port" in calls[1]
    assert "9699" in calls[1]
    assert "--markers" in calls[1]
    assert "cross_project and live" in calls[1]


def test_run_conformance_timeout_fails_closed(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def fake_run(_command: list[str], **_kwargs: object) -> subprocess.CompletedProcess:
        raise subprocess.TimeoutExpired(["pytest"], timeout=1)

    monkeypatch.setattr(subprocess, "run", fake_run)

    rc = hosted.run_conformance_profiles(
        project_a=tmp_path / "ProjectA",
        port_a=9600,
        project_b=tmp_path / "ProjectB",
        port_b=9699,
        reports=tmp_path / "reports",
        timeout=300,
        verbose=False,
    )

    assert rc == 1


def test_windows_signal_process_does_not_require_sigkill(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    class FakeProcess:
        def __init__(self) -> None:
            self.calls: list[str] = []

        def terminate(self) -> None:
            self.calls.append("terminate")

        def kill(self) -> None:
            self.calls.append("kill")

    process = FakeProcess()
    monkeypatch.setattr(hosted.os, "name", "nt")
    monkeypatch.delattr(hosted.signal, "SIGKILL", raising=False)

    hosted._signal_process(process, force=False)  # noqa: SLF001
    hosted._signal_process(process, force=True)  # noqa: SLF001

    assert process.calls == ["terminate", "kill"]
