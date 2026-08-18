"""Collect source-backed inventory facts for README/SVG generation.

This module is the ONLY place numbers are computed.
Reproduce commands (run from repo root):
  tools:         public entries in server/src/unity_mcp/tools/tool_specs.py
  tests_python:  pytest collection excluding live, live_cli, live_chat, and monkey
  tests_stress:  pytest collection selected by monkey only
  tests_live:    pytest collection selected by live, live_cli, or live_chat
  tests_unity:   static scan of NUnit test attributes in unity-plugin/

Pure stdlib, no pip deps.
"""
import ast
import json
import re
import subprocess
import sys
import tomllib
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    import pathlib


def _find_pytest_python(repo_root: pathlib.Path) -> str:
    """Find a Python interpreter that has pytest installed."""
    venv_python = repo_root / "server" / ".venv" / "bin" / "python"
    if venv_python.exists():
        return str(venv_python)
    return sys.executable


# ---------------------------------------------------------------------------
# Individual counters (re-exported for backward compat with update_readme)
# ---------------------------------------------------------------------------

def _count_public_tool_specs(specs_file: pathlib.Path) -> int:
    """Count public ToolSpec entries while excluding protocol-only commands."""
    tree = ast.parse(specs_file.read_text(encoding="utf-8"), filename=str(specs_file))
    specs_dict: ast.Dict | None = None
    for node in tree.body:
        if (
            isinstance(node, ast.AnnAssign)
            and isinstance(node.target, ast.Name)
            and node.target.id == "_SPECS"
            and isinstance(node.value, ast.Dict)
        ):
            specs_dict = node.value
            break
        if (
            isinstance(node, ast.Assign)
            and isinstance(node.value, ast.Dict)
            and any(isinstance(target, ast.Name) and target.id == "_SPECS" for target in node.targets)
        ):
            specs_dict = node.value
            break

    if specs_dict is None:
        raise ValueError(f"_SPECS dictionary not found in {specs_file}")

    count = 0
    for value in specs_dict.values:
        if not isinstance(value, ast.Call):
            raise ValueError(f"Unexpected _SPECS entry in {specs_file}")
        category = next(
            (keyword.value for keyword in value.keywords if keyword.arg == "category"),
            None,
        )
        if not isinstance(category, ast.Constant) or not isinstance(category.value, str):
            raise ValueError(f"ToolSpec category must be a string in {specs_file}")
        if category.value != "_INTERNAL":
            count += 1
    return count


def count_mcp_tools(src_dir: pathlib.Path) -> int | None:
    """Count public tools from ToolSpec SSOT, with a generic source fallback.

    Reproduce: python3 -c "
      import sys; sys.path.insert(0,'scripts'); import readme_facts as rf; import pathlib
      print(rf.count_mcp_tools(pathlib.Path('server/src/unity_mcp')))"
    """
    if not src_dir.exists():
        return None
    specs_file = src_dir / "tools" / "tool_specs.py"
    if specs_file.exists():
        return _count_public_tool_specs(specs_file)

    # Generic fallback retained for isolated collector tests and external reuse.
    count = 0
    for py in src_dir.rglob("*.py"):
        tree = ast.parse(py.read_text(encoding="utf-8"), filename=str(py))
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


_REGULAR_MARKERS = "not live and not live_cli and not live_chat and not monkey"
_STRESS_MARKERS = "monkey and not live and not live_cli and not live_chat"
_LIVE_MARKERS = "live or live_cli or live_chat"


def _count_pytest_marked(
    tests_dir: pathlib.Path,
    marker_expression: str,
    *,
    ignore_live_directory: bool,
) -> int:
    if not tests_dir.exists():
        return 0
    py = _find_pytest_python(tests_dir.parent.parent)
    command = [
        py,
        "-m",
        "pytest",
        str(tests_dir),
        "--co",
        "-q",
        "--no-header",
        "--strict-markers",
        "-m",
        marker_expression,
    ]
    if ignore_live_directory:
        command.extend(["--ignore", str(tests_dir / "live")])

    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        raise RuntimeError(f"pytest collection failed: {error}") from error
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise RuntimeError(
            f"pytest collection exited {result.returncode}: {detail[-2000:]}"
        )
    return sum(1 for line in result.stdout.splitlines() if "::" in line)


