"""Repo-wide guard: test docstrings must lead with behavior, not a ticket code.

Three prior cleanup commits (6eb145ae, 5db2c9c0, 65910619) each rewrote only
the specific files their own review round had flagged, instead of adding a
sweep-once guard -- so the same anti-pattern kept resurfacing in files no
round happened to name (C1 r4 #4). This test walks every test_*.py file
under server/tests and install/tests via ast, not import, so it also catches
a live-only or optional-dependency module a plain `import` would skip.
"""
import ast
import pathlib
import re

_TICKET_PREFIX = re.compile(r"^\s*(ARC-\d|DEV-\d|C1[- ]|QUALITY-|B[23]-|R\d-\d|P\d+[: ])")
_REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
_TEST_DIRS = (_REPO_ROOT / "server" / "tests", _REPO_ROOT / "install" / "tests")


def _iter_test_functions():
    for test_dir in _TEST_DIRS:
        for path in sorted(test_dir.glob("*.py")):
            tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
            for node in ast.walk(tree):
                is_test_def = isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
                if is_test_def and node.name.startswith("test_"):
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
