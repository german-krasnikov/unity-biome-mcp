"""Live stress tests for multi-scene support — N scenes, large object counts, edge cases."""
import asyncio
import logging
import uuid
from contextlib import asynccontextmanager

import pytest

from tests.live.conftest import (
    RUN_OWNED_ROOT,
    _close_owned_scene,
    _cs,
    _delete_owned_asset,
    _ok,
    _transient_id_expression,
    _transient_ref,
)

pytestmark = pytest.mark.live


@asynccontextmanager
async def _make_scenes(bridge, ownership, n: int):
    """Create n additive scenes. Yield list of names. Cleanup on exit."""
    names = []
    paths = []
    for _ in range(n):
        uid = uuid.uuid4().hex[:8]
        name = f"LiveSS_{uid}"
        path = f"{RUN_OWNED_ROOT}/{name}.unity"
        names.append(name)
        paths.append(path)
    ownership.scene_paths.update(paths)
    ownership.asset_paths.update(paths)
    created_paths = []
    try:
        for path in paths:
            code = (
                f'var path = {_cs(path)};'
                'var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine('
                ' UnityEngine.Application.dataPath, "..", path));'
                'if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(path)) ||'
                ' System.IO.File.Exists(fullPath)) return "target-exists";'
                'var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene('
                'UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,'
                'UnityEditor.SceneManagement.NewSceneMode.Additive);'
                'if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, path)) {'
                ' UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true);'
                ' return "save-failed";'
                '}'
                'return s.path;'
            )
            created_path = _ok(
                await bridge.send("execute_code", {"code": code})
            ).strip()
            assert created_path == path, (
                "additive scene was not saved at its registered path: "
                f"expected={path}, actual={created_path}"
            )
            created_paths.append(path)
        yield names
    finally:
        failures = []
        for path in created_paths:
            for operation in (_close_owned_scene, _delete_owned_asset):
                last_error = None
                for attempt in range(2):
                    try:
                        await operation(bridge, path)
                        last_error = None
                        break
                    except Exception as exc:
                        last_error = exc
                        logging.warning(
                            "cleanup failed for %s via %s (attempt %d): %s",
                            path,
                            operation.__name__,
                            attempt + 1,
                            exc,
                        )
                        if attempt == 0:
                            await asyncio.sleep(2)
                if last_error is not None:
                    failures.append(
                        f"{operation.__name__}({path}): {last_error}"
                    )
        if failures:
            raise AssertionError("multi-scene cleanup failed: " + "; ".join(failures))


async def _create_objects(bridge, scene_name: str, prefix: str, count: int) -> list[str]:
    """Create `count` objects in scene. Returns list of names."""
    names = [f"{prefix}_{i}_{uuid.uuid4().hex[:4]}" for i in range(count)]
    names_cs = ", ".join(f'"{n}"' for n in names)
    code = (
        f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{scene_name}");'
        f'var names = new string[]{{{names_cs}}};'
        'foreach(var n in names) {'
        '  var go = new UnityEngine.GameObject(n);'
        '  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
        '}'
        f'return string.Join("\\n", names);'
    )
    r = await bridge.send("execute_code", {"code": code})
    _ok(r)
    return names


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

async def test_3_scenes_all_hierarchy_headers(bridge, unity_state_owner):
    async with _make_scenes(bridge, unity_state_owner, 3) as scenes:
        for s in scenes:
            await _create_objects(bridge, s, "Hdr", 1)
        r = await bridge.send("get_hierarchy", {})
        data = _ok(r)
        for s in scenes:
            assert s in data, f"Scene header '{s}' missing from hierarchy"
        assert data.count("[") >= 3


async def test_5_scenes_search_across_all(bridge, unity_state_owner):
    async with _make_scenes(bridge, unity_state_owner, 5) as scenes:
        obj_names = []
        for s in scenes:
            names = await _create_objects(bridge, s, "SS5", 1)
            obj_names.append(names[0])
        for obj in obj_names:
            r = await bridge.send("search_scene", {"query": obj})
            data = _ok(r)
            assert obj in data, f"Object '{obj}' not found in search"


async def test_triple_ambiguity(bridge, unity_state_owner):
    """Same name in 3 scenes triggers ambiguity error."""
    async with _make_scenes(bridge, unity_state_owner, 3) as scenes:
        shared = f"AmbigObj_{uuid.uuid4().hex[:6]}"
        for s in scenes:
            code = (
                f'var go = new UnityEngine.GameObject("{shared}");'
                f'var sc = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{s}");'
                'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, sc);'
                'return "ok";'
            )
            await bridge.send("execute_code", {"code": code})
        r = await bridge.send("get_component", {"path": f"/{shared}", "type": "Transform"})
        err = r.get("err", "") or r.get("data", "")
        ok = r.get("ok", True)
        assert not ok or any(kw in err.lower() for kw in ("ambiguous", "exists in", "multiple")), (
            f"Expected ambiguity error, got: {err}"
        )


