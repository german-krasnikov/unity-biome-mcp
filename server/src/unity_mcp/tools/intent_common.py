"""Shared DSL utilities for Tier B intent tools."""
from typing import Callable, Optional
from mcp.server.fastmcp.exceptions import ToolError
from ..sampling_postproc import strip_fences  # backward-compat re-export
from ..utils import parse_kv  # consolidated — was local copy

__all__ = ["strip_fences", "parse_kv", "parse_indent_tree", "sanitize_intent", "build_batch_line", "run_intent_pipeline"]


def parse_indent_tree(dsl: str) -> list[dict]:
    """Parse 2-space-indent DSL into flat list with depth + parent refs."""
    nodes: list[dict] = []
    for raw_line in dsl.splitlines():
        if not raw_line.strip():
            continue
        stripped = raw_line.rstrip()
        indent = len(stripped) - len(stripped.lstrip(" "))
        depth = indent // 2
        # Find parent: last node with depth = current - 1
        parent = None
        for n in reversed(nodes):
            if n["depth"] == depth - 1:
                parent = n
                break
        nodes.append({"line": stripped.strip(), "depth": depth, "parent": parent})
    return nodes


def sanitize_intent(text: str, max_len: int = 500) -> str:
    """Cap length, strip newlines and braces to mitigate prompt injection."""
    return text[:max_len].replace("\n", " ").replace("{", "").replace("}", "")


def build_batch_line(cmd: str, **kwargs) -> str:
    """Build a batch command line, skipping None values. Quote values with spaces."""
    parts = [cmd]
    for k, v in kwargs.items():
        if v is not None:
            sv = str(v)
            parts.append(f'{k}="{sv}"' if " " in sv else f"{k}={sv}")
    return " ".join(parts)


async def run_intent_pipeline(
    send: Callable,
    sampling,
    prompt: str,
    feature: str,
    parse_fn: Callable,
    build_fn: Callable,
    dry_run: bool,
    validate_fn: Optional[Callable[[dict], Optional[str]]] = None,
) -> str:
    """Common tail shared by all intent tools: generate → strip → parse → [validate] → build → execute."""
    dsl_raw = await sampling.generate(prompt, feature=feature)
    if not dsl_raw:
        raise ToolError("Haiku unavailable (set UNITY_MCP_VISUAL_VERIFY=1)")
    parsed = parse_fn(strip_fences(dsl_raw))
    if validate_fn is not None:
        err = validate_fn(parsed)
        if err:
            raise ToolError(f"INVALID DSL: {err}")
    lines = build_fn(parsed)
    if not lines:
        raise ToolError("DSL produced no commands")
    batch_text = "\n".join(lines)
    if dry_run:
        return batch_text
    result = await send("batch", {"commands": batch_text})
    return f"{feature}: {len(lines)} ops\n{result}"
