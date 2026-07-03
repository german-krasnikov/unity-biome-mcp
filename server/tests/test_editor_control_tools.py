"""Unit tests for editor_control.py tool functions (B2 split from scene.py):
editor, ping_object, get_selection, checkpoint, undo_last, get_capabilities."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.editor_control as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def ec_mod():
    import unity_mcp.tools.editor_control as mod
    return mod


# ── T5: undo_last ─────────────────────────────────────────────────────────────

async def test_undo_last_sends_correct_command(ec_mod, _patch_send):
    await ec_mod.undo_last()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "undo_last"
    assert call_args[0][1].get("turns") == 1


async def test_undo_last_passes_turns_param(ec_mod, _patch_send):
    await ec_mod.undo_last(turns=3)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("turns") == 3


# ── ping_object / get_selection ──────────────────────────────────────────────

async def test_ping_object_sends_command(ec_mod, _patch_send):
    await ec_mod.ping_object(path="/Test")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "ping_object"
    assert call_args[0][1] == {"path": "/Test"}


async def test_get_selection_sends_command(ec_mod, _patch_send):
    await ec_mod.get_selection()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_selection"
    assert call_args[0][1] == {}


# ── checkpoint ────────────────────────────────────────────────────────────────

async def test_checkpoint_sends_label(ec_mod, _patch_send):
    await ec_mod.checkpoint(label="save")

    _patch_send.assert_called_once_with("checkpoint", {"label": "save"})


async def test_checkpoint_default_label(ec_mod, _patch_send):
    await ec_mod.checkpoint()

    _patch_send.assert_called_once_with("checkpoint", {"label": "checkpoint"})


# ── editor ────────────────────────────────────────────────────────────────────

async def test_editor_state_uses_longer_timeout(ec_mod, _patch_send):
    await ec_mod.editor()

    _, kwargs = _patch_send.call_args
    assert kwargs.get("timeout") == 30.0


async def test_editor_play_uses_shorter_timeout(ec_mod, _patch_send):
    await ec_mod.editor(action="play")

    _, kwargs = _patch_send.call_args
    assert kwargs.get("timeout") == 15.0


# ── get_capabilities ──────────────────────────────────────────────────────────

async def test_get_capabilities_sends_command(ec_mod, _patch_send):
    await ec_mod.get_capabilities()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_capabilities"
    assert call_args[0][1] == {}
