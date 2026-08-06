"""Export all non-_INTERNAL MCP tool definitions to JSON.

Usage:
    python scripts/export_tools.py                            # toolsmith format, stdout
    python scripts/export_tools.py --format mcplint           # bare array, stdout
    python scripts/export_tools.py --format toolsmith --out tools.json
"""
from __future__ import annotations

import json
import pathlib
import sys
from typing import Literal

# Allow running from repo root without installing the package
_SERVER_SRC = pathlib.Path(__file__).parent.parent / "server" / "src"
if str(_SERVER_SRC) not in sys.path:
    sys.path.insert(0, str(_SERVER_SRC))


def _load_tools() -> list[dict]:
    from unity_mcp.server import mcp  # noqa: PLC0415
    from unity_mcp.tools.tool_specs import _SPECS  # noqa: PLC0415

    if not hasattr(mcp, "_tool_manager"):
        raise RuntimeError("FastMCP._tool_manager missing — check FastMCP version")

    internal = {name for name, spec in _SPECS.items() if spec.category == "_INTERNAL"}
    tools_map: dict = mcp._tool_manager._tools

    result = []
    for name in sorted(tools_map):
        if name in internal:
            continue
        tool = tools_map[name]
        entry = {
            "name": name,
            "title": tool.title,
            "description": tool.description,
            "inputSchema": tool.parameters,
        }
        ann = getattr(tool, "annotations", None)
        if ann is not None:
            entry["annotations"] = {
                k: v for k, v in ann.model_dump().items() if v is not None
            }
        result.append(entry)
    return result


def _spec_version() -> str:
    """8-char sha256 of sorted spec names+categories — detects schema drift."""
    import hashlib  # noqa: PLC0415

    from unity_mcp.tools.tool_specs import _SPECS  # noqa: PLC0415
    payload = json.dumps(
        sorted((name, spec.category) for name, spec in _SPECS.items()),
        separators=(",", ":"),
    ).encode()
    return hashlib.sha256(payload).hexdigest()[:8]


def export_json(fmt: Literal["toolsmith", "mcplint"] = "toolsmith") -> str:
    tools = _load_tools()
    if fmt == "mcplint":
        return json.dumps(tools, indent=2)
    return json.dumps({"version": _spec_version(), "tools": tools}, indent=2)


def main(argv: list[str] | None = None) -> None:
    import argparse  # noqa: PLC0415

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--format", choices=["toolsmith", "mcplint"], default="toolsmith")
    parser.add_argument("--out", metavar="FILE", help="Write to file instead of stdout")
    args = parser.parse_args(argv)

    output = export_json(fmt=args.format)

    if args.out:
        out = pathlib.Path(args.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(output, encoding="utf-8")
    else:
        print(output)


if __name__ == "__main__":
    main()
