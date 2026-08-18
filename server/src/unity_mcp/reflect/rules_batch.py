"""Reflection rules for batch commands — parse sub-ops, cap warnings at 3."""
import re
from collections.abc import Awaitable, Callable  # noqa: TC003

from ..utils import parse_kv_line
from . import Mismatch, register_rule
from . import reflect as _reflect

_SUB_RESP_HEADER = re.compile(r"^\[(\d+)\]")


def _split_batch_response(response: str) -> list[str]:
    """Split batch response into per-sub-op blocks using [N] markers."""
    blocks: list[str] = []
    current: list[str] = []
    for line in response.splitlines():
        if _SUB_RESP_HEADER.match(line):
            if current:
                blocks.append("\n".join(current))
            current = [line]
        else:
            current.append(line)
    if current:
        blocks.append("\n".join(current))
    return blocks


def _parse_batch_commands(commands: str) -> list[tuple[str, dict]]:
    """Parse 'cmd key=val ...' lines into (cmd, args) pairs."""
    result = []
    for line in commands.splitlines():
        line = line.strip()
        if not line:
            continue
        cmd, args = parse_kv_line(line)
        if cmd:
            result.append((cmd, args))
    return result


@register_rule("batch")
async def _rule_batch(
    args: dict, response: str, send_fn: Callable[..., Awaitable[str]]
) -> Mismatch | None:
    commands_str = args.get("commands", "")
    if not commands_str:
        return None

    sub_cmds = _parse_batch_commands(commands_str)
    sub_resps = _split_batch_response(response)

    mismatches: list[str] = []
    for i, (sub_cmd, sub_args) in enumerate(sub_cmds):
        sub_resp = sub_resps[i] if i < len(sub_resps) else ""
        m = await _reflect(sub_cmd, sub_args, sub_resp, send_fn)
        if m is not None:
            mismatches.append(f"[{i+1}] {sub_cmd}: {m.msg}")

    if not mismatches:
        return None

    # Cap at 3 visible + "(N more)" suffix
    visible = mismatches[:3]
    extra = len(mismatches) - 3
    msg = "; ".join(visible)
    if extra > 0:
        msg += f" ({extra} more)"
    return Mismatch(msg)
