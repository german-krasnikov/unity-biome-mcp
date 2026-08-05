"""Internal helpers and subcommand implementations for install.py.

Functions that don't reference patchable config imports live here.
"""
import argparse
import json
import platform
import re
import shutil
import socket
import subprocess
import sys
from pathlib import Path

# ── pure helpers ──────────────────────────────────────────────────────────────

def check_python() -> bool:
    return sys.version_info >= (3, 10)


def venv_python(server_dir: Path) -> Path:
    if platform.system() == "Windows":
        return server_dir / ".venv" / "Scripts" / "python.exe"
    return server_dir / ".venv" / "bin" / "python"


def venv_stale(server_dir: Path) -> bool:
    """True if venv python doesn't point into server_dir (moved folder)."""
    py = venv_python(server_dir)
    if not py.exists():
        return False
    try:
        resolved = py.resolve()
        return server_dir not in resolved.parents
    except Exception:
        return True


def sync_uv(server_dir: Path) -> None:
    subprocess.run(["uv", "sync"], cwd=server_dir, check=True)


def create_venv(server_dir: Path) -> None:
    venv_dir = server_dir / ".venv"
    subprocess.run([sys.executable, "-m", "venv", str(venv_dir)], check=True)
    pip = venv_dir / ("Scripts" if platform.system() == "Windows" else "bin") / "pip"
    subprocess.run([str(pip), "install", "-e", ".[dev]"], cwd=server_dir, check=True)


def write_codex_config(server_dir: Path, codex_dir: Path, codex_config: Path, ui) -> None:
    from unity_mcp.config.merger import SERVER_NAME
    py = venv_python(server_dir)
    codex_dir.mkdir(exist_ok=True)
    codex_config.write_text(
        f"[mcp_servers.{SERVER_NAME}]\n"
        f'command = "{py.as_posix()}"\n'
        'args = ["-m", "unity_mcp.server"]\n'
        "startup_timeout_sec = 10\n"
        "tool_timeout_sec = 120\n"
        "enabled = true\n"
        f"\n[mcp_servers.{SERVER_NAME}.env]\n"
        'PYTHONUTF8 = "1"\n',
        encoding="utf-8",
    )
    ui.ok(f"Wrote {codex_config}")


def discover_port() -> int:
    """Return Unity Biome MCP port from ~/.unity-biome-mcp/ports/*.port, default 9500."""
    from unity_mcp.paths import ports_dir
    ports = ports_dir()
    if ports.exists():
        for f in ports.glob("*.port"):
            try:
                return int(f.read_text(encoding="utf-8").split("\n")[0])
            except (ValueError, OSError):  # noqa: PERF203
                continue
    return 9500


def setup_env(server_dir: Path, codex_dir: Path, codex_config: Path, ui,
              force_recreate: bool = False) -> None:
    if not check_python():
        sys.exit(
            f"Python 3.10+ required, got {sys.version.split()[0]}\n"
            "  Download: https://python.org/downloads/\n"
            "  Windows tip: check 'Add Python to PATH' during install"
        )

    has_uv = shutil.which("uv") is not None

    if force_recreate and not has_uv:
        venv_dir = server_dir / ".venv"
        if venv_dir.exists():
            ui.info("Removing stale .venv …")
            shutil.rmtree(venv_dir)

    if has_uv:
        ui.info("uv found — running uv sync …")
        sync_uv(server_dir)
    else:
        ui.info("uv not found — using venv + pip …")
        create_venv(server_dir)

    write_codex_config(server_dir, codex_dir, codex_config, ui)


# ── subcommands (no config imports) ──────────────────────────────────────────

def cmd_setup(server_dir: Path, codex_dir: Path, codex_config: Path, ui,
              _args: argparse.Namespace) -> None:
    ui.box(["Unity Biome MCP setup"])
    setup_env(server_dir, codex_dir, codex_config, ui)
    ui.ok("Done. Run 'python install.py doctor' to verify.")


def cmd_update(server_dir: Path, codex_dir: Path, codex_config: Path, ui,
               _args: argparse.Namespace) -> None:
    ui.box(["Unity Biome MCP update"])
    stale = venv_stale(server_dir)
    if stale:
        ui.info("Stale venv detected (folder moved).")
    setup_env(server_dir, codex_dir, codex_config, ui, force_recreate=stale)
    ui.ok("Done.")


