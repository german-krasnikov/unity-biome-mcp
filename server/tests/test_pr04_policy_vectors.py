"""PR-04: five policy-vector tests (read/write, batch route, retry-safety,
default metadata) for inspect/set_property/editor/run_playtest/uitk_file,
verified from the Python-authoritative side. No production code changes --
pure characterization of existing is_write/check_read_only/batch/retry_safe_cmds
behavior. Cross-referenced by comment with the C#-side vectors added to
unity-plugin/Editor/Tests/CommandRouterTests.cs (Pr04_* tests) for the same 5
commands, so a future drift between the two planes is easy to spot by eye.
"""
import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.middleware import Middleware
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
