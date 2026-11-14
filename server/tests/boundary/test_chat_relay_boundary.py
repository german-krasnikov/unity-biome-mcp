"""Boundary test: core Python modules must not import from chat_relay or its siblings."""
import ast
from pathlib import Path

import pytest

_SRC = Path(__file__).resolve().parents[2] / "src" / "unity_mcp"

_CHAT_ONLY_MODULES = frozenset({
    "chat_relay", "cli_session", "relay_buffer", "agent_event",
    "stream_transform", "adapters.pipe_parser", "adapters.protocol",
})

_CORE_DIRS = [_SRC, _SRC / "tools", _SRC / "config", _SRC / "middleware"]


def _collect_imports(path: Path) -> list[str]:
    try:
        tree = ast.parse(path.read_text("utf-8"), filename=str(path))
    except SyntaxError:
        return []
    names: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            names.extend(a.name for a in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            names.append(node.module)
    return names


def _core_python_files():
    for d in _CORE_DIRS:
        if not d.is_dir():
            continue
        for f in d.glob("*.py"):
            if f.name.startswith("_") and f.name != "__init__.py":
                continue
            if f.name in ("chat_relay.py", "relay_buffer.py", "agent_event.py",
                         "stream_transform.py", "cli_session.py"):
                continue
            yield f


@pytest.mark.parametrize("path", list(_core_python_files()), ids=lambda p: p.name)
def test_core_module_does_not_import_chat_modules(path: Path):
    imports = _collect_imports(path)
    for imp in imports:
        parts = imp.replace("unity_mcp.", "").split(".")
        base = parts[0] if len(parts) == 1 else f"{parts[0]}.{parts[1]}"
        assert base not in _CHAT_ONLY_MODULES, (
            f"{path.name} imports chat-only module '{imp}' — "
            f"core must not depend on chat"
        )
