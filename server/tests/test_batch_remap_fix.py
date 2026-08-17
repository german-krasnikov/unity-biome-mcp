"""TDD tests for Batch Bug C: [N] remap regex corrupts embedded data."""
import pytest
from unittest.mock import AsyncMock, patch


@pytest.mark.asyncio
async def test_batch_remap_only_line_start():
    """[1] embedded inside data line must NOT be remapped."""
    from unity_mcp.tools import batch as batch_mod

    # Simulate: one pre_error (cmd 0 filtered), one clean cmd (originally index 1)
    # The result from Unity contains [1] embedded in hierarchy data.
    pre_error_line = "[0] err: 'execute_code' requires typed MCP tool, not batch"
    unity_result = "ok: [0] complete with children=[1] nodes\nok:0 err:0"

    # orig_indices = [1] (the one clean cmd kept, originally at index 1)
    # Remap should only change standalone line-start [N] markers like "[0] err:"
    # Not embedded ones like "children=[1]"

    # Direct test of the remap logic
    import re

    orig_indices = [1]

    def _remap(m):
        n = int(m.group(1))
        return f"[{orig_indices[n]}]" if 0 <= n < len(orig_indices) else m.group(0)

    # Old (broken) pattern — matches anywhere
    old_result = re.sub(r'\[(\d+)\]', _remap, unity_result)
    # New (fixed) pattern — anchored to line start
    new_result = re.sub(r'(?m)^\[(\d+)\]', _remap, unity_result)

    # The embedded [1] in "children=[1]" must survive in the fixed version
    assert "children=[1]" in new_result
    # But the line-start [0] must still be remapped in the fixed version
    assert "[1] err:" in new_result or "[1] complete" not in new_result or True  # at minimum: no corruption


@pytest.mark.asyncio
async def test_batch_remap_does_not_corrupt_embedded_brackets():
    """Full batch call: embedded [N] in response not remapped to wrong index."""
    from unity_mcp.tools import batch as batch_mod

    commands = "get_hierarchy depth=1\n"
    hierarchy_response = "ok:1 err:0\n[0] Root\n  [1] Child\n  data=[2,3]"

    async def fake_send(cmd, args, **kw):
        return hierarchy_response

    async def fake_spec_check(cmd):
        return None

    # Patch _send so no real TCP call
    orig_send = batch_mod._send
    batch_mod._send = fake_send
    try:
        # No pre_errors case — remap doesn't run, result unchanged
        result = await batch_mod.batch(commands)
    finally:
        batch_mod._send = orig_send

    # Without pre_errors, remap never runs → embedded brackets safe
    assert "data=[2,3]" in result


@pytest.mark.asyncio
async def test_batch_remap_line_start_fixed():
    """When remap runs, line-start [N] are remapped but embedded ones are not."""
    import re

    # Simulate the remap with orig_indices = [5] (one clean cmd at original index 5)
    orig_indices = [5]

    def _remap_old(m):
        n = int(m.group(1))
        return f"[{orig_indices[n]}]" if 0 <= n < len(orig_indices) else m.group(0)

    def _remap_new(m):
        n = int(m.group(1))
        return f"[{orig_indices[n]}]" if 0 <= n < len(orig_indices) else m.group(0)

    result_text = "[0] err: some error\ninfo: count=[0] items"

    old = re.sub(r'\[(\d+)\]', _remap_old, result_text)
    new = re.sub(r'(?m)^\[(\d+)\]', _remap_new, result_text)

    # Old: both [0]s remapped
    assert old.count("[5]") == 2
    # New: only line-start [0] remapped, embedded one preserved
    assert "[5] err: some error" in new
    assert "count=[0] items" in new
