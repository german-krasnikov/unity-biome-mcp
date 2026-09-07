"""PR-04: five policy-vector tests (read/write, batch route, retry-safety,
default metadata) for inspect/set_property/editor/run_playtest/uitk_file,
verified from the Python-authoritative side. No production code changes --
pure characterization of existing is_write/check_read_only/batch/retry_safe_cmds
behavior. Cross-referenced by comment with the C#-side vectors added to
unity-plugin/Editor/Tests/CommandRouterTests.cs (Pr04_* tests) for the same 5
commands, so a future drift between the two planes is easy to spot by eye.
"""
from dataclasses import replace
from unittest.mock import AsyncMock

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.middleware import Middleware
from unity_mcp.middleware_pipeline import wrap_send
from unity_mcp.middleware_types import is_write
from unity_mcp.tools.batch import _preprocess_continue_mode, _preprocess_stop_mode
from unity_mcp.tools.tool_specs import _SPECS

FIVE_COMMANDS = ("inspect", "set_property", "editor", "run_playtest", "uitk_file")


# ── Vector 1: is_write() classification (unknown/missing args, defaults) ────

@pytest.mark.parametrize("cmd,args,expected", [
    ("inspect", {}, False),
    ("inspect", {"paths": "/Foo"}, False),
    ("set_property", {}, True),
    ("set_property", {"path": "/Foo", "value": "1"}, True),
    ("editor", {}, True),                          # missing action -> conservative write
    ("editor", {"action": "state"}, False),        # read action
    ("editor", {"action": "project_path"}, False), # read action
    ("editor", {"action": "play"}, True),          # mutating action
    ("run_playtest", {}, True),                    # no action map -> always write
    ("run_playtest", {"script": "ASSERT_CONSOLE_CLEAN"}, True),
    ("uitk_file", {"action": "read"}, False),
    ("uitk_file", {"action": "write"}, True),
    ("uitk_file", {}, True),                       # missing action -> conservative write
])
def test_is_write_classification_vectors(cmd, args, expected):
    assert is_write(cmd, args) is expected


# ── Vector 2: read-only endpoint gate (allowed/denied per command+args) ─────

@pytest.mark.parametrize("cmd,args,blocked", [
    ("inspect", {}, False),
    ("set_property", {"path": "/Foo", "value": "1"}, True),
    ("editor", {"action": "state"}, False),
    ("editor", {"action": "play"}, True),
    ("run_playtest", {"script": "ASSERT_CONSOLE_CLEAN"}, True),
    ("uitk_file", {"action": "read"}, False),
    ("uitk_file", {"action": "write"}, True),
])
def test_read_only_gate_vectors(cmd, args, blocked):
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only(cmd, args)
    if blocked:
        assert result is not None
        assert "READ_ONLY_BLOCKED" in result
    else:
        assert result is None


def test_read_only_gate_off_never_blocks_any_of_the_five():
    mw = Middleware()
    mw.is_read_only = False
    for cmd in FIVE_COMMANDS:
        assert mw.check_read_only(cmd, {}) is None


# ── Vector 3: direct/batch/DSL route ────────────────────────────────────────

@pytest.mark.parametrize("cmd,line", [
    ("inspect", "inspect paths=/Foo"),
    ("set_property", "set_property path=/Foo component=Health prop=hp value=1"),
    ("editor", "editor action=state"),
])
def test_batch_route_non_direct_only_commands_pass_through_stop_mode(cmd, line):
    result = _preprocess_stop_mode(line)
    assert result == line


@pytest.mark.parametrize("cmd,line", [
    ("run_playtest", "run_playtest script=ASSERT_CONSOLE_CLEAN"),
    ("uitk_file", "uitk_file path=Assets/x.uxml action=read"),
])
def test_batch_route_direct_only_commands_rejected_stop_mode(cmd, line):
    assert _SPECS[cmd].direct_only is True
    with pytest.raises(ToolError, match="direct-only"):
        _preprocess_stop_mode(line)


def test_batch_route_direct_only_commands_rejected_continue_mode():
    commands = "inspect paths=/Foo\nrun_playtest script=ASSERT_CONSOLE_CLEAN\nuitk_file path=x action=read"
    clean, pre_errors, orig_indices = _preprocess_continue_mode(commands)
    assert "inspect" in clean
    assert "run_playtest" not in clean
    assert "uitk_file" not in clean
    assert len(pre_errors) == 2
    assert orig_indices == [0]  # only the surviving "inspect" line keeps its original index


# ── Vector 4: retry-safety after SENT ───────────────────────────────────────

async def test_retry_safe_cmds_vectors():
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds

    safe = await retry_safe_cmds(mcp)
    assert "inspect" in safe          # RO
    assert "set_property" in safe     # RW_IDEM
    assert "editor" not in safe       # RW, no idempotentHint
    assert "run_playtest" not in safe  # RW
    assert "uitk_file" not in safe     # RW


# ── Vector 5: default/mode metadata from _SPECS ─────────────────────────────

@pytest.mark.parametrize("cmd,expected", [
    ("inspect", {"mutability": "read", "direct_only": False, "runtime_only": False}),
    ("set_property", {"mutability": "write", "direct_only": False, "runtime_only": False}),
    ("editor", {"mutability": "write", "direct_only": False, "runtime_only": False}),
    ("run_playtest", {"mutability": "write", "direct_only": True, "runtime_only": False}),
    ("uitk_file", {"mutability": "write", "direct_only": True, "runtime_only": False}),
])
def test_default_metadata_vectors(cmd, expected):
    spec = _SPECS[cmd]
    assert spec.mutability == expected["mutability"]
    assert spec.direct_only is expected["direct_only"]
    assert spec.runtime_only is expected["runtime_only"]


