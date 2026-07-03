"""Collect authoritative stats for README/SVG generation.

This module is the ONLY place numbers are computed.
Reproduce commands (run from repo root):
  tools:         python3 -c "import scripts.readme_facts as f; print(f.count_mcp_tools(...))"
  tests_python:  source server/.venv/bin/activate && python -m pytest server/tests/ --collect-only -q -m "not live" 2>&1 | tail -1
  tests_live:    source server/.venv/bin/activate && python -m pytest server/tests/ --collect-only -q -m "live" 2>&1 | tail -1
  tests_unity:   TCP get_test_results (accurate) or grep fallback
  server_ver:    grep -m1 '^version' server/pyproject.toml
  plugin_ver:    python3 -c "import json; print(json.load(open('unity-plugin/package.json'))['version'])"

Pure stdlib, no pip deps.
"""
import ast
import json
import pathlib
import re
import shutil
import subprocess
import sys
import warnings
from dataclasses import dataclass


def _find_pytest_python(repo_root: pathlib.Path) -> str:
    """Find a Python interpreter that has pytest installed."""
    venv_python = repo_root / "server" / ".venv" / "bin" / "python"
    if venv_python.exists():
        return str(venv_python)
    brew = shutil.which("python3.12") or shutil.which("python3.11")
    if brew:
        return brew
    return sys.executable
from typing import Optional

try:
    import tomllib  # Python 3.11+
except ImportError:
    try:
        import tomli as tomllib  # type: ignore[no-redef]
    except ImportError:
        tomllib = None  # type: ignore[assignment]


# ---------------------------------------------------------------------------
# Individual counters (re-exported for backward compat with update_readme)
# ---------------------------------------------------------------------------

def count_mcp_tools(src_dir: pathlib.Path) -> Optional[int]:
    """Count mcp.tool() registrations across all .py files in src_dir (recursive).

    Reproduce: python3 -c "
      import sys; sys.path.insert(0,'scripts'); import readme_facts as rf; import pathlib
      print(rf.count_mcp_tools(pathlib.Path('server/src/unity_mcp')))"
    """
    if not src_dir.exists():
        return None
    count = 0
    for py in src_dir.rglob("*.py"):
        try:
            tree = ast.parse(py.read_text(encoding="utf-8"))
        except SyntaxError:
            continue
        inner_tool_calls: set[int] = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Call):
                func = node.func
                if isinstance(func, ast.Call):
                    inner_func = func.func
                    if (isinstance(inner_func, ast.Attribute) and
                            inner_func.attr == "tool"):
                        inner_tool_calls.add(id(func))
                        count += 1
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and id(node) not in inner_tool_calls:
                func = node.func
                if isinstance(func, ast.Attribute) and func.attr == "tool":
                    count += 1
    return count


def count_pytest_python(tests_dir: pathlib.Path) -> int:
    """Count non-live Python tests via pytest --collect-only."""
    if not tests_dir.exists():
        return 0
    py = _find_pytest_python(tests_dir.parent.parent)
    try:
        result = subprocess.run(
            [py, "-m", "pytest", str(tests_dir),
             "--co", "-q", "--no-header", "-m", "not live"],
            capture_output=True, text=True, timeout=120,
        )
        return sum(1 for l in result.stdout.splitlines() if "::" in l)
    except Exception as e:
        warnings.warn(f"pytest collection failed: {e}")
        return 0


def count_pytest_live(tests_dir: pathlib.Path) -> int:
    """Count live Python tests via pytest --collect-only -m live."""
    if not tests_dir.exists():
        return 0
    py = _find_pytest_python(tests_dir.parent.parent)
    try:
        result = subprocess.run(
            [py, "-m", "pytest", str(tests_dir),
             "--co", "-q", "--no-header", "-m", "live"],
            capture_output=True, text=True, timeout=120,
        )
        return sum(1 for l in result.stdout.splitlines() if "::" in l)
    except Exception as e:
        warnings.warn(f"pytest live collection failed: {e}")
        return 0


def _count_unity_tests_tcp() -> Optional[int]:
    """Try to get NUnit count from running Unity via TCP (discovery, no test run)."""
    import asyncio, struct, time
    async def _ask(port: int) -> Optional[str]:
        r, w = await asyncio.open_connection("127.0.0.1", port)
        msg = json.dumps({"cmd": "get_test_count", "args": {}}).encode()
        w.write(struct.pack(">I", len(msg)) + msg)
        await w.drain()
        raw = await asyncio.wait_for(r.readexactly(
            struct.unpack(">I", await asyncio.wait_for(r.readexactly(4), 3))[0]), 3)
        w.close()
        return json.loads(raw).get("data", "")
    async def _query():
        for port_file in pathlib.Path.home().glob(".unity-mcp/ports/*.port"):
            try:
                port = int(port_file.read_text().split("\n")[0])
            except Exception:
                continue
            for _ in range(5):
                try:
                    data = await _ask(port)
                    if data == "discovering":
                        await asyncio.sleep(1)
                        continue
                    m = re.match(r"(\d+)\|", data or "")
                    if m:
                        return int(m.group(1))
                except Exception:
                    break
        return None
    try:
        return asyncio.run(_query())
    except Exception:
        return None


