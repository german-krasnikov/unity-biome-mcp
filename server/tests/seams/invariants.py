"""Reusable assertion functions for cross-tool seam tests.

Each function tests one seam class:
  assert_round_trip       — mutate → readback → semantic verify
  assert_batch_report_accurate — batch summary must match [N] body markers
  assert_surface_consistency   — tool reachable both direct and via batch
  assert_composition           — N-step composition preserves coherent state
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

from unity_mcp.tools.batch import _SUMMARY_RE

# Body line format: [N] ok: <detail>  or  [N] err: <detail>
_BODY_LINE_RE = re.compile(r"^\[(\d+)\] (ok|err):")


@dataclass
class BatchResult:
    ok_count: int     # from summary line "ok:X"
    err_count: int    # from summary line "err:Y" (0 if absent)
    skip_count: int   # from summary line "skip:Z" (0 if absent)
    body_ok: int      # [N] lines containing " ok:" in body
    body_err: int     # [N] lines containing " err:" in body
    items: list = field(default_factory=list)  # [(index, status, detail)]

    @property
    def n(self) -> int:
        """Total commands accounted for in summary."""
        return self.ok_count + self.err_count + self.skip_count

    def is_coherent(self) -> bool:
        """Summary ok/err counts match the body [N] line counts."""
        return self.ok_count == self.body_ok and self.err_count == self.body_err


def parse_batch_result(text: str) -> BatchResult:
    """Parse batch response text → BatchResult.

    Summary line (terminal, per _SUMMARY_RE): ok:X [err:Y] [skip:Z]
    Body lines: [N] ok: <detail>  or  [N] err: <detail>
    """
    m = _SUMMARY_RE.search(text)
    if not m:
        raise AssertionError(f"No summary line (ok:X) found in batch response:\n{text[:500]}")

    ok_count = int(m.group("ok") or 0)
    err_count = int(m.group("err") or 0)
    skip_count = int(m.group("skip") or 0)

    body_ok = 0
    body_err = 0
    items: list[tuple[int, str, str]] = []
    for line in text.splitlines():
        bm = _BODY_LINE_RE.match(line)
        if bm:
            idx = int(bm.group(1))
            status = bm.group(2)
            detail = line[bm.end():].strip()
            items.append((idx, status, detail))
            if status == "ok":
                body_ok += 1
            else:
                body_err += 1

    return BatchResult(
        ok_count=ok_count,
        err_count=err_count,
        skip_count=skip_count,
        body_ok=body_ok,
        body_err=body_err,
        items=items,
    )


def parse_status_text(text: str) -> dict[str, str]:
    """Parse 'key=value\\n...' text → dict. Normalises bool values to lowercase."""
    values: dict[str, str] = {}
    for line in text.splitlines():
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        normalized = value.strip()
        if normalized.lower() in {"true", "false"}:
            normalized = normalized.lower()
        values[key.strip()] = normalized
    return values


async def assert_round_trip(
    bridge,
    mutate_cmd: str,
    mutate_args: dict,
    read_cmd: str,
    read_args: dict,
    check_fn,  # Callable[[dict], None] — raises AssertionError on failure
) -> None:
    """CORE pattern: execute mutation, readback, verify semantically.

    Use for: create_object, set_property, manage_component, set_active,
    delete_object, set_parent, rename_object, set_property_delta.

    1. Sends mutate_cmd. Asserts ok=True.
    2. Sends read_cmd. Asserts ok=True.
    3. Calls check_fn(read_response). check_fn raises AssertionError on mismatch.
    """
    mutate_resp = await bridge.send(mutate_cmd, mutate_args)
    assert mutate_resp.get("ok"), (
        f"{mutate_cmd} failed: {mutate_resp.get('err', mutate_resp)}"
    )

    read_resp = await bridge.send(read_cmd, read_args)
    assert read_resp.get("ok"), (
        f"{read_cmd} failed: {read_resp.get('err', read_resp)}"
    )

    result = check_fn(read_resp)
    # Support both bool-returning and void-raising check functions
    if result is not None and not result:
        raise AssertionError(
            f"Readback check failed for {read_cmd}: {read_resp.get('data', '')[:200]}"
        )


async def assert_batch_report_accurate(
    bridge,
    commands_str: str,
) -> BatchResult:
    """REPORT seam: batch summary must match per-command [N] markers.

    Sends batch(commands=commands_str) with default on_error=continue.
    Parses summary line and body [N] markers. Asserts:
        result.is_coherent()   — summary ok/err match body counts
        result.n == line_count — no commands dropped from summary

    Returns BatchResult for caller to make additional assertions.
    Intended for on_error=continue (default) batches; skip count
    invariant does not hold for on_error=stop runs.
    """
    n_commands = sum(
        1 for line in commands_str.splitlines()
        if line.strip() and not line.strip().startswith("#")
    )

    resp = await bridge.send("batch", {"commands": commands_str})
    text = resp.get("data", "") or resp.get("err", "")

    assert "ok:" in text, f"No summary line in batch response:\n{text[:500]}"

    result = parse_batch_result(text)

    assert result.is_coherent(), (
        f"Batch summary/body mismatch: summary ok:{result.ok_count} err:{result.err_count} "
        f"but body counted ok:{result.body_ok} err:{result.body_err}\n"
        f"Full response:\n{text[:600]}"
    )
    assert result.n == n_commands, (
        f"Batch dropped commands: sent {n_commands} but "
        f"ok({result.ok_count})+err({result.err_count})+skip({result.skip_count})={result.n}\n"
        f"Full response:\n{text[:600]}"
    )

    return result


async def assert_surface_consistency(
    bridge,
    cmd: str,
    args: dict | None = None,
) -> tuple[dict, BatchResult]:
    """ROUTE seam: cmd must be routable both directly and via batch.

    Neither call may produce 'Unknown command' in the response.
    Tool-level errors (wrong args, missing object) are acceptable — they prove
    the C# handler exists. 'Unknown command' means no handler registered.

    Returns (direct_resp, batch_result) for caller inspection.
    """
    if args is None:
        args = {}

    direct_resp = await bridge.send(cmd, args)
    direct_text = direct_resp.get("data", "") or direct_resp.get("err", "")
    assert "Unknown command" not in direct_text, (
        f"D7: '{cmd}' not in C# CommandRouter (direct call): {direct_text[:200]}"
    )

    # Serialize args to key=value string for batch
    args_str = " ".join(f"{k}={v}" for k, v in args.items())
    batch_line = f"{cmd} {args_str}".strip()
    batch_resp = await bridge.send("batch", {"commands": batch_line})
    batch_text = batch_resp.get("data", "") or batch_resp.get("err", "")

    assert "Unknown command" not in batch_text, (
        f"D7: '{cmd}' not in C# CommandRouter (batch call): {batch_text[:200]}"
    )

    batch_result = parse_batch_result(batch_text)
    return direct_resp, batch_result


async def assert_composition(
    bridge,
    steps: list[tuple[str, dict]],
    final_check_fn,  # Callable[[list[dict]], None]
    cleanup_fn=None,  # Callable[[], Awaitable[None]] | None
) -> None:
    """STRUCT seam: chain N tool calls, verify coherent final state.

    Executes steps in order. Each must return ok=True.
    Calls final_check_fn(responses) — raises AssertionError on failure.
    cleanup_fn is awaited in a finally block even on failure.
    """
    responses: list[dict] = []
    try:
        for cmd, args in steps:
            resp = await bridge.send(cmd, args)
            assert resp.get("ok"), (
                f"Composition step '{cmd}' failed: {resp.get('err', resp)}"
            )
            responses.append(resp)
        final_check_fn(responses)
    finally:
        if cleanup_fn is not None:
            await cleanup_fn()
