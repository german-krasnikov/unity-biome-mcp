"""Fence tests: ensure Python 3.14 idioms are maintained across the codebase."""

import sys
import typing
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SRC_ROOT = Path(__file__).resolve().parent.parent / "src" / "unity_mcp"
TEST_ROOT = Path(__file__).resolve().parent
INSTALL_ROOT = _REPO_ROOT / "install"
SCRIPTS_ROOT = _REPO_ROOT / "scripts"
_SELF = Path(__file__).resolve()


def _py_files(root: Path) -> list[Path]:
    return sorted(f for f in root.rglob("*.py") if f != _SELF)


def _violations(root: Path, pattern: str) -> list[str]:
    return [str(f.relative_to(root)) for f in _py_files(root) if pattern in f.read_text(encoding="utf-8")]


class TestPython314Compliance:
    """Fence tests preventing regression to pre-3.14 patterns."""

    def test_runtime_version(self):
        assert sys.version_info >= (3, 14), f"Running on {sys.version}"

    def test_no_future_annotations_in_src(self):
        v = _violations(SRC_ROOT, "from __future__ import annotations")
        assert not v, f"Files still importing __future__ annotations: {v}"

    def test_no_future_annotations_in_tests(self):
        v = _violations(TEST_ROOT, "from __future__ import annotations")
        assert not v, f"Test files still importing __future__ annotations: {v}"

    def test_no_future_annotations_in_install(self):
        v = _violations(INSTALL_ROOT, "from __future__ import annotations")
        assert not v, f"install/ files still importing __future__ annotations: {v}"

    def test_no_future_annotations_in_scripts(self):
        v = _violations(SCRIPTS_ROOT, "from __future__ import annotations")
        assert not v, f"scripts/ files still importing __future__ annotations: {v}"

    def test_no_asyncio_get_event_loop(self):
        """Use get_running_loop() instead of deprecated get_event_loop()."""
        violations = []
        for f in _py_files(SRC_ROOT):
            text = f.read_text(encoding="utf-8")
            # Skip files that provide a compatibility shim using both
            if "get_event_loop()" in text and "get_running_loop" not in text:
                violations.append(str(f.relative_to(SRC_ROOT)))
        assert not violations, f"Files using deprecated get_event_loop(): {violations}"

    def test_no_asyncio_ensure_future(self):
        v = _violations(SRC_ROOT, "ensure_future")
        assert not v, f"Files using deprecated ensure_future(): {v}"

    def test_no_asyncio_iscoroutinefunction(self):
        v = _violations(SRC_ROOT, "asyncio.iscoroutinefunction")
        assert not v, f"Files using removed asyncio.iscoroutinefunction: {v}"

    def test_no_optional_in_src(self):
        v = _violations(SRC_ROOT, "Optional[")
        assert not v, f"Files using Optional[] instead of X | None: {v}"

    def test_pyproject_requires_python(self):
        pyproject = Path(__file__).resolve().parent.parent / "pyproject.toml"
        text = pyproject.read_text(encoding="utf-8")
        assert 'requires-python = ">=3.14"' in text, "requires-python must be >=3.14"

    def test_all_annotations_resolve(self):
        """Every public callable's annotations must resolve under PEP 649.

        Catches regressions where ruff --unsafe-fixes moved a runtime import to
        TYPE_CHECKING, breaking pydantic / MCP SDK calls to get_type_hints().
        """
        import importlib
        import pkgutil

        pkg = importlib.import_module("unity_mcp")
        failures = []
        for info in pkgutil.walk_packages(pkg.__path__, prefix="unity_mcp."):
            # unity_mcp.__version__ is a real submodule (src/unity_mcp/__version__.py).
            # Importing it here rebinds sys.modules['unity_mcp'].__version__ from
            # the package's version STRING (set in __init__.py) to this submodule
            # object — skip it so the walk doesn't corrupt process-global state.
            if info.name.endswith(".__version__"):
                continue
            try:
                mod = importlib.import_module(info.name)
            except Exception as e:
                if "No module named" not in str(e):
                    failures.append(f"{info.name}: import failed: {e}")
                continue
            for name, obj in vars(mod).items():
                if not callable(obj) or not hasattr(obj, "__annotations__"):
                    continue
                try:
                    typing.get_type_hints(obj, include_extras=True)
                except Exception as e:
                    failures.append(f"{info.name}.{name}: {e}")
        assert not failures, "Annotation resolution failures:\n" + "\n".join(failures)
        assert isinstance(pkg.__version__, str), (
            "walking unity_mcp.__version__ as a submodule must not leave "
            "unity_mcp.__version__ rebound to the module object"
        )
