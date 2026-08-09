from __future__ import annotations

import os
import signal
import socket
import subprocess
import sys
import time
from pathlib import Path
from typing import TYPE_CHECKING

from create_unity_test_worker import BOOTSTRAP_SCENE, WorkerCreationError, create_worker

if TYPE_CHECKING:
    from collections.abc import Mapping

REPO_ROOT = Path(__file__).resolve().parents[2]


class HostedConformanceError(RuntimeError):
    pass


def build_unity_command(unity: Path, project: Path, log: Path) -> list[str]:
    return [
        str(unity),
        "-batchmode",
        "-nographics",
        "-projectPath",
        str(project.resolve()),
        "-logFile",
        str(log.resolve()),
    ]


def build_unity_environment(
    base: Mapping[str, str],
    *,
    port: int,
    project: Path,
    read_only: bool,
) -> dict[str, str]:
    env = dict(base)
    env["UNITY_MCP_PORT"] = str(port)
    env["UNITY_MCP_PROJECT_PATH"] = str(project.resolve())
    env["UNITY_MCP_ENABLE_BATCHMODE"] = "1"
    env["UNITY_MCP_BOOTSTRAP_SCENE"] = BOOTSTRAP_SCENE
    env["UNITY_MCP_BUDGET"] = "0"
    env["UNITY_MCP_HINTS"] = "0"
    env["UNITY_MCP_DISTILL"] = "0"
    env["UNITY_MCP_PREFETCH_CACHE"] = "0"
    if read_only:
        env["UNITY_MCP_READ_ONLY"] = "1"
    else:
        env.pop("UNITY_MCP_READ_ONLY", None)
    return env


def write_mcp_project_settings(project: Path, *, port: int, read_only: bool) -> None:
    settings = project / "ProjectSettings" / "MCPSettings.json"
    settings.parent.mkdir(parents=True, exist_ok=True)
    settings.write_text(
        f'{{"port":{port},"chatPort":{port + 1},"readOnly":'
        f'{"true" if read_only else "false"}}}\n',
        encoding="utf-8",
    )


def run_conformance_profiles(
    *,
    project_a: Path,
    port_a: int,
    project_b: Path,
    port_b: int,
    reports: Path,
    timeout: int,
    verbose: bool,
) -> int:
    reports.mkdir(parents=True, exist_ok=True)
    commands = (
        _conformance_command(
            project=project_a,
            port=port_a,
            reports=reports,
            timeout=timeout,
            label="single",
        ),
        _conformance_command(
            project=project_a,
            port=port_a,
            reports=reports,
            timeout=timeout,
            label="dual",
            second=(project_b, port_b),
        ),
    )
    for command in commands:
        try:
            result = subprocess.run(command, cwd=REPO_ROOT, timeout=timeout + 90)
        except subprocess.TimeoutExpired as exc:
            print(f"ERROR: conformance subprocess timed out: {exc}", file=sys.stderr)
            return 1
        if result.returncode != 0:
            return result.returncode
    return 0


