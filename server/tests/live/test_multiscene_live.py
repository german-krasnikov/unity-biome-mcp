"""Live integration tests for multi-scene support."""
import logging
import uuid

import pytest
import pytest_asyncio

from tests.live.conftest import _destroy, _ok, _iid

_TEMP_FOLDER = "Assets/TestsTemp"

pytestmark = pytest.mark.live


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest_asyncio.fixture
async def additive_scene(bridge):
    """Open a new empty additive scene. Yield its name. Close on teardown."""
    uid = uuid.uuid4().hex[:8]
    scene_name = f"LiveMS_{uid}"
    scene_path = f"{_TEMP_FOLDER}/{scene_name}.unity"
    code = (
        f'UnityEditor.AssetDatabase.CreateFolder("Assets", "TestsTemp");'
        'var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene('
        'UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, '
        'UnityEditor.SceneManagement.NewSceneMode.Additive);'
        f'UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, "{scene_path}");'
        'return s.name;'
    )
    r = await bridge.send("execute_code", {"code": code})
    name = _ok(r).strip()
    yield name
    try:
        close_code = (
            f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{name}");'
            'if(s.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true); }'
            f'UnityEditor.AssetDatabase.DeleteAsset("{scene_path}");'
            'return "closed";'
        )
        await bridge.send("execute_code", {"code": close_code})
    except Exception as e:
        logging.warning("additive_scene cleanup failed for %r: %s", name, e)


@pytest_asyncio.fixture
async def additive_obj(bridge, additive_scene):
    """Create a GameObject in the additive scene. Yield (name, iid_string)."""
    obj_name = f"Live_{uuid.uuid4().hex[:8]}"
    code = (
        f'var go = new UnityEngine.GameObject("{obj_name}");'
        f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{additive_scene}");'
        'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
        'return "#" + go.GetInstanceID();'
    )
    r = await bridge.send("execute_code", {"code": code})
    iid = _iid(_ok(r))
    yield obj_name, iid
    # scene teardown handles cleanup via CloseScene(removeScene=true)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

async def test_hierarchy_shows_scene_headers(bridge, additive_obj, additive_scene):
    """get_hierarchy should show scene name headers with bracket notation."""
    r = await bridge.send("get_hierarchy", {})
    data = _ok(r)
    assert "[" in data, f"No scene headers found:\n{data}"
    assert additive_scene in data, f"Additive scene '{additive_scene}' not in hierarchy:\n{data}"


async def test_search_finds_in_additive(bridge, additive_obj):
    """search_scene should find the object created in the additive scene."""
    obj_name, _ = additive_obj
    r = await bridge.send("search_scene", {"query": obj_name})
    data = _ok(r)
    assert obj_name in data, f"'{obj_name}' not found in search results:\n{data}"


async def test_search_has_scene_prefix(bridge, additive_obj, additive_scene):
    """search_scene results should include 'SceneName:/' path prefix."""
    obj_name, _ = additive_obj
    r = await bridge.send("search_scene", {"query": obj_name})
    data = _ok(r)
    assert ":/" in data, f"No scene-qualified path (SceneName:/) in:\n{data}"


async def test_get_component_scene_qualified(bridge, additive_obj, additive_scene):
    """get_component with 'SceneName:/ObjName' path should return Transform data."""
    obj_name, _ = additive_obj
    path = f"{additive_scene}:/{obj_name}"
    r = await bridge.send("get_component", {"path": path, "type": "Transform"})
    data = _ok(r)
    assert "position" in data.lower(), f"No position in Transform data:\n{data}"


async def test_ambiguity_error(bridge, additive_obj, additive_scene):
    """Same object name in two scenes should trigger an ambiguity error."""
    obj_name, _ = additive_obj
    # Create same name in active (GridTest) scene
    await bridge.send("create_object", {"name": obj_name})
    try:
        r = await bridge.send("get_component", {"path": f"/{obj_name}", "type": "Transform"})
        err = r.get("err", "") or r.get("data", "")
        assert not r.get("ok", True) or any(
            kw in err.lower() for kw in ("ambiguous", "exists in", "multiple")
        ), f"Expected ambiguity error, got ok=True with: {err}"
    finally:
        await _destroy(bridge, obj_name)


async def test_instance_id_cross_scene(bridge, additive_obj):
    """get_component with #instanceId should work regardless of scene."""
    _, iid = additive_obj
    r = await bridge.send("get_component", {"path": iid, "type": "Transform"})
    data = _ok(r)
    assert "position" in data.lower(), f"No position in Transform data:\n{data}"


async def test_slash_in_name_via_iid(bridge, additive_scene):
    """Object with '/' in name should be findable by instance ID."""
    slash_name = f"Live_slash/{uuid.uuid4().hex[:6]}"
    code = (
        f'var go = new UnityEngine.GameObject("{slash_name}");'
        f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{additive_scene}");'
        'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
        'return "#" + go.GetInstanceID();'
    )
    r = await bridge.send("execute_code", {"code": code})
    iid = _iid(_ok(r))
    r2 = await bridge.send("get_component", {"path": iid, "type": "Transform"})
    data = _ok(r2)
    assert "position" in data.lower(), f"No position for slash-named object:\n{data}"