async def test_scene_qualified_across_3_scenes(bridge, unity_state_owner):
    """get_component with 3 different scene-qualified paths works."""
    async with _make_scenes(bridge, unity_state_owner, 3) as scenes:
        obj_names = []
        for s in scenes:
            names = await _create_objects(bridge, s, "SQ", 1)
            obj_names.append(names[0])
        for s, obj in zip(scenes, obj_names):
            path = f"{s}:/{obj}"
            r = await bridge.send("get_component", {"path": path, "type": "Transform"})
            data = _ok(r)
            assert "position" in data.lower(), f"No Transform data for {path}"


async def test_deep_nested_qualified_path(bridge, unity_state_owner):
    """Root/A/B/C in additive → get_component Scene:/Root/A/B/C."""
    async with _make_scenes(bridge, unity_state_owner, 1) as scenes:
        scene = scenes[0]
        code = (
            f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{scene}");'
            'var root = new UnityEngine.GameObject("NRoot");'
            'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, s);'
            'var a = new UnityEngine.GameObject("NA"); a.transform.SetParent(root.transform);'
            'var b = new UnityEngine.GameObject("NB"); b.transform.SetParent(a.transform);'
            'var c = new UnityEngine.GameObject("NC"); c.transform.SetParent(b.transform);'
            'return "ok";'
        )
        await bridge.send("execute_code", {"code": code})
        path = f"{scene}:/NRoot/NA/NB/NC"
        r = await bridge.send("get_component", {"path": path, "type": "Transform"})
        data = _ok(r)
        assert "position" in data.lower(), f"No Transform at deep path {path}"


async def test_object_with_spaces(bridge, unity_state_owner):
    """'My Live Object' → search('My Live') finds it."""
    async with _make_scenes(bridge, unity_state_owner, 1) as scenes:
        uid = uuid.uuid4().hex[:6]
        obj_name = f"My Live Obj {uid}"
        code = (
            f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{scenes[0]}");'
            f'var go = new UnityEngine.GameObject("{obj_name}");'
            'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
            'return "ok";'
        )
        await bridge.send("execute_code", {"code": code})
        r = await bridge.send("search_scene", {"query": f"My Live Obj {uid}"})
        data = _ok(r)
        assert obj_name in data, f"'{obj_name}' not found in search:\n{data}"


async def test_brackets_in_name_via_transient_id(bridge, unity_state_owner):
    """[SECTION/NAME] object is findable by transient object ID."""
    async with _make_scenes(bridge, unity_state_owner, 1) as scenes:
        obj_name = f"[SECTION_{uuid.uuid4().hex[:4]}]"
        transient_id = await _transient_id_expression(bridge, "go")
        code = (
            f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{scenes[0]}");'
            f'var go = new UnityEngine.GameObject("{obj_name}");'
            'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
            f'return "#" + {transient_id};'
        )
        r = await bridge.send("execute_code", {"code": code})
        object_ref = _transient_ref(_ok(r))
        r2 = await bridge.send(
            "get_component", {"path": object_ref, "type": "Transform"}
        )
        data = _ok(r2)
        assert "position" in data.lower(), (
            f"No Transform for bracket-named obj via {object_ref}"
        )


async def test_stress_30_objects_3_scenes(bridge, unity_state_owner):
    """10 objects × 3 scenes = 30 objects searchable."""
    prefix = f"Stress30_{uuid.uuid4().hex[:4]}"
    async with _make_scenes(bridge, unity_state_owner, 3) as scenes:
        for s in scenes:
            await _create_objects(bridge, s, prefix, 10)
        r = await bridge.send("search_scene", {"query": prefix, "limit": "50"})
        data = _ok(r)
        found = data.count(prefix)
        assert found >= 30, f"Expected 30 objects, found {found} in:\n{data[:500]}"


async def test_10_objects_search_limit(bridge, unity_state_owner):
    """10 objects in 1 additive, limit=3 → +7 more in result."""
    async with _make_scenes(bridge, unity_state_owner, 1) as scenes:
        prefix = f"Lim10_{uuid.uuid4().hex[:4]}"
        await _create_objects(bridge, scenes[0], prefix, 10)
        r = await bridge.send("search_scene", {"query": prefix, "limit": "3"})
        data = _ok(r)
        assert "+7" in data or "more" in data.lower(), (
            f"Expected '+7 more' indicator for limit=3 with 10 objects:\n{data}"
        )