def count_pytest_python(tests_dir: pathlib.Path) -> int:
    """Count regular Python tests, excluding live and stress suites."""
    return _count_pytest_marked(
        tests_dir,
        _REGULAR_MARKERS,
        ignore_live_directory=True,
    )


def count_pytest_stress(tests_dir: pathlib.Path) -> int:
    """Count non-live monkey/stress tests."""
    return _count_pytest_marked(
        tests_dir,
        _STRESS_MARKERS,
        ignore_live_directory=True,
    )


def count_pytest_live(tests_dir: pathlib.Path) -> int:
    """Count all live integration categories without running them."""
    return _count_pytest_marked(
        tests_dir,
        _LIVE_MARKERS,
        ignore_live_directory=False,
    )


@dataclass
class TestCount:
    """A Unity source-inventory count with its collection method."""
    count: int
    source: str  # "static_grep" | "unavailable"


_CSHARP_NON_CODE = re.compile(
    r"//[^\r\n]*"
    r"|/\*.*?\*/"
    r'|\$*"{3,}.*?"{3,}'
    r'|(?:\$@|@\$|@)"(?:""|[^"])*"'
    r'|\$?"(?:\\.|[^"\\])*"'
    r"|'(?:\\.|[^'\\])*'",
    re.DOTALL,
)


def count_unity_tests(plugin_dir: pathlib.Path) -> TestCount:
    """Count NUnit source attributes deterministically without contacting Unity.

    Counts [Test], [UnityTest], and [TestCase(...)] attributes. This is an
    inventory of source declarations, not executed or discovered NUnit cases;
    generated cases such as TestCaseSource can be undercounted.
    """
    if not plugin_dir.exists():
        return TestCount(0, "unavailable")
    count = 0
    bare = re.compile(r"\[(?:Test|UnityTest)\]")
    case_attr = re.compile(r"\[TestCase\(")
    for cs_file in plugin_dir.rglob("*.cs"):
        text = cs_file.read_text(encoding="utf-8")
        text = _CSHARP_NON_CODE.sub("", text)
        count += len(case_attr.findall(text)) + len(bare.findall(text))
    return TestCount(count, "static_grep")


def read_server_version(pyproject: pathlib.Path) -> str | None:
    """Read version from server/pyproject.toml.

    Reproduce: grep -m1 '^version' server/pyproject.toml
    """
    if not pyproject.exists():
        return None
    with open(pyproject, "rb") as f:
        data = tomllib.load(f)
    return data.get("project", {}).get("version")


def read_plugin_version(package_json: pathlib.Path) -> str | None:
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
    """Compute all volatile inventory facts without executing test suites.

    Call collect_facts(repo_root) then write_meta_json(repo_root, facts) to persist.
    """
    tests_dir = repo_root / "server" / "tests"
    plugin_dir = repo_root / "unity-plugin"

    tools = count_mcp_tools(repo_root / "server" / "src" / "unity_mcp") or 0
    python_tests = count_pytest_python(tests_dir)
    stress_tests = count_pytest_stress(tests_dir)
    live_tests = count_pytest_live(tests_dir)
    unity_result = count_unity_tests(plugin_dir)
    server_ver = read_server_version(repo_root / "server" / "pyproject.toml") or "?"
    plugin_ver = read_plugin_version(repo_root / "unity-plugin" / "package.json") or "?"

    return {
        "tools": tools,
        "tests_total": python_tests + stress_tests + unity_result.count + live_tests,
        "tests_python": python_tests,
        "tests_stress": stress_tests,
        "tests_unity": unity_result.count,
        "tests_unity_source": unity_result.source,
        "tests_live": live_tests,
        "server_version": server_ver,
        "plugin_version": plugin_ver,
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
