"""Install and prove the Python MCP runtime from one staged wheel."""


import hashlib
import json
import os
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

from gauntlet.artifact_contracts import read_stable_artifact
from gauntlet.process_contracts import ProcessSpec, ProcessSupervisionError
from gauntlet.process_supervisor import ProcessSupervisor

_MAX_WHEEL_BYTES = 256 * 1024 * 1024
_DIST_NAME = "unity-biome-mcp"
_MODULE_ROOT = "unity_mcp"
_ENTRYPOINT = "unity-biome-mcp"


class RuntimeInstallError(RuntimeError):
    """Raised when an installed runtime cannot be proven."""


@dataclass(frozen=True, slots=True)
class InstalledRuntimeReceipt:
    artifact_sha256: str
    artifact_size_bytes: int
    distribution_version: str
    distribution_path: str
    module_path: str
    entrypoint_path: str
    python_executable: str
    runtime_root: str


def install_python_wheel_runtime(
    wheel_path: Path,
    runtime_root: Path,
    *,
    expected_sha256: str,
    product_version: str,
    python_executable: str = sys.executable,
    uv_executable: str | None = None,
) -> InstalledRuntimeReceipt:
    """Install one exact wheel into a fresh venv and return a verifiable receipt."""
    _validate_digest(expected_sha256)
    runtime_root = _prepare_runtime_root(runtime_root)
    snapshot = read_stable_artifact(wheel_path, _MAX_WHEEL_BYTES)
    digest = hashlib.sha256(snapshot).hexdigest()
    if digest != expected_sha256:
        raise RuntimeInstallError("wheel digest does not match expected artifact")
    venv = runtime_root / "venv"
    _run((python_executable, "-m", "venv", "--system-site-packages", str(venv)), runtime_root)
    venv_python = _venv_python(venv)
    uv = uv_executable or shutil.which("uv")
    if uv:
        _run(
            (
                uv,
                "pip",
                "install",
                "--python",
                str(venv_python),
                "--force-reinstall",
                "--no-deps",
                str(wheel_path),
            ),
            runtime_root,
        )
    else:
        _run(
            (str(venv_python), "-m", "pip", "install", "--force-reinstall", "--no-deps", str(wheel_path)),
            runtime_root,
        )
    return _probe_runtime(
        venv_python,
        runtime_root,
        expected_sha256=digest,
        artifact_size=len(snapshot),
        product_version=product_version,
    )


def public_stdio_environment(
    receipt: InstalledRuntimeReceipt,
    *,
    home: Path,
    temp: Path,
    port: int,
    project_path: Path,
) -> dict[str, str]:
    """Build a minimal environment for launching the installed stdio entrypoint."""
    entrypoint = Path(receipt.entrypoint_path)
    environment = {
        "HOME": str(home),
        "USERPROFILE": str(home),
        "APPDATA": str(home / "appdata"),
        "LOCALAPPDATA": str(home / "localappdata"),
        "TMPDIR": str(temp),
        "TEMP": str(temp),
        "TMP": str(temp),
        "PATH": str(entrypoint.parent) + os.pathsep + os.environ.get("PATH", ""),
        "PYTHONNOUSERSITE": "1",
        "PYTHONUTF8": "1",
        "UNITY_MCP_PORT": str(port),
        "UNITY_MCP_PROJECT_PATH": str(project_path.resolve()),
        "UNITY_MCP_PROJECT_DIR": str(project_path.resolve()),
        "UNITY_MCP_TRANSPORT": "stdio",
        "UNITY_MCP_IDLE_TIMEOUT": "0",
        "UNITY_MCP_HINTS": "0",
        "UNITY_MCP_BUDGET": "0",
        "UNITY_MCP_PREFETCH_CACHE": "0",
        "UNITY_MCP_DISTILL": "0",
        "UNITY_MCP_PLUGIN_DIRS": "",
    }
    if "SYSTEMROOT" in os.environ:
        environment["SYSTEMROOT"] = os.environ["SYSTEMROOT"]
    return environment


