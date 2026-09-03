"""Repo-wide guard: docstrings must lead with behavior, not a ticket code.

Three prior cleanup commits (6eb145ae, 5db2c9c0, 65910619) each rewrote only
the specific files their own review round had flagged, instead of adding a
sweep-once guard -- so the same anti-pattern kept resurfacing in files no
round happened to name (C1 r4 #4). This test walks every test_*.py file
under server/tests and install/tests via ast, not import, so it also catches
a live-only or optional-dependency module a plain `import` would skip.

A second walker (C1 r6 #3) covers production modules: the test-only walk let
a ticket-code-leading docstring slip into server/src/unity_mcp/bridge.py
because that file lives outside server/tests and install/tests. It reuses
the same _TICKET_PREFIX regex over every FunctionDef/AsyncFunctionDef/
ClassDef docstring, not just `test_*`, in server/src/unity_mcp, install/*.py,
install.py, and scripts/*.py.
"""
import ast
import pathlib
import re

_TICKET_PREFIX = re.compile(r"^\s*(ARC-\d|DEV-\d|C1[- ]|QUALITY-|B[23]-|R\d-\d|P\d+[: ])")
_REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
_TEST_DIRS = (_REPO_ROOT / "server" / "tests", _REPO_ROOT / "install" / "tests")
_PRODUCTION_DIRS = (_REPO_ROOT / "server" / "src" / "unity_mcp",)
_PRODUCTION_GLOB_DIRS = ((_REPO_ROOT / "install", "*.py"), (_REPO_ROOT / "scripts", "*.py"))
_PRODUCTION_FILES = (_REPO_ROOT / "install.py",)


def _iter_test_functions():
    for test_dir in _TEST_DIRS:
        for path in sorted(test_dir.glob("*.py")):
            tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
            for node in ast.walk(tree):
                is_test_def = isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
                if is_test_def and node.name.startswith("test_"):
                    yield path, node


def _production_paths():
    for prod_dir in _PRODUCTION_DIRS:
        yield from sorted(prod_dir.rglob("*.py"))
    for prod_dir, pattern in _PRODUCTION_GLOB_DIRS:
        yield from sorted(prod_dir.glob(pattern))
    for prod_file in _PRODUCTION_FILES:
        if prod_file.exists():
            yield prod_file


def _iter_production_definitions():
    for path in _production_paths():
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
                yield path, node


def test_no_test_docstring_leads_with_a_ticket_code():
    """A `def test_*` docstring must describe behavior first -- a leading
    ticket/sprint code (ARC-6, DEV-63, C1-FIX-01, B2-P9, R4-01, P1:) belongs
    nowhere in the sentence a reader scans first, per this repo's own
    behavior-first rule (already enforced ad hoc by three prior commits)."""
    offenders = []
    for path, node in _iter_test_functions():
        docstring = ast.get_docstring(node)
        if docstring and _TICKET_PREFIX.match(docstring):
            offenders.append(f"{path.relative_to(_REPO_ROOT)}:{node.lineno} {node.name}")
    assert not offenders, "Ticket-code-leading docstrings found:\n" + "\n".join(offenders)


def test_no_production_docstring_leads_with_a_ticket_code():
    """A production function/class docstring must describe behavior first --
    the same leading-ticket-code anti-pattern the test-only guard above
    catches also slips into server/src/unity_mcp, install, and scripts
    modules, so this walks every FunctionDef/AsyncFunctionDef/ClassDef there,
    not just `test_*`."""
    offenders = []
    for path, node in _iter_production_definitions():
        docstring = ast.get_docstring(node)
        if docstring and _TICKET_PREFIX.match(docstring):
            offenders.append(f"{path.relative_to(_REPO_ROOT)}:{node.lineno} {node.name}")
    assert not offenders, "Ticket-code-leading docstrings found:\n" + "\n".join(offenders)
