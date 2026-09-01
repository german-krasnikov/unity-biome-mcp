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


# ── editor multi-select (paths param) ────────────────────────────────────────

async def test_editor_select_paths_sends_paths(ec_mod, _patch_send):
    """paths param is forwarded for multi-select."""
    await ec_mod.editor(action="select", paths="/Player,/Enemy")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "editor"
    assert call_args[0][1]["paths"] == "/Player,/Enemy"
    assert call_args[0][1]["action"] == "select"


async def test_editor_select_single_path_omits_paths(ec_mod, _patch_send):
    """paths is absent from args when not provided."""
    await ec_mod.editor(action="select", path="/Player")

    call_args = _patch_send.call_args
    assert call_args[0][1].get("path") == "/Player"
    assert "paths" not in call_args[0][1]


async def test_editor_paths_and_path_together(ec_mod, _patch_send):
    """Both path and paths can be sent simultaneously."""
    await ec_mod.editor(action="select", path="/Player", paths="/Player,/Enemy")

    call_args = _patch_send.call_args
    assert call_args[0][1]["path"] == "/Player"
    assert call_args[0][1]["paths"] == "/Player,/Enemy"


# ── mutation_mode (P0-70) ─────────────────────────────────────────────────────

async def test_mutation_mode_query_omits_enable(ec_mod, _patch_send):
    """No enable kwarg at all -> "enable" absent from wire args (query)."""
    await ec_mod.editor(action="mutation_mode")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "editor"
    assert "enable" not in call_args[0][1]


async def test_mutation_mode_enable_true_sends_lowercase_string(ec_mod, _patch_send):
    await ec_mod.editor(action="mutation_mode", enable=True)

    call_args = _patch_send.call_args
    assert call_args[0][1]["enable"] == "true"


async def test_mutation_mode_enable_false_sends_lowercase_string_not_omitted(ec_mod, _patch_send):
    """Critical tri-state case: explicit False must cross the wire as "false",
    never collapsed into omission the way Pattern-A optional flags are."""
    await ec_mod.editor(action="mutation_mode", enable=False)

    call_args = _patch_send.call_args
    assert call_args[0][1]["enable"] == "false"


async def test_mutation_mode_enable_true_updates_local_cache(ec_mod, _patch_send):
    from unity_mcp.tools._source_patch_intent import get_cached_intent, set_cached_intent
    set_cached_intent(False)

    await ec_mod.editor(action="mutation_mode", enable=True)

    assert get_cached_intent() is True
    set_cached_intent(False)  # restore


async def test_mutation_mode_enable_false_updates_local_cache(ec_mod, _patch_send):
    from unity_mcp.tools._source_patch_intent import get_cached_intent, set_cached_intent
    set_cached_intent(True)

    await ec_mod.editor(action="mutation_mode", enable=False)

    assert get_cached_intent() is False


async def test_mutation_mode_error_response_does_not_update_cache(ec_mod, _patch_send):
    from unity_mcp.tools._source_patch_intent import get_cached_intent, set_cached_intent
    set_cached_intent(False)
    _patch_send.return_value = "err: source patch provider absent"

    await ec_mod.editor(action="mutation_mode", enable=True)

    assert get_cached_intent() is False


async def test_non_mutation_mode_action_does_not_touch_cache(ec_mod, _patch_send):
    from unity_mcp.tools._source_patch_intent import get_cached_intent, set_cached_intent
    set_cached_intent(False)

    await ec_mod.editor(action="play")

    assert get_cached_intent() is False
