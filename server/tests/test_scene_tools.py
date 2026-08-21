"""Unit tests for scene.py tool functions (B2: slimmed after console/screenshot/
testing/editor_control split — only scene_environment remains here; get_hierarchy/
scene/search_scene/fingerprint/scene_diff coverage lives in test_field_projection.py,
test_search.py, test_search_scoped.py, test_fingerprint_scan.py, test_server_delta.py)."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.scene as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def scene_mod():
    import unity_mcp.tools.scene as mod
    return mod


# ── scene ────────────────────────────────────────────────────────────────────

async def test_scene_name_passed_as_scene_in_args(scene_mod, _patch_send):
    await scene_mod.scene(action="save", scene="Level1")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene"
    assert call_args[0][1]["scene"] == "Level1"


# ── scene_environment ────────────────────────────────────────────────────────

async def test_scene_environment_get_sends_command(scene_mod, _patch_send):
    await scene_mod.scene_environment()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene_environment"
    assert call_args[0][1] == {"action": "get"}


async def test_scene_environment_set_sends_params(scene_mod, _patch_send):
    await scene_mod.scene_environment(action="set", prop="fog", value="true")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene_environment"
    assert call_args[0][1] == {"action": "set", "prop": "fog", "value": "true"}


# ── scene save_copy ───────────────────────────────────────────────────────────

async def test_scene_save_copy_sends_correct_action(scene_mod, _patch_send):
    await scene_mod.scene(action="save_copy", path="Assets/Backups/Scene.unity")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene"
    assert call_args[0][1]["action"] == "save_copy"
    assert call_args[0][1]["path"] == "Assets/Backups/Scene.unity"


async def test_scene_save_copy_with_scene_identifier(scene_mod, _patch_send):
    await scene_mod.scene(action="save_copy", path="Assets/Backups/Scene.unity", scene="Level1")

    call_args = _patch_send.call_args
    assert call_args[0][1]["scene"] == "Level1"


async def test_scene_save_copy_include_unsaved_not_in_args(scene_mod, _patch_send):
    """include_unsaved exists for discoverability only; must NOT be forwarded to TCP."""
    await scene_mod.scene(action="save_copy", path="Assets/Backups/Scene.unity", include_unsaved=False)

    args = _patch_send.call_args[0][1]
    assert "include_unsaved" not in args


# ── _OBJECT_REF regex coverage ────────────────────────────────────────────────

import re
from unity_mcp.tools.scene import _OBJECT_REF


def test_object_ref_matches_ampersand():
    assert re.fullmatch(_OBJECT_REF, "&abc123")


def test_object_ref_matches_hash():
    assert re.fullmatch(_OBJECT_REF, "#12345")


def test_object_ref_matches_dollar_hex():
    assert re.fullmatch(_OBJECT_REF, "$3E8")


def test_object_ref_no_match_plain_path():
    assert not re.fullmatch(_OBJECT_REF, "/some/path")
