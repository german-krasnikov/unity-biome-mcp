"""Live integration tests for multi-scene support."""
import uuid

import pytest
import pytest_asyncio

from tests.live.conftest import (
    RUN_OWNED_ROOT,
    _close_owned_scene,
    _cs,
    _delete_owned_asset,
    _destroy,
    _ok,
    _transient_id_expression,
    _transient_ref,
)

pytestmark = pytest.mark.live


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest_asyncio.fixture
async def additive_scene(bridge, unity_state_owner):
    """Open a new empty additive scene. Yield its name. Close on teardown."""
    uid = uuid.uuid4().hex[:8]
    scene_name = f"LiveMS_{uid}"
    scene_path = f"{RUN_OWNED_ROOT}/{scene_name}.unity"
    unity_state_owner.scene_paths.add(scene_path)
    unity_state_owner.asset_paths.add(scene_path)
    code = (
        f'var path = {_cs(scene_path)};'
        'var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine('
        ' UnityEngine.Application.dataPath, "..", path));'
        'if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(path)) ||'
        ' System.IO.File.Exists(fullPath)) return "target-exists";'
        'var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene('
        'UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, '
        'UnityEditor.SceneManagement.NewSceneMode.Additive);'
        'if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, path)) {'
        ' UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true);'
        ' return "save-failed";'
        '}'
        'return s.path;'
    )
    r = await bridge.send("execute_code", {"code": code})
    created_path = _ok(r).strip()
    assert created_path == scene_path, (
        f"additive scene was not saved at its registered path: {created_path}"
    )
    try:
        yield scene_name
    finally:
        errors = []
        try:
            await _close_owned_scene(bridge, scene_path)
        except Exception as exc:
            errors.append(str(exc))
        try:
            await _delete_owned_asset(bridge, scene_path)
        except Exception as exc:
            errors.append(str(exc))
        if errors:
            raise AssertionError(
                f"additive_scene cleanup failed for {scene_path}: "
                + "; ".join(errors)
            )


@pytest_asyncio.fixture
async def additive_obj(bridge, additive_scene):
    """Create a GameObject in the additive scene. Yield (name, transient ID)."""
    obj_name = f"Live_{uuid.uuid4().hex[:8]}"
    transient_id = await _transient_id_expression(bridge, "go")
    code = (
        f'var go = new UnityEngine.GameObject("{obj_name}");'
        f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{additive_scene}");'
        'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
        f'return "#" + {transient_id};'
    )
    r = await bridge.send("execute_code", {"code": code})
    object_ref = _transient_ref(_ok(r))
    yield obj_name, object_ref
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


async def test_transient_id_cross_scene(bridge, additive_obj):
    """get_component with a #transientObjectId works regardless of scene."""
    _, object_ref = additive_obj
    r = await bridge.send("get_component", {"path": object_ref, "type": "Transform"})
    data = _ok(r)
    assert "position" in data.lower(), f"No position in Transform data:\n{data}"


async def test_slash_in_name_via_transient_id(bridge, additive_scene):
    """Object with '/' in its name is findable by transient object ID."""
    slash_name = f"Live_slash/{uuid.uuid4().hex[:6]}"
    transient_id = await _transient_id_expression(bridge, "go")
    code = (
        f'var go = new UnityEngine.GameObject("{slash_name}");'
        f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{additive_scene}");'
        'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
        f'return "#" + {transient_id};'
    )
    r = await bridge.send("execute_code", {"code": code})
    object_ref = _transient_ref(_ok(r))
    r2 = await bridge.send(
        "get_component", {"path": object_ref, "type": "Transform"}
    )
    data = _ok(r2)
    assert "position" in data.lower(), f"No position for slash-named object:\n{data}"