# ── Discovered, characterized (not fixed) cross-route asymmetry ────────────

def test_editor_play_action_write_classification_asymmetric_with_csharp():
    """Python's is_write("editor", {"action": "play"}) is True (only "state"/
    "project_path" are reads per middleware_types._EDITOR_READ_ACTIONS), while
    C#'s CommandRegistry.IsMutating("editor", {"action":"play"}) is False
    ("play/stop/select don't corrupt scene data" -- CommandRegistry.cs). This
    pins Python's actual (stricter) behavior; it is not a fix and not an
    xfail. See CommandRouterTests.cs::
    Pr04_EditorPlayAction_IsMutating_False_AsymmetricWithPython for the pinned
    C#-side counterpart. Consistent with the plan's "Python консервативно
    защищает свой endpoint, C# авторитетно защищает Unity" -- not necessarily
    a bug, flagged for a future PR if the team wants the two routes unified.
    """
    assert is_write("editor", {"action": "play"}) is True


# ── N1b (P4/P5/P7): sync_unity policy vectors, second self-owned module ────
# Cross-referenced with C1-C9 in CommandRegistryGuardFlagsTests.cs and
# tools/sync_spec.py's SPEC_KWARGS / OWNED_WIRE_COMMANDS.

def test_sync_unity_batch_rejected_stop_mode():
    """sync_unity is direct_only through the real _SPECS table (owned by
    tools/sync_spec.py, not a hand-built dict here) — a batch DSL line
    naming it must be rejected before any dispatch, same route Vector 3
    proves for run_playtest/uitk_file above (N1b P4)."""
    assert _SPECS["sync_unity"].direct_only is True
    with pytest.raises(ToolError, match="direct-only"):
        _preprocess_stop_mode("sync_unity resolve=true")


def test_negative_control_sync_unity_batch_rejected(monkeypatch):
    """Proves the assertion above is load-bearing: strip direct_only from
    the live spec and confirm the batch line now passes through instead of
    raising."""
    monkeypatch.setitem(_SPECS, "sync_unity", replace(_SPECS["sync_unity"], direct_only=False))
    assert _preprocess_stop_mode("sync_unity resolve=true") == "sync_unity resolve=true"


async def test_sync_unity_read_only_blocked_before_dispatch():
    """sync_unity is a write (SYSTEM, tier1, direct_only) — in read-only
    mode wrap_send must raise READ_ONLY_BLOCKED before the injected send is
    ever awaited (N1b P5). That send is what ultimately reaches C#
    SyncHelper's AssetDatabase.Refresh + RequestScriptCompilation; a
    zero-call spy proves the mutation effect never fires, not just that an
    error string appears."""
    send = AsyncMock(return_value="ok")
    mw = Middleware()
    mw.is_read_only = True
    wrapped = wrap_send(send, mw)

    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await wrapped("sync_unity", {})

    send.assert_not_awaited()


async def test_negative_control_sync_unity_read_only_blocked():
    """Proves the assertion above is load-bearing: reclassify sync_unity's
    live spec as a read and confirm the read-only gate no longer blocks it
    (the send IS awaited)."""
    from unity_mcp import middleware_types
    middleware_types.WRITE_CMDS.discard("sync_unity")
    middleware_types.READ_CMDS.add("sync_unity")
    try:
        send = AsyncMock(return_value="ok")
        mw = Middleware()
        mw.is_read_only = True
        wrapped = wrap_send(send, mw)
        await wrapped("sync_unity", {})
        send.assert_awaited_once()
    finally:
        middleware_types.WRITE_CMDS.add("sync_unity")
        middleware_types.READ_CMDS.discard("sync_unity")


def test_sync_wire_commands_read_only_gate_vectors():
    """The wire-level mutating/read split the plan's audit found: 'sync' and
    'force_refresh' (consumed for recovery) are mutating wire commands and
    must be blocked in read-only mode; 'sync_status' is the pure-read
    escape hatch (_KNOWN_SAFE_READS) and must stay callable."""
    mw = Middleware()
    mw.is_read_only = True
    assert mw.check_read_only("sync_status", {}) is None
    for mutating_wire_cmd in ("sync", "force_refresh"):
        result = mw.check_read_only(mutating_wire_cmd, {})
        assert result is not None
        assert "READ_ONLY_BLOCKED" in result


async def test_sync_wire_commands_retry_safety_vectors():
    """sync_status is resend-safe after a SENT/uncertain delivery boundary
    (idempotent status read); the mutating 'sync' and 'force_refresh' wire
    commands must never be inferred retry-safe (N1b P5 companion) — each
    resend would re-trigger a real Refresh/compile in Unity, so an unsafe
    SENT operation must never be repeated automatically."""
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds
    safe = await retry_safe_cmds(mcp)
    assert "sync_status" in safe
    assert "sync" not in safe
    assert "force_refresh" not in safe


async def test_negative_control_sync_retry_safety(monkeypatch):
    """Proves the assertion above is load-bearing: add 'sync' to the
    internal retry-safe allowlist and confirm retry_safe_cmds() now
    (wrongly) treats it as resend-safe."""
    from unity_mcp.tools import _annotations
    monkeypatch.setattr(
        _annotations, "_INTERNAL_RETRY_SAFE_CMDS",
        _annotations._INTERNAL_RETRY_SAFE_CMDS | {"sync"},
    )
    from unity_mcp.server import mcp
    safe = await _annotations.retry_safe_cmds(mcp)
    assert "sync" in safe
