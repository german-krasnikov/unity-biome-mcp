from __future__ import annotations

import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from monitor_mcp_processes import (  # noqa: E402
    ProcessRow,
    classify_state,
    parse_age_seconds,
    parse_ps,
    redact_command,
)


def test_classifies_stale_version_probe() -> None:
    state = classify_state(
        processes=[
            ProcessRow(
                pid=10,
                ppid=1,
                stat="S",
                etimes=7200,
                pcpu=0.0,
                pmem=0.0,
                command="uvx unity-biome-mcp-relay --version",
            )
        ],
        live_pids={10},
        lock_pids=set(),
        listener_pids=set(),
        max_version_seconds=60,
    )

    assert state["issues"] == [
        {
            "kind": "stale_version_probe",
            "pid": 10,
            "age_seconds": 7200,
            "command": "uvx unity-biome-mcp-relay --version",
        }
    ]


def test_classifies_stale_lock_when_pid_is_absent() -> None:
    state = classify_state(
        processes=[],
        live_pids=set(),
        lock_pids={12345},
        listener_pids=set(),
        max_version_seconds=60,
    )

    assert state["issues"] == [
        {"kind": "stale_server_lock", "pid": 12345}
    ]


def test_marks_running_server_with_listener_as_active() -> None:
    state = classify_state(
        processes=[
            ProcessRow(
                pid=20,
                ppid=1,
                stat="S",
                etimes=4000,
                pcpu=0.1,
                pmem=0.2,
                command="/bin/python /bin/unity-biome-mcp",
            )
        ],
        live_pids={20},
        lock_pids={20},
        listener_pids={20},
        max_version_seconds=60,
    )

    assert state["issues"] == []
    assert state["active_servers"] == [
        {
            "pid": 20,
            "age_seconds": 4000,
            "has_lock": True,
            "has_listener": True,
            "command": "/bin/python /bin/unity-biome-mcp",
        }
    ]


def test_does_not_classify_unity_project_path_as_mcp_server() -> None:
    state = classify_state(
        processes=[
            ProcessRow(
                pid=30,
                ppid=1,
                stat="S",
                etimes=4000,
                pcpu=0.1,
                pmem=0.2,
                command=(
                    "Unity -projectpath "
                    "/Users/me/Work/python/unity-biome-mcp/unity-test-project"
                ),
            )
        ],
        live_pids={30},
        lock_pids=set(),
        listener_pids={30},
        max_version_seconds=60,
    )

    assert state["issues"] == []
    assert state["active_servers"] == []


def test_redacts_access_tokens_from_process_commands() -> None:
    assert (
        redact_command("Unity -accessToken secret-value -projectPath /tmp/x")
        == "Unity -accessToken <redacted> -projectPath /tmp/x"
    )


def test_parse_age_seconds_supports_macos_etime_formats() -> None:
    assert parse_age_seconds("42") == 42
    assert parse_age_seconds("03:04") == 184
    assert parse_age_seconds("02:03:04") == 7384
    assert parse_age_seconds("01-02:03:04") == 93784


def test_parse_ps_reads_macos_etime_and_redacts_tokens() -> None:
    rows = parse_ps(
        "  PID  PPID STAT     ELAPSED  %CPU %MEM COMMAND\n"
        "  123     1 S      01-02:03:04   0.1  0.2 "
        "Unity -accessToken secret -projectPath /tmp/project\n"
    )

    assert rows == [
        ProcessRow(
            pid=123,
            ppid=1,
            stat="S",
            etimes=93784,
            pcpu=0.1,
            pmem=0.2,
            command="Unity -accessToken <redacted> -projectPath /tmp/project",
        )
    ]