def _prepare_runtime_root(path: Path) -> Path:
    if path.exists() and any(path.iterdir()):
        raise RuntimeInstallError("runtime root must be empty")
    path.mkdir(parents=True, exist_ok=True)
    return path.resolve()


def _venv_python(venv: Path) -> Path:
    candidate = venv / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
    if not candidate.is_file():
        raise RuntimeInstallError("venv python was not created")
    return candidate


def _run(command: tuple[str, ...], cwd: Path) -> None:
    try:
        result = ProcessSupervisor().run(
            ProcessSpec(
                command=command,
                cwd=cwd,
                environment={"PATH": os.environ.get("PATH", ""), "PYTHONUTF8": "1"},
                timeout_seconds=60,
                output_limit_bytes=512 * 1024,
                graceful_shutdown_seconds=1,
            )
        )
    except ProcessSupervisionError as exc:
        raise RuntimeInstallError(str(exc)) from exc
    if not result.completed_within_scope:
        detail = result.stderr.tail.decode("utf-8", errors="replace")
        raise RuntimeInstallError(f"runtime command failed: {detail}")


def _probe_runtime(
    python: Path,
    runtime_root: Path,
    *,
    expected_sha256: str,
    artifact_size: int,
    product_version: str,
) -> InstalledRuntimeReceipt:
    payload = _probe_json(python, runtime_root)
    if payload["distribution_version"] != product_version:
        raise RuntimeInstallError("installed distribution version does not match product version")
    for key in ("distribution_path", "module_path", "entrypoint_path"):
        _require_under_root(Path(str(payload[key])), runtime_root, key)
    return InstalledRuntimeReceipt(
        artifact_sha256=expected_sha256,
        artifact_size_bytes=artifact_size,
        distribution_version=str(payload["distribution_version"]),
        distribution_path=str(payload["distribution_path"]),
        module_path=str(payload["module_path"]),
        entrypoint_path=str(payload["entrypoint_path"]),
        python_executable=str(python),
        runtime_root=str(runtime_root),
    )


def _probe_json(python: Path, cwd: Path) -> dict[str, object]:
    script = (
        "import importlib.metadata as md,json,pathlib,shutil,sys,unity_mcp;"
        f"dist=md.distribution({_DIST_NAME!r});"
        f"entry=shutil.which({_ENTRYPOINT!r});"
        "print(json.dumps({"
        "'distribution_version':dist.version,"
        "'distribution_path':str(pathlib.Path(dist.locate_file('')).resolve()),"
        "'module_path':str(pathlib.Path(unity_mcp.__file__).resolve()),"
        "'entrypoint_path':str(pathlib.Path(entry).resolve()) if entry else ''"
        "},sort_keys=True))"
    )
    try:
        result = ProcessSupervisor().run(
            ProcessSpec(
                command=(str(python), "-I", "-c", script),
                cwd=cwd,
                environment={"PATH": str(python.parent) + os.pathsep + os.environ.get("PATH", "")},
                timeout_seconds=30,
                output_limit_bytes=64 * 1024,
                graceful_shutdown_seconds=1,
            )
        )
    except ProcessSupervisionError as exc:
        raise RuntimeInstallError(str(exc)) from exc
    if not result.completed_within_scope:
        raise RuntimeInstallError("installed runtime probe failed")
    try:
        value = json.loads(result.stdout.tail.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise RuntimeInstallError("installed runtime probe did not return JSON") from exc
    if not isinstance(value, dict) or set(value) != {
        "distribution_version",
        "distribution_path",
        "module_path",
        "entrypoint_path",
    }:
        raise RuntimeInstallError("installed runtime probe schema mismatch")
    return value


def _require_under_root(path: Path, root: Path, label: str) -> None:
    if not path.is_absolute():
        raise RuntimeInstallError(f"{label} is not absolute")
    try:
        path.relative_to(root)
    except ValueError as exc:
        raise RuntimeInstallError(f"{label} is outside the installed runtime") from exc


def _validate_digest(value: str) -> None:
    if len(value) != 64 or any(character not in "0123456789abcdef" for character in value):
        raise RuntimeInstallError("artifact digest is invalid")
