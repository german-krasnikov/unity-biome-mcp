"""Import-time guard: turn a Domain-A crash into one stderr line instead of a traceback.

Runs BEFORE any other unity_mcp.* import. Must stay stdlib-only (sys, importlib.util) —
importing anything from this package here would defeat its own purpose.
"""
import importlib.util
import sys

_MIN_PYTHON = (3, 10)
_FIX_HINT = (
    "uvx --reinstall --from "
    "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp"
)


def run_preflight() -> None:
    """No-op on success. On failure: one line to stderr, sys.exit(2)."""
    if sys.version_info < _MIN_PYTHON:
        found = ".".join(str(p) for p in sys.version_info[:3])
        needed = ".".join(str(p) for p in _MIN_PYTHON)
        print(
            f"UNITY-BIOME-MCP-FATAL: Python {found} found, need >={needed} "
            f"| fix: use a newer interpreter, e.g. uvx --python 3.12 --from "
            f"git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server unity-biome-mcp",
            file=sys.stderr,
        )
        sys.exit(2)

    if importlib.util.find_spec("mcp") is None:
        print(
            f"UNITY-BIOME-MCP-FATAL: mcp SDK not installed in this interpreter | fix: {_FIX_HINT}",
            file=sys.stderr,
        )
        sys.exit(2)