def cmd_doctor(server_dir: Path, codex_config: Path, mcp_json: Path, ui,
               _args: argparse.Namespace) -> None:
    from unity_mcp.config.merger import SERVER_NAME

    def _check(label: str, result: bool, info: str = "") -> None:
        suffix = f" ({info})" if info else ""
        (ui.ok if result else ui.fail)(f"{label}{suffix}")

    ui.box(["Unity Biome MCP doctor"])
    _check("Python >= 3.10", check_python(), sys.version.split()[0])
    _check("uv found", shutil.which("uv") is not None)
    _check("uvx found", shutil.which("uvx") is not None)
    _check("git found", shutil.which("git") is not None)

    py = venv_python(server_dir)
    _check(".venv/python exists", py.exists(), str(py))

    importable = False
    if py.exists():
        r = subprocess.run([str(py), "-c", "import unity_mcp"], capture_output=True)
        importable = r.returncode == 0
    _check("unity_mcp importable", importable)

    paths_ok = False
    stale_entry = False
    if codex_config.exists():
        content = codex_config.read_text(encoding="utf-8")
        paths_ok = str(py) in content
        stale_entry = bool(re.search(r'^\[mcp_servers\.unity\]', content, re.MULTILINE))
    _check(".codex/config.toml paths correct", paths_ok)
    if stale_entry:
        ui.fail(".codex/config.toml has stale [mcp_servers.unity] — run: python install.py configure --tool codex")

    mcp_ok = False
    if mcp_json.exists():
        try:
            data = json.loads(mcp_json.read_text(encoding="utf-8"))
            mcp_ok = SERVER_NAME in data.get("mcpServers", {})
        except Exception:
            pass
    _check(".mcp.json configured", mcp_ok)

    # Check AI tool configs
    from unity_mcp.config.clients import CLIENT_REGISTRY
    from unity_mcp.config.resolver import GIT_INSTALL_URL as _GIT_URL
    for key, info in CLIENT_REGISTRY.items():
        if info.stdout_only or info.is_toml:
            continue
        if info.config_path.exists():
            try:
                content = info.config_path.read_text(encoding="utf-8")
                has_entry = f'"{SERVER_NAME}"' in content
                has_git_url = _GIT_URL in content
                if has_entry and not has_git_url:
                    _check(f"{info.name} config", False,
                           f"stale PyPI config — run: install.py configure --tool {key}")
                else:
                    _check(f"{info.name} config", has_entry,
                           str(info.config_path) if has_entry else f"{SERVER_NAME} missing")
            except OSError:
                pass

    port = discover_port()
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=1):
            ui.info(f"TCP :{port} — OPEN (Unity plugin listening)")
    except OSError:
        ui.info(f"TCP :{port} — closed (Unity not running or plugin disabled)")


def cmd_pull(repo_root: Path, ui) -> int:
    """git pull --tags for local clone installations. Returns 0 on success, 1 on failure."""
    if not (repo_root / ".git").exists():
        ui.error("Not a git clone. Download the latest release from GitHub.")
        return 1
    try:
        result = subprocess.run(["git", "pull", "--tags"], cwd=repo_root)
    except (FileNotFoundError, OSError) as exc:
        ui.error(f"git pull failed: {exc}")
        return 1
    if result.returncode != 0:
        ui.error("git pull failed — check the output above.")
        return 1
    ui.ok("Updated. Focus Unity to reload the plugin.")
    return 0


def cmd_uninstall(server_dir: Path, unity_mcp_data_dir: Path, ui, prompt_yn,
                  _args: argparse.Namespace) -> None:
    """Remove Unity Biome MCP venv and optionally ~/.unity-biome-mcp data."""
    venv_dir = server_dir / ".venv"
    if venv_dir.exists():
        ui.info(f"Removing {venv_dir} …")
        shutil.rmtree(venv_dir)
        ui.ok("venv removed")
    else:
        ui.info("No venv found, skipping")

    if unity_mcp_data_dir.exists():
        if prompt_yn(f"Remove {unity_mcp_data_dir} (port files, logs)?", default=False):
            shutil.rmtree(unity_mcp_data_dir)
            ui.ok(f"{unity_mcp_data_dir} removed")
        else:
            ui.info("Keeping data directory")


_MCP_PKGS = ("com.unity-biome-mcp.editor", "com.unity-biome-mcp.reload")
_REPO_ROOT = Path(__file__).parent.parent.resolve()


def cmd_connect(args: argparse.Namespace, ui) -> int:
    """Add file: references to a Unity project's manifest.json."""
    project_dir = Path(args.unity_project).resolve()
    manifest = project_dir / "Packages" / "manifest.json"

    if not manifest.exists():
        ui.error(f"Not a Unity project: {project_dir} (Packages/manifest.json not found)")
        return 1

    editor_path = _REPO_ROOT / "unity-plugin"
    reload_path = _REPO_ROOT / "unity-plugin-reload"

    data = json.loads(manifest.read_text("utf-8"))
    deps = data.setdefault("dependencies", {})

    editor_ref = f"file:{editor_path.as_posix()}"
    reload_ref = f"file:{reload_path.as_posix()}"
    if deps.get("com.unity-biome-mcp.editor") == editor_ref and deps.get("com.unity-biome-mcp.reload") == reload_ref:
        ui.ok("Already connected.")
        return 0

    shutil.copy2(manifest, manifest.with_suffix(".json.bak"))

    deps["com.unity-biome-mcp.editor"] = editor_ref
    deps["com.unity-biome-mcp.reload"] = reload_ref
    manifest.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", "utf-8")
    ui.ok(f"Connected to {project_dir.name}. Focus Unity to reload.")
    return 0


def cmd_disconnect(args: argparse.Namespace, ui) -> int:
    """Remove Unity Biome MCP file: references from manifest.json."""
    project_dir = Path(args.unity_project).resolve()
    manifest = project_dir / "Packages" / "manifest.json"

    if not manifest.exists():
        ui.error(f"Not a Unity project: {project_dir}")
        return 1

    data = json.loads(manifest.read_text("utf-8"))
    deps = data.get("dependencies", {})

    removed = any(pkg in deps for pkg in _MCP_PKGS)
    for pkg in _MCP_PKGS:
        deps.pop(pkg, None)

    testables = data.get("testables", [])
    for pkg in _MCP_PKGS:
        if pkg in testables:
            testables.remove(pkg)

    if not removed:
        ui.ok("Not connected — nothing to remove.")
        return 0

    shutil.copy2(manifest, manifest.with_suffix(".json.bak"))
    manifest.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", "utf-8")
    ui.ok("Disconnected. Focus Unity to unload plugin.")
    return 0
