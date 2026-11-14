"""Print the MCP surface, the ToolSpec and the wire of every tool as the text lines of server/tests/golden/tool_surface.txt.

That file is the independent oracle of the tool catalog (dsl/tools.biome): it was taken at 86eca971, before any generator
existed, and is never regenerated to make a difference disappear. An intended change of a tool is an INTENDED entry, with its
reason, in server/tests/test_tool_surface_golden.py. This script only reproduces the lines (check the golden at its commit) and
prints the lines of a tool that is new.

`wire` lines cover the wrappers that only forward: a wrapper that calls nothing but _send, _args, builtins and string methods is
run against a recording _send with a fixed set of calls (the base call, every parameter at each of its sample values, 40 seeded
combinations); the line holds the digest of everything it sent (command, arguments with their types) and returned. A generated
wrapper therefore has to send, byte for byte, what the hand-written one sent.

Usage (from the repository root):
  python scripts/dsl_golden.py                 # header and every line
  python scripts/dsl_golden.py search_scene    # only the lines of the named tools
"""

import ast
import asyncio
import dataclasses
import hashlib
import importlib.metadata
import inspect
import json
import pathlib
import platform
import random
import sys
import textwrap
import typing

_SERVER_SRC = pathlib.Path(__file__).resolve().parent.parent / "server" / "src"
if str(_SERVER_SRC) not in sys.path:
    sys.path.insert(0, str(_SERVER_SRC))

HEADER = """\
# The MCP surface and the ToolSpec of every tool, taken at 86eca971 before any generator existed (the oracle of dsl/tools.biome).
# tool <name> <first 16 hex digits of the sha256 of the canonical JSON of each field: title, description, inputSchema, annotations>
# spec <name> <the fields of ToolSpec>
# wire <name> calls=<number of calls> digest=<first 16 hex digits of the sha256 of what the wrapper sent and returned for them>
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_tool_surface_golden.py.
"""


def env() -> str:
    """The versions whose output the digests depend on (a pydantic or mcp upgrade can change every inputSchema)."""
    mcp_version, pydantic_version = (importlib.metadata.version(name) for name in ("mcp", "pydantic"))
    return f"# env python {platform.python_version()} mcp {mcp_version} pydantic {pydantic_version}"


def _digest(value) -> str:
    text = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


_CALLS = {"_send", "_args", "str", "int", "float", "bool", "max", "min", "len", "abs", "round", "isinstance"}
_METHODS = {"get", "items", "lower", "strip", "split", "join", "startswith", "endswith", "replace"}
_UNSAFE = (ast.Import, ast.ImportFrom, ast.Global, ast.Nonlocal, ast.Try, ast.With, ast.AsyncWith, ast.Lambda)
_SAMPLES = {str: ["a", ""], str | None: [None, "a", ""], int: [0, 7, -1], int | None: [None, 0, 7], float: [0.0, 2.5],
            float | None: [None, 0.0, 2.5], bool: [True, False], bool | None: [None, True, False]}


def only_forwards(fn) -> bool:
    """True when the wrapper calls nothing but _send, _args, builtins, string methods and the wire encoders (rt.w_*) of the generated
    wrappers: it is safe to run with a recording _send."""
    try:
        tree = ast.parse(textwrap.dedent(inspect.getsource(fn)))
    except (OSError, TypeError):
        return False
    for node in ast.walk(tree):
        call = node.func if isinstance(node, ast.Call) else None
        if isinstance(node, _UNSAFE) or (call and not (isinstance(call, ast.Name) and call.id in _CALLS
                                                       or isinstance(call, ast.Attribute) and (call.attr in _METHODS or call.attr.startswith("w_")))):
            return False
        if isinstance(node, ast.Await) and not (isinstance(node.value, ast.Call) and getattr(node.value.func, "id", "") == "_send"):
            return False
    return not fn.__code__.co_freevars


