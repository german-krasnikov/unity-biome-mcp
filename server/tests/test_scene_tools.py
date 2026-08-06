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


# ── fingerprint ───────────────────────────────────────────────────────────────

async def test_fingerprint_no_path_omits_path_key(scene_mod, _patch_send):
    """P-106: fingerprint() with no path must NOT include 'path' in TCP args."""
    await scene_mod.fingerprint()

    args = _patch_send.call_args[0][1]
    assert "path" not in args, f"path must not appear in args when None; got {args}"
    assert args.get("depth") == 3


async def test_fingerprint_with_path_includes_path_key(scene_mod, _patch_send):
    """P-106: fingerprint(path='Player') must include path in TCP args."""
    await scene_mod.fingerprint(path="Player")

    args = _patch_send.call_args[0][1]
    assert args.get("path") == "Player"
