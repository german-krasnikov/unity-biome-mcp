"""Static source-scan guard for this repo's own tests/<module>/ layout
(architecture plan: `Plans/task-p4-python-test-restructure.md`, "Import
Boundary Enforcement"). Opt-in and additive per module -- this file does not
try to pre-encode every possible cross-module rule (KISS); it starts with the
one boundary that both has a real production-module distinction to check
against and currently holds with zero exceptions in the real tree:
tests/tools/*.py must not reach into the middleware pipeline/guard decision
internals (unity_mcp.middleware_pipeline / unity_mcp.middleware_guards)
directly -- a tools test exercises the wrapped `send`/`args` surface a tool
module actually calls, not the pipeline's own decision machinery, which is
tests/middleware/'s job.

A `bridge ↛ tools internals` rule was evaluated too (the other boundary named
in the task) but dropped for now: real tests/bridge/*.py files already
import several tools/ submodules as integration vehicles (code_intel,
connection, diagnose, editor_state, objects, runtime, testing, tool_specs) on
top of the bridge-owned tools/sync*.py and tools/reload_ladder.py, so a
blanket ban would need an allowlist wide enough to catch nothing -- a
false-green guard. Add it later only once a real bridge-test-reaching-into-
tool-internals violation is found to shape the allowlist around.
"""
import re
from pathlib import Path

_TOOLS_DIR = Path(__file__).resolve().parent.parent / "tools"
_FORBIDDEN_IMPORT_RE = re.compile(
    r"^\s*(?:from|import)\s+(unity_mcp\.middleware_(?:pipeline|guards))\b",
    re.MULTILINE,
)


def _scan_for_forbidden_imports(directory: Path) -> list[str]:
    """Each tests/<module>/test_*.py must not import a forbidden production
    module -- mirrors test_docstring_hygiene.py's scan-and-assert-empty
    pattern."""
    violations = []
    for test_file in sorted(directory.glob("test_*.py")):
        text = test_file.read_text(encoding="utf-8")
        for match in _FORBIDDEN_IMPORT_RE.finditer(text):
            violations.append(f"{test_file.name}: imports {match.group(1)}")
    return violations


def test_tools_tests_do_not_import_middleware_pipeline_or_guards_directly():
    """tests/tools/*.py sees the wrapped `send`/`args` surface a tool module
    calls, not the middleware pipeline's own decision internals -- those
    belong to tests/middleware/."""
    violations = _scan_for_forbidden_imports(_TOOLS_DIR)
    assert not violations, "\n".join(violations)


def test_scan_catches_an_injected_violation(tmp_path):
    """Negative control: the scanner must actually fail on a real violation,
    not just vacuously pass on an empty/misconfigured glob."""
    (tmp_path / "test_injected_violation.py").write_text(
        "from unity_mcp.middleware_pipeline import Pipeline\n",
        encoding="utf-8",
    )
    violations = _scan_for_forbidden_imports(tmp_path)
    assert violations == ["test_injected_violation.py: imports unity_mcp.middleware_pipeline"]
