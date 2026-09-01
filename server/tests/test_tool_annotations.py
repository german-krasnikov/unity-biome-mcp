"""Tests for idempotentHint on write tools that are safe to repeat."""
import pytest
from mcp.types import ToolAnnotations
from unity_mcp.tools import objects, scene, asset, ui, connection, runtime
from unity_mcp.tools import console, testing, editor_control, screenshot, spatial
from unity_mcp.tools import metrics_tool, profiling


def _get_annotation(module, fn_name: str) -> ToolAnnotations | None:
    """Extract the _RW_IDEM / _RW / _RO constant used in register() for a given function."""
    import ast, inspect, textwrap
    src = inspect.getsource(module)
    tree = ast.parse(textwrap.dedent(src))
    # Find the register function body
    for node in ast.walk(tree):
        if isinstance(node, ast.FunctionDef) and node.name == "register":
            for stmt in node.body:
                # mcp.tool(annotations=CONST)(fn_name)
                if not isinstance(stmt, ast.Expr):
                    continue
                call = stmt.value
                if not isinstance(call, ast.Call):
                    continue
                # inner call: mcp.tool(annotations=CONST) → the CONST name
                if not isinstance(call.func, ast.Call):
                    continue
                inner = call.func  # mcp.tool(annotations=...)
                # check positional arg to inner call is fn_name
                if not call.args or not isinstance(call.args[0], ast.Name):
                    continue
                if call.args[0].id != fn_name:
                    continue
                # find annotations= kwarg in inner call
                for kw in inner.keywords:
                    if kw.arg == "annotations" and isinstance(kw.value, ast.Name):
                        const_name = kw.value.id
                        return getattr(module, const_name)
    return None


IDEM_TOOLS = [
    (objects, "set_property"),
    (objects, "set_active"),
    (objects, "set_material"),
    (asset, "project_settings"),
    (ui, "set_rect"),
    (connection, "reconnect_unity"),
    (runtime, "wait_until"),
    (testing, "run_tests"),
    (testing, "cancel_test_run"),
]

NON_IDEM_TOOLS = [
    (objects, "create_object"),
    (objects, "delete_object"),
    (objects, "manage_component"),
    (objects, "wire_event"),
    (scene, "scene"),
    (testing, "run_tests_wait"),
    # Every call triggers a fresh compile/reload; dedup does not survive it.
    (console, "recompile"),
    # editor mutates editor state (play/pause/stop) — not idempotent
    (editor_control, "editor"),
]


@pytest.mark.parametrize("mod,fn", IDEM_TOOLS)
def test_idempotent_tool_has_idempotentHint(mod, fn):
    ann = _get_annotation(mod, fn)
    assert ann is not None, f"{mod.__name__}.{fn}: no annotation found"
    assert ann.idempotentHint is True, f"{mod.__name__}.{fn}: idempotentHint should be True"
    assert ann.readOnlyHint is False, f"{mod.__name__}.{fn}: readOnlyHint should be False"


@pytest.mark.parametrize("mod,fn", NON_IDEM_TOOLS)
def test_non_idempotent_tool_lacks_idempotentHint(mod, fn):
    ann = _get_annotation(mod, fn)
    assert ann is not None, f"{mod.__name__}.{fn}: no annotation found"
    assert ann.idempotentHint is not True, f"{mod.__name__}.{fn}: should NOT have idempotentHint=True"


def test_run_tests_not_marked_read_only():
    """run_tests triggers domain reload — must NOT have readOnlyHint=True."""
    ann = _get_annotation(testing, "run_tests")
    assert ann is not None, "run_tests: no annotation found"
    assert ann.readOnlyHint is not True, "run_tests causes domain reload — readOnlyHint must be False"


@pytest.mark.parametrize(
    "module,name",
    [
        (spatial, "navmesh_query"),
        (screenshot, "screenshot"),
        (scene, "get_changes"),
        (scene, "screenshot_compare"),
        (metrics_tool, "get_metrics"),
        (profiling, "profile"),
    ],
)
def test_conditionally_or_file_mutating_tools_are_not_read_only(module, name):
    ann = _get_annotation(module, name)
    assert ann is not None
    assert ann.readOnlyHint is not True


async def test_retry_safe_cmds_includes_only_readonly_or_idempotent():
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds
    safe = await retry_safe_cmds(mcp)
    assert "get_console" in safe          # RO
    assert "execute_code" not in safe     # RW, no idempotentHint


async def test_retry_safe_cmds_includes_internal_status_commands():
    """get_status/sync_status are internal wire commands with no MCP tool
    annotation (mcp.list_tools() never sees them) but are read-only/idempotent
    by inspection of their C# handler -- must be retry-safe (P-322 fail-closed
    gate, ff44cc8c) or every post-SENT disconnect during get_status/sync_status
    raises UncertainDeliveryError."""
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds
    safe = await retry_safe_cmds(mcp)
    assert "get_status" in safe
    assert "sync_status" in safe


async def test_retry_safe_cmds_excludes_mutating_internal_cmds():
    """Double-red guard: the internal allowlist is not a blanket escape hatch.
    source_patch_write is a mutating internal wire command (see
    test_sent_unsafe_source_patch_is_never_resent). sync (TriggerSync) is
    sync_status's sibling registration but bumps epoch and forces a recompile
    -- proves the fix does not sweep in the whole sync/* family."""
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds
    safe = await retry_safe_cmds(mcp)
    assert "source_patch_write" not in safe
    assert "sync" not in safe


async def test_recompile_annotation_prevents_public_send_after_domain_reload():
    """Executable annotation mutant: RW_IDEM would produce multiple frames."""
    import json
    from unittest.mock import AsyncMock, MagicMock, patch

    import unity_mcp.bridge as bridge_module
    from unity_mcp.bridge import BridgeState, CommandStatus, UnityBridge
    from unity_mcp.bridge_socket import DomainReloadError
    from unity_mcp.errors import UncertainDeliveryError
    from unity_mcp.server import mcp
    from unity_mcp.tools._annotations import retry_safe_cmds

    safe = await retry_safe_cmds(mcp)
    assert "recompile" not in safe

    class ImmediateGate:
        def clear(self):
            pass

        def set(self):
            pass

        async def wait(self):
            return None

    probe = MagicMock()
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    bridge = UnityBridge(probe=probe, is_retry_safe=safe.__contains__)
    bridge._reload_gate = ImmediateGate()
    bridge._crash_log = MagicMock()
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()
    writer.get_extra_info.return_value = None
    writer.wait_closed = AsyncMock()
    bridge._writer = writer
    bridge._reader = MagicMock()
    bridge._state = BridgeState.CONNECTED
    frames: list[dict] = []

    def capture_frame(_writer, raw: bytes) -> None:
        frames.append(json.loads(raw.decode("utf-8")))

    with (
        patch.object(bridge_module, "frame_write", side_effect=capture_frame),
        patch.object(
            bridge, "_read_response", new_callable=AsyncMock,
            side_effect=DomainReloadError("reload after recompile"),
        ),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        with pytest.raises(UncertainDeliveryError) as caught:
            await bridge.send("recompile", {})

    assert len(frames) == 1
    assert caught.value.cmd == "recompile"
    assert caught.value.op_id == frames[0]["op_id"]
    assert caught.value.delivery is CommandStatus.ACCEPTED
    assert bridge.get_command_status(caught.value.op_id)[0] is CommandStatus.ACCEPTED
    await bridge.close()
