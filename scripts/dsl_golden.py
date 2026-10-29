"""Print the MCP surface and the ToolSpec of every tool as the text lines of server/tests/golden/tool_surface.txt.

That file is the independent oracle of the tool catalog (dsl/tools.biome): it was taken at 86eca971, before any generator
existed, and is never regenerated to make a difference disappear. An intended change of a tool is an INTENDED entry, with its
reason, in server/tests/test_tool_surface_golden.py. This script only reproduces the lines (check the golden at its commit) and
prints the lines of a tool that is new.

Usage (from the repository root):
  python scripts/dsl_golden.py                 # header and every line
  python scripts/dsl_golden.py search_scene    # only the lines of the named tools
"""

import dataclasses
import hashlib
import importlib.metadata
import json
import pathlib
import platform
import sys

_SERVER_SRC = pathlib.Path(__file__).resolve().parent.parent / "server" / "src"
if str(_SERVER_SRC) not in sys.path:
    sys.path.insert(0, str(_SERVER_SRC))

HEADER = """\
# The MCP surface and the ToolSpec of every tool, taken at 86eca971 before any generator existed (the oracle of dsl/tools.biome).
# tool <name> <first 16 hex digits of the sha256 of the canonical JSON of each field: title, description, inputSchema, annotations>
# spec <name> <the fields of ToolSpec>
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_tool_surface_golden.py.
"""


def env() -> str:
    """The versions whose output the digests depend on (a pydantic or mcp upgrade can change every inputSchema)."""
    mcp_version, pydantic_version = (importlib.metadata.version(name) for name in ("mcp", "pydantic"))
    return f"# env python {platform.python_version()} mcp {mcp_version} pydantic {pydantic_version}"


def _digest(value) -> str:
    text = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def surface_lines(names: frozenset[str] = frozenset()) -> list[str]:
    """`tool` lines of the first-party MCP tools and `spec` lines of every ToolSpec, sorted by name; `names` narrows them."""
    from unity_mcp.server import mcp
    from unity_mcp.tools.tool_specs import _SPECS

    lines = []
    for name, tool in sorted(mcp._tool_manager._tools.items()):
        if not tool.fn.__module__.startswith("unity_mcp.") or (names and name not in names):
            continue
        annotations = tool.annotations.model_dump(exclude_none=True) if tool.annotations else None
        fields = {"title": tool.title, "description": tool.description, "schema": tool.parameters, "annotations": annotations}
        lines.append(f"tool {name} " + " ".join(f"{key}={_digest(value)}" for key, value in fields.items()))
    for name, spec in sorted(_SPECS.items()):
        if not names or name in names:
            lines.append(f"spec {name} " + " ".join(f"{f.name}={getattr(spec, f.name)}" for f in dataclasses.fields(spec)))
    return lines


def main(argv: list[str]) -> None:
    names = frozenset(argv)
    if not names:
        print(HEADER + env())
    print("\n".join(surface_lines(names)))


if __name__ == "__main__":
    main(sys.argv[1:])
