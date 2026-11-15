"""Surface reachability: every non-direct_only, non-runtime_only tool must be
routable via batch without producing 'Unknown command'.

Auto-generated from _SPECS at import time to stay in sync as tools are added.

Test failure = C# CommandRouter has no handler for a tool that claims
batch-callable status. Tool-level errors (wrong args, missing object)
are acceptable — they prove the handler exists.
"""

import pytest

from tests.seams.invariants import parse_batch_result
from unity_mcp.tools.generated.catalog_examples import ARG_EXAMPLES
from unity_mcp.tools.tool_specs import _SPECS

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


# Minimal args for tools that need specific parameters to avoid C# parse errors.
# Preference: use nonexistent paths so write tools fail at the object-lookup level
# rather than with a parse/routing error. The test only checks absence of
# "Unknown command", not whether the command succeeds.
#
# Sourced from the catalog's ARG EXAMPLE values (dsl/tools.biome, SV6) instead of a
# hand-maintained literal: a tool's minimal-args string is its EXAMPLE-bearing ARGs
# joined as "key=val key2=val2"; a tool with none (safe defaults, or no args at all)
# gets the empty string via .get(name, "") below.
_MINIMAL_ARGS: dict[str, str] = {
    name: " ".join(f"{k}={v}" for k, v in args.items())
    for name, args in ARG_EXAMPLES.items()
    if args
}


def _build_surface_cases() -> list:
    """Auto-generate parametrized cases from _SPECS.

    Excludes:
    - direct_only tools (batch pre-filters them; routing not via C# CommandRouter)
    - runtime_only tools (require Play Mode; out of scope for static surface test)
    - _INTERNAL category (protocol-only, not MCP tools)
    """
    cases = []
    for name, spec in sorted(_SPECS.items()):
        if spec.direct_only or spec.runtime_only or spec.category == "_INTERNAL":
            continue
        args = _MINIMAL_ARGS.get(name, "")
        cases.append(pytest.param(name, args, id=name))
    return cases


SURFACE_CASES = _build_surface_cases()


@pytest.mark.parametrize("cmd,args_str", SURFACE_CASES)
async def test_tool_reachable_via_batch(seam_bridge, seam_worker, cmd, args_str):
    """D7 routing seam: tool must not return 'Unknown command' via batch.

    Failure = C# CommandRouter has no handler registered for this tool.
    Tool-level errors (wrong args, missing object) are acceptable — they
    prove the handler exists. Only 'Unknown command' indicates routing failure.
    """
    line = f"{cmd} {args_str}".strip()
    resp = await seam_bridge.send("batch", {"commands": line})
    text = resp.get("data", "") or resp.get("err", "")

    # Scope the routing-failure check to the [0] body line's own status and
    # detail rather than a blind substring search across the whole
    # response: a payload-returning tool like get_console can legitimately
    # embed the literal text "Unknown command" inside its OWN console-log
    # data (e.g. another probe's deliberate invalid-command test still in
    # the rolling console buffer), which a whole-text search misreports as
    # THIS tool's routing having failed. "Unknown command" is only real D7
    # evidence when it IS the [0] result itself.
    result = None
    if "ok:" in text or "err:" in text:
        try:
            result = parse_batch_result(text)
        except AssertionError:
            result = None

    if result is not None and result.items:
        _, status, detail = result.items[0]
        assert not (status == "err" and "Unknown command" in detail), (
            f"D7: '{cmd}' claims batch-capable (not direct_only) but C# CommandRouter "
            f"returned 'Unknown command': {detail[:300]}"
        )
    else:
        # No parseable [0] body line (e.g. a connection-level error, not a
        # batch response) — fall back to the whole-text check so a genuine
        # routing failure is never silently missed.
        assert "Unknown command" not in text, (
            f"D7: '{cmd}' claims batch-capable (not direct_only) but C# CommandRouter "
            f"returned 'Unknown command': {text[:300]}"
        )

    # Best-effort: verify summary exists so we know the response is valid batch format
    # (not a connection-level error). OK to skip if response is non-batch error.
    if result is not None:
        assert result.is_coherent(), (
            f"D7: '{cmd}' batch response summary/body mismatch: "
            f"summary ok:{result.ok_count} err:{result.err_count} "
            f"vs body ok:{result.body_ok} err:{result.body_err}"
        )