def launch_worker(
    *,
    unity: Path,
    project: Path,
    port: int,
    log: Path,
    read_only: bool,
) -> subprocess.Popen:
    command = build_unity_command(unity, project, log)
    env = build_unity_environment(os.environ, port=port, project=project, read_only=read_only)
    kwargs: dict[str, object] = {
        "cwd": REPO_ROOT,
        "env": env,
        "stdin": subprocess.DEVNULL,
        "stdout": subprocess.DEVNULL,
        "stderr": subprocess.DEVNULL,
    }
    if os.name == "nt":
        kwargs["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        kwargs["start_new_session"] = True
    return subprocess.Popen(command, **kwargs)


def wait_for_port(
    *,
    host: str,
    port: int,
    process: subprocess.Popen,
    log: Path,
    timeout: float,
) -> None:
    deadline = time.monotonic() + timeout
    last_error: OSError | None = None
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise HostedConformanceError(
                f"Unity worker for port {port} exited early with {process.returncode}.\n"
                + tail_log(log)
            )
        try:
            with socket.create_connection((host, port), timeout=1.0):
                return
        except OSError as exc:
            last_error = exc
            time.sleep(1.0)
    raise HostedConformanceError(
        f"Timed out waiting for Unity MCP port {host}:{port}: {last_error}\n"
        + tail_log(log)
    )


def terminate_workers(processes: list[subprocess.Popen], *, timeout: float = 20.0) -> None:
    for process in processes:
        if process.poll() is None:
            _signal_process(process, force=False)
    deadline = time.monotonic() + timeout
    for process in processes:
        while process.poll() is None and time.monotonic() < deadline:
            time.sleep(0.2)
    for process in processes:
        if process.poll() is None:
            _signal_process(process, force=True)


def run_hosted_conformance(
    *,
    unity: Path,
    source_project: Path,
    work_root: Path,
    reports: Path,
    host: str,
    port_a: int,
    port_b: int,
    startup_timeout: int,
    timeout: int,
    verbose: bool,
) -> int:
    reports = reports.resolve()
    reports.mkdir(parents=True, exist_ok=True)
    project_a = work_root.resolve() / "worker-a"
    project_b = work_root.resolve() / "worker-b"
    processes: list[subprocess.Popen] = []
    try:
        work_root.mkdir(parents=True)
        create_worker(source_project, project_a)
        create_worker(source_project, project_b)
        write_mcp_project_settings(project_a, port=port_a, read_only=False)
        write_mcp_project_settings(project_b, port=port_b, read_only=True)
        specs = (
            (project_a, port_a, reports / "unity-worker-a.log", False),
            (project_b, port_b, reports / "unity-worker-b.log", True),
        )
        for project, port, log, read_only in specs:
            processes.append(
                launch_worker(
                    unity=unity,
                    project=project,
                    port=port,
                    log=log,
                    read_only=read_only,
                )
            )
        for process, (_, port, log, _) in zip(processes, specs, strict=True):
            wait_for_port(
                host=host,
                port=port,
                process=process,
                log=log,
                timeout=startup_timeout,
            )
        return run_conformance_profiles(
            project_a=project_a,
            port_a=port_a,
            project_b=project_b,
            port_b=port_b,
            reports=reports,
            timeout=timeout,
            verbose=verbose,
        )
    finally:
        terminate_workers(processes)


def tail_log(path: Path, *, lines: int = 120) -> str:
    if not path.exists():
        return f"Unity log not found: {path}"
    data = path.read_text(encoding="utf-8", errors="replace").splitlines()
    return "\n".join(data[-lines:])


def _conformance_command(
    *,
    project: Path,
    port: int,
    reports: Path,
    timeout: int,
    label: str,
    second: tuple[Path, int] | None = None,
) -> list[str]:
    command = [
        sys.executable,
        "scripts/conformance_runner.py",
        "--port",
        str(port),
        "--project",
        str(project.resolve()),
    ]
    if second:
        project_b, port_b = second
        command.extend(
            ["--second-port", str(port_b), "--second-project", str(project_b.resolve())]
        )
        command.extend(["--markers", "cross_project and live"])
    command.extend(
        [
            "--junit",
            str(reports / f"conformance-hosted-{label}.xml"),
            "--record",
            str(reports / f"conformance-hosted-{label}-trace.jsonl"),
            "--timeout",
            str(timeout),
        ]
    )
    return command


def _signal_process(process: subprocess.Popen, *, force: bool) -> None:
    try:
        if os.name == "nt":
            process.kill() if force else process.terminate()
        else:
            sig = signal.SIGKILL if force else signal.SIGTERM
            os.killpg(process.pid, sig)
    except OSError:
        pass


__all__ = [
    "HostedConformanceError",
    "WorkerCreationError",
    "build_unity_command",
    "build_unity_environment",
    "run_conformance_profiles",
    "run_hosted_conformance",
    "tail_log",
    "terminate_workers",
    "wait_for_port",
    "write_mcp_project_settings",
]
