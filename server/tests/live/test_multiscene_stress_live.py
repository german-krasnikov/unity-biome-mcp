"""Live stress tests for multi-scene support — N scenes, large object counts, edge cases."""
import asyncio
import logging
import uuid
from contextlib import asynccontextmanager

import pytest

from tests.live.conftest import _destroy, _ok, _iid

_TEMP = "Assets/TestsTemp"
pytestmark = pytest.mark.live


@asynccontextmanager
async def _make_scenes(bridge, n: int):
    """Create n additive scenes. Yield list of names. Cleanup on exit."""
    names = []
    paths = []
    for _ in range(n):
        uid = uuid.uuid4().hex[:8]
        name = f"LiveSS_{uid}"
        path = f"{_TEMP}/{name}.unity"
        code = (
            f'UnityEditor.AssetDatabase.CreateFolder("Assets", "TestsTemp");'
            'var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene('
            'UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,'
            'UnityEditor.SceneManagement.NewSceneMode.Additive);'
            f'UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, "{path}");'
            'return s.name;'
        )
        r = await bridge.send("execute_code", {"code": code})
        names.append(_ok(r).strip())
        paths.append(path)
    try:
        yield names
    finally:
        for name, path in zip(names, paths):
            code = (
                f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{name}");'
                'if(s.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s, true);'
                f'UnityEditor.AssetDatabase.DeleteAsset("{path}");'
                'return "ok";'
            )
            for attempt in range(2):
                try:
                    await bridge.send("execute_code", {"code": code})
                    break
                except Exception as e:
                    logging.warning(f"cleanup failed for scene {name} (attempt {attempt + 1}): {e}")
                    if attempt == 0:
                        await asyncio.sleep(2)


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

async def test_3_scenes_all_hierarchy_headers(bridge):
    async with _make_scenes(bridge, 3) as scenes:
        for s in scenes:
            await _create_objects(bridge, s, "Hdr", 1)
        r = await bridge.send("get_hierarchy", {})
        data = _ok(r)
        for s in scenes:
            assert s in data, f"Scene header '{s}' missing from hierarchy"
        assert data.count("[") >= 3


async def test_5_scenes_search_across_all(bridge):
    async with _make_scenes(bridge, 5) as scenes:
        obj_names = []
        for s in scenes:
            names = await _create_objects(bridge, s, "SS5", 1)
            obj_names.append(names[0])
        for obj in obj_names:
            r = await bridge.send("search_scene", {"query": obj})
            data = _ok(r)
            assert obj in data, f"Object '{obj}' not found in search"


async def test_triple_ambiguity(bridge):
    """Same name in 3 scenes triggers ambiguity error."""
    async with _make_scenes(bridge, 3) as scenes:
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


async def test_scene_qualified_across_3_scenes(bridge):
    """get_component with 3 different scene-qualified paths works."""
    async with _make_scenes(bridge, 3) as scenes:
        obj_names = []
        for s in scenes:
            names = await _create_objects(bridge, s, "SQ", 1)
            obj_names.append(names[0])
        for s, obj in zip(scenes, obj_names):
            path = f"{s}:/{obj}"
            r = await bridge.send("get_component", {"path": path, "type": "Transform"})
            data = _ok(r)
            assert "position" in data.lower(), f"No Transform data for {path}"


async def test_deep_nested_qualified_path(bridge):
    """Root/A/B/C in additive → get_component Scene:/Root/A/B/C."""
    async with _make_scenes(bridge, 1) as scenes:
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


async def test_object_with_spaces(bridge):
    """'My Live Object' → search('My Live') finds it."""
    async with _make_scenes(bridge, 1) as scenes:
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


async def test_brackets_in_name_via_iid(bridge):
    """[SECTION/NAME] object findable by #iid."""
    async with _make_scenes(bridge, 1) as scenes:
        obj_name = f"[SECTION_{uuid.uuid4().hex[:4]}]"
        code = (
            f'var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByName("{scenes[0]}");'
            f'var go = new UnityEngine.GameObject("{obj_name}");'
            'UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, s);'
            'return "#" + go.GetInstanceID();'
        )
        r = await bridge.send("execute_code", {"code": code})
        iid = _iid(_ok(r))
        r2 = await bridge.send("get_component", {"path": iid, "type": "Transform"})
        data = _ok(r2)
        assert "position" in data.lower(), f"No Transform for bracket-named obj via {iid}"


async def test_stress_30_objects_3_scenes(bridge):
    """10 objects × 3 scenes = 30 objects searchable."""
    prefix = f"Stress30_{uuid.uuid4().hex[:4]}"
    async with _make_scenes(bridge, 3) as scenes:
        for s in scenes:
            await _create_objects(bridge, s, prefix, 10)
        r = await bridge.send("search_scene", {"query": prefix, "limit": "50"})
        data = _ok(r)
        found = data.count(prefix)
        assert found >= 30, f"Expected 30 objects, found {found} in:\n{data[:500]}"


async def test_10_objects_search_limit(bridge):
    """10 objects in 1 additive, limit=3 → +7 more in result."""
    async with _make_scenes(bridge, 1) as scenes:
        prefix = f"Lim10_{uuid.uuid4().hex[:4]}"
        await _create_objects(bridge, scenes[0], prefix, 10)
        r = await bridge.send("search_scene", {"query": prefix, "limit": "3"})
        data = _ok(r)
        assert "+7" in data or "more" in data.lower(), (
            f"Expected '+7 more' indicator for limit=3 with 10 objects:\n{data}"
        )