@dataclass
class TestCount:
    """A3: count_unity_tests() result with provenance -- TCP-live and
    static-grep are structurally different counting methods (static grep
    undercounts anything not literally spelled [Test]/[UnityTest]/[TestCase(],
    e.g. [TestCaseSource(...)] expands to N tests from 1 attribute). Callers
    must be able to tell which method produced a given number.
    """
    count: int
    source: str  # "tcp" | "static_grep" | "unavailable"


def count_unity_tests(plugin_dir: pathlib.Path) -> TestCount:
    """Get NUnit test count from Unity TCP (accurate) or static grep (fallback).

    TCP reads the actual last-run result (handles TestCaseSource, Values, etc.).
    Static grep counts [Test], [UnityTest], [TestCase(] attributes.
    """
    tcp_count = _count_unity_tests_tcp()
    if tcp_count is not None and tcp_count > 0:
        return TestCount(tcp_count, "tcp")
    if not plugin_dir.exists():
        return TestCount(0, "unavailable")
    count = 0
    bare = re.compile(r"\[(?:Test|UnityTest)\]")
    case_attr = re.compile(r"\[TestCase\(")
    for cs_file in plugin_dir.rglob("*.cs"):
        try:
            text = cs_file.read_text(encoding="utf-8")
            count += len(case_attr.findall(text)) + len(bare.findall(text))
        except Exception:
            continue
    return TestCount(count, "static_grep")


def read_server_version(pyproject: pathlib.Path) -> Optional[str]:
    """Read version from server/pyproject.toml.

    Reproduce: grep -m1 '^version' server/pyproject.toml
    """
    if not pyproject.exists():
        return None
    if tomllib is None:
        m = re.search(r'^version\s*=\s*"([^"]+)"', pyproject.read_text(), re.MULTILINE)
        return m.group(1) if m else None
    with open(pyproject, "rb") as f:
        data = tomllib.load(f)
    return data.get("project", {}).get("version")


def read_plugin_version(package_json: pathlib.Path) -> Optional[str]:
    """Read version from unity-plugin/package.json.

    Reproduce: python3 -c "import json; print(json.load(open('unity-plugin/package.json'))['version'])"
    """
    if not package_json.exists():
        return None
    return json.loads(package_json.read_text(encoding="utf-8")).get("version")


# ---------------------------------------------------------------------------
# Main collector
# ---------------------------------------------------------------------------

def collect_facts(repo_root: pathlib.Path) -> dict:
    """Compute all volatile stats. Writes nothing — returns a dict.

    Call collect_facts(repo_root) then write_meta_json(repo_root, facts) to persist.
    """
    tests_dir = repo_root / "server" / "tests"
    plugin_dir = repo_root / "unity-plugin"

    tools = count_mcp_tools(repo_root / "server" / "src" / "unity_mcp") or 0
    python_tests = count_pytest_python(tests_dir)
    live_tests = count_pytest_live(tests_dir)
    unity_result = count_unity_tests(plugin_dir)
    server_ver = read_server_version(repo_root / "server" / "pyproject.toml") or "?"
    plugin_ver = read_plugin_version(repo_root / "unity-plugin" / "package.json") or "?"

    return {
        "tools": tools,
        "tests_total": python_tests + unity_result.count + live_tests,
        "tests_python": python_tests,
        "tests_unity": unity_result.count,
        "tests_unity_source": unity_result.source,
        "tests_live": live_tests,
        "server_version": server_ver,
        "plugin_version": plugin_ver,
        "batch_savings": "80–95%",
    }


def write_meta_json(repo_root: pathlib.Path, facts: dict) -> pathlib.Path:
    """Persist facts to docs/assets/_meta.json."""
    path = repo_root / "docs" / "assets" / "_meta.json"
    path.write_text(json.dumps(facts, indent=2) + "\n", encoding="utf-8")
    return path


def read_meta_json(repo_root: pathlib.Path) -> dict:
    """Read docs/assets/_meta.json. Raises FileNotFoundError if missing."""
    path = repo_root / "docs" / "assets" / "_meta.json"
    return json.loads(path.read_text(encoding="utf-8"))


def load_meta(repo_root: pathlib.Path) -> dict:
    """Read docs/assets/_meta.json. Returns {} if missing."""
    try:
        return read_meta_json(repo_root)
    except (FileNotFoundError, json.JSONDecodeError):
        return {}