def sample_calls(fn, seed: str) -> list[dict] | None:
    """The calls to run: the base call, then one parameter at a time at each sample (and at its default), then 40 seeded combinations.
    None when a parameter has no samples (its type is not str, int, float, bool, optional or a Literal)."""
    params, pools = inspect.signature(fn).parameters.values(), {}
    for p in params:
        values = _SAMPLES.get(p.annotation) or (list(typing.get_args(p.annotation)) if typing.get_origin(p.annotation) is typing.Literal else None)
        if values is None:
            return None
        pools[p.name] = values + ([p.default] if p.default is not p.empty and p.default not in values else [])
    base = {p.name: pools[p.name][0] if p.default is p.empty else p.default for p in params}
    rng = random.Random(seed)
    calls = [base] + [{**base, name: value} for name, values in pools.items() for value in values]
    calls += [{name: rng.choice(values) for name, values in pools.items()} for _ in range(40)]
    return [dict(items) for items in dict.fromkeys(tuple(call.items()) for call in calls)]


async def _sent(fn, kwargs: dict, args) -> list:
    """What the wrapper sends and returns for one call, with _send replaced by a recorder in the globals of the wrapper's module."""
    seen, saved = [], (fn.__globals__.get("_send"), fn.__globals__.get("_args"))

    async def send(*positional, **keywords):
        seen.append([positional, keywords])
        return "R"
    fn.__globals__["_send"], fn.__globals__["_args"] = send, args
    try:
        return [seen, await fn(**kwargs)]
    except Exception as e:  # an error is part of what the wrapper does
        return [seen, f"{type(e).__name__}: {e}"]
    finally:
        fn.__globals__["_send"], fn.__globals__["_args"] = saved


def wire_digest(fn, args, seed: str | None = None) -> str | None:
    """`calls=<n> digest=<hex>` of a wrapper that only forwards (what it sent and returned for its sample calls), None for any other."""
    calls = sample_calls(fn, seed or fn.__name__) if only_forwards(fn) else None

    async def run():
        return [await _sent(fn, call, args) for call in calls]
    return calls and f"calls={len(calls)} digest={_digest(asyncio.run(run()))}"


def wire_lines(names: frozenset[str] = frozenset()) -> list[str]:
    """`wire` lines of the first-party tools whose wrapper only forwards, sorted by name; `names` narrows them."""
    from unity_mcp.server import _args, mcp

    found = [(name, wire_digest(tool.fn, _args)) for name, tool in sorted(mcp._tool_manager._tools.items())
             if tool.fn.__module__.startswith("unity_mcp.") and (not names or name in names)]
    return [f"wire {name} {line}" for name, line in found if line]


def surface_lines(names: frozenset[str] = frozenset()) -> list[str]:
    """`tool` lines of the first-party MCP tools, `spec` lines of every ToolSpec and `wire` lines of the forwarding wrappers, by name; `names` narrows them."""
    from unity_mcp.server import mcp
    from unity_mcp.tools.tool_specs import _SPECS

    lines = []
    for name, tool in sorted(mcp._tool_manager._tools.items()):
        if not tool.fn.__module__.startswith("unity_mcp.") or (names and name not in names):
            continue
        annotations = tool.annotations.model_dump(exclude_none=True, by_alias=True) if tool.annotations else None
        fields = {"title": tool.title, "description": tool.description, "schema": tool.parameters, "annotations": annotations}
        lines.append(f"tool {name} " + " ".join(f"{key}={_digest(value)}" for key, value in fields.items()))
    for name, spec in sorted(_SPECS.items()):
        if not names or name in names:
            lines.append(f"spec {name} " + " ".join(f"{f.name}={getattr(spec, f.name)}" for f in dataclasses.fields(spec)))
    return lines + wire_lines(names)


def main(argv: list[str]) -> None:
    names = frozenset(argv)
    if not names:
        print(HEADER + env())
    print("\n".join(surface_lines(names)))


if __name__ == "__main__":
    main(sys.argv[1:])
