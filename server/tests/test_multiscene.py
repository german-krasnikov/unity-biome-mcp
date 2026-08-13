"""Tests for multi-scene support across middleware and distiller.

Includes: path cache, split, validate, distiller, compress, scale/stress, edge cases.
"""
from unity_mcp.middleware_paths import PathResolverMixin, _split_scene_qualified
from unity_mcp.distiller import ResponseDistiller
from unity_mcp.server import compress_hierarchy

# ── fixtures ──────────────────────────────────────────────────────────────────

MULTI_SCENE_HIERARCHY = """[MainScene]
├─ Player &1
│  ├─ Camera &2
│  └─ Weapon &3
[AdditiveScene]
├─ Enemy &4
│  └─ Health &5
├─ Alice &6"""

SINGLE_SCENE_HIERARCHY = """├─ Player &1
│  ├─ Camera &2
│  └─ Weapon &3"""

MULTI_SCENE_HIER_WITH_INDENT = """[MainScene]
├─ Player &1
├─ Camera &2
[AdditiveScene]
├─ Enemy &3"""

THREE_SCENES = "[MainScene]\nPlayer &1\n├─ Camera &2\n[Level1]\nEnemy &3\n├─ Health &4\n[Level2]\nBoss &5\n├─ Shield &6\n├─ Weapon &7"
FIVE_SCENES = "\n".join(f"[Scene{i}]\nObj{i} &{i}" for i in range(1, 6))
TEN_SCENES = "\n".join(f"[Scene{i}]\nObj{i} &{i}" for i in range(10))
DEEP_HIER = "[Main]\nA &1\n├─ B &2\n│  ├─ C &3\n│  │  ├─ D &4\n│  │  │  └─ E &5"


# ── helpers ───────────────────────────────────────────────────────────────────

class FakeMixin(PathResolverMixin):
    def __init__(self):
        self.known_paths = set()
        self.path_to_scene = {}


def _m(hier: str) -> FakeMixin:
    m = FakeMixin()
    m.update_path_cache("get_hierarchy", hier)
    return m


# ── basic path cache ──────────────────────────────────────────────────────────

class TestPathCacheMultiScene:
    def test_tracks_scene_headers(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert "/Player" in m.known_paths
        assert "/Enemy" in m.known_paths
        assert "/Alice" in m.known_paths

    def test_scene_ownership(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert m.path_to_scene.get("/Player") == "MainScene"
        assert m.path_to_scene.get("/Enemy") == "AdditiveScene"
        assert m.path_to_scene.get("/Alice") == "AdditiveScene"

    def test_single_scene_no_scene_ownership(self):
        m = _m(SINGLE_SCENE_HIERARCHY)
        assert "/Player" in m.known_paths
        assert len(m.path_to_scene) == 0

    def test_children_tracked(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert "/Player/Camera" in m.known_paths
        assert "/Enemy/Health" in m.known_paths

    def test_scene_header_not_in_paths(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert not any("[" in p for p in m.known_paths)

    def test_clears_path_to_scene_on_refresh(self):
        m = FakeMixin()
        m.update_path_cache("get_hierarchy", MULTI_SCENE_HIERARCHY)
        assert len(m.path_to_scene) > 0
        m.update_path_cache("get_hierarchy", SINGLE_SCENE_HIERARCHY)
        assert len(m.path_to_scene) == 0

    def test_children_scene_ownership(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert m.path_to_scene.get("/Player/Camera") == "MainScene"
        assert m.path_to_scene.get("/Enemy/Health") == "AdditiveScene"


# ── path cache: scale ─────────────────────────────────────────────────────────

class TestPathCacheScale:
    def test_three_scenes_all_tracked(self):
        m = _m(THREE_SCENES)
        assert {"/Player", "/Enemy", "/Boss"}.issubset(m.known_paths)

    def test_three_scenes_ownership(self):
        m = _m(THREE_SCENES)
        assert m.path_to_scene["/Player"] == "MainScene"
        assert m.path_to_scene["/Enemy"] == "Level1"
        assert m.path_to_scene["/Boss"] == "Level2"

    def test_five_scenes_paths_distributed(self):
        m = _m(FIVE_SCENES)
        # bare paths + scene-qualified paths are both stored (P1 fix)
        for i in range(1, 6):
            assert f"/Obj{i}" in m.known_paths
            assert f"Scene{i}:/Obj{i}" in m.known_paths
            assert m.path_to_scene[f"/Obj{i}"] == f"Scene{i}"

    def test_ten_scenes_stress(self):
        m = _m(TEN_SCENES)
        # P1 fix: both bare and scene-qualified paths stored — 10 bare + 10 qualified = 20
        assert len(m.known_paths) == 20
        assert len(m.path_to_scene) == 10
        assert m.path_to_scene["/Obj9"] == "Scene9"


# ── path cache: name edge cases ───────────────────────────────────────────────

class TestPathCacheNameEdgeCases:
    def test_scene_name_with_spaces(self):
        m = _m("[My Scene Name]\nPlayer &1")
        assert m.path_to_scene.get("/Player") == "My Scene Name"

    def test_scene_name_with_parentheses(self):
        m = _m("[Level (unsaved)]\nEnemy &1")
        assert m.path_to_scene.get("/Enemy") == "Level (unsaved)"

    def test_dollar_in_scene_name_is_a_header(self):
        # After $ → & migration, $ in scene name does not prevent header detection
        m = _m("[$pecialScene]\nPlayer &1")
        assert m.path_to_scene.get("/Player") == "$pecialScene"

    def test_consecutive_scene_headers_empty_first(self):
        m = _m("[Scene1]\n[Scene2]\nObj &1")
        assert "/Obj" in m.known_paths
        assert m.path_to_scene["/Obj"] == "Scene2"

    def test_empty_scene_does_not_break_parser(self):
        m = _m("[EmptyScene]\n[NextScene]\nPlayer &1")
        # P1 fix: both bare and scene-qualified stored
        assert "/Player" in m.known_paths
        assert "NextScene:/Player" in m.known_paths

    def test_long_scene_name_with_underscores(self):
        name = "MyVeryLongSceneName_With_Underscores"
        m = _m(f"[{name}]\nObj &1")
        assert m.path_to_scene.get("/Obj") == name


# ── path cache: duplicates + deep hierarchy ───────────────────────────────────

class TestPathCacheDuplicatesAndDepth:
    def test_duplicate_object_across_scenes_last_wins(self):
        m = _m("[SceneA]\nPlayer &1\n[SceneB]\nPlayer &2\n[SceneC]\nPlayer &3")
        # P1 fix: all scene-qualified paths stored (disambiguation), last wins in path_to_scene
        assert "/Player" in m.known_paths
        assert "SceneA:/Player" in m.known_paths
        assert "SceneB:/Player" in m.known_paths
        assert "SceneC:/Player" in m.known_paths
        assert m.path_to_scene["/Player"] == "SceneC"

    def test_deep_hierarchy_five_levels(self):
        m = _m(DEEP_HIER)
        assert "/A/B/C/D/E" in m.known_paths
        assert m.path_to_scene["/A/B/C/D/E"] == "Main"

    def test_deep_hierarchy_intermediate_paths(self):
        m = _m(DEEP_HIER)
        for path in ("/A", "/A/B", "/A/B/C", "/A/B/C/D"):
            assert path in m.known_paths


# ── split scene qualified ─────────────────────────────────────────────────────

class TestSplitSceneQualified:
    def test_qualified(self):
        assert _split_scene_qualified("Scene:/foo") == ("Scene", "/foo")

    def test_unqualified(self):
        assert _split_scene_qualified("/foo") == ("", "/foo")

    def test_ref_ignored(self):
        # Both & (new) and $ (backward compat) are treated as refs, not scene names
        assert _split_scene_qualified("&1") == ("", "&1")
        assert _split_scene_qualified("$1") == ("", "$1")

    def test_no_slash_after_colon(self):
        assert _split_scene_qualified("foo:bar") == ("", "foo:bar")


class TestSplitSceneQualifiedEdge:
    def test_long_name_with_underscores(self):
        assert _split_scene_qualified("MyVeryLongSceneName_With_Underscores:/obj") == \
            ("MyVeryLongSceneName_With_Underscores", "/obj")

    def test_double_colon_first_wins(self):
        assert _split_scene_qualified("Scene:Name:/obj") == ("", "Scene:Name:/obj")

    def test_empty_scene_name(self):
        assert _split_scene_qualified(":/foo") == ("", "/foo")

    def test_just_colon(self):
        assert _split_scene_qualified(":") == ("", ":")


# ── validate path ─────────────────────────────────────────────────────────────

class TestValidatePathMultiScene:
    def test_validate_path_scene_qualified_no_warning(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert m.validate_path("AdditiveScene:/Enemy") is None

    def test_validate_path_wrong_scene_warns(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        result = m.validate_path("MainScene:/Enemy")
        assert result is not None
        assert "AdditiveScene" in result

    def test_validate_path_bare_still_works(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert m.validate_path("/Enemy") is None

    def test_validate_path_hash_ref_skipped(self):
        m = _m(MULTI_SCENE_HIERARCHY)
        assert m.validate_path("#12345") is None


class TestValidatePathBoundary:
    def test_qualified_nonexistent_path_warns(self):
        m = _m("[Main]\nPlayer &1")
        result = m.validate_path("Main:/NonExistent")
        assert result is not None
        assert "PATH WARNING" in result

    def test_path_with_spaces_exists(self):
        m = _m("[Main]\nMy Object &1")
        assert m.validate_path("/My Object") is None

    def test_dollar_ref_skipped(self):
        m = _m("[Main]\nPlayer &1")
        assert m.validate_path("$a") is None

    def test_ampersand_ref_skipped(self):
        m = _m("[Main]\nPlayer &1")
        assert m.validate_path("&1") is None

    def test_instance_id_skipped(self):
        m = _m("[Main]\nPlayer &1")
        assert m.validate_path("#12345") is None


# ── distiller ─────────────────────────────────────────────────────────────────

class TestDistillerMultiScene:
    def test_scene_headers_preserved(self):
        d = ResponseDistiller(min_size=0)
        result = d.distill_heuristic("get_hierarchy", MULTI_SCENE_HIER_WITH_INDENT, ("/Player",))
        assert "[MainScene]" in result.text

    def test_scene_headers_preserved_for_additive(self):
        d = ResponseDistiller(min_size=0)
        result = d.distill_heuristic("get_hierarchy", MULTI_SCENE_HIER_WITH_INDENT, ("/Enemy",))
        assert "[AdditiveScene]" in result.text


class TestDistillerBoundary:
    D = ResponseDistiller(min_size=0)

    def test_all_lines_scene_headers_preserved(self):
        text = "[Scene1]\n[Scene2]\n[Scene3]"
        r = self.D.distill_heuristic("get_hierarchy", text, ("/Player",))
        assert "[Scene1]" in r.text
        assert "[Scene2]" in r.text
        assert "[Scene3]" in r.text

    def test_both_headers_kept_when_focus_in_one_scene(self):
        text = "[Scene1]\nA $a\nB $b\nC $c\nD $d\nE $e\n[Scene2]\nX $x\nY $y\nZ $z"
        r = self.D.distill_heuristic("get_hierarchy", text, ("/A",))
        assert "[Scene1]" in r.text
        assert "[Scene2]" in r.text

    def test_hidden_count_message_accurate(self):
        text = "[Main]\n" + "\n".join(f"Obj{i} $r{i}" for i in range(20))
        r = self.D.distill_heuristic("get_hierarchy", text, ("/Obj0",))
        assert "+20 hidden" in r.text

    def test_scene_header_not_at_first_line(self):
        text = "Total: 5 objects\n[Scene1]\nA $a\nB $b\nC $c\nD $d\nE $e"
        r = self.D.distill_heuristic("get_hierarchy", text, ("/A",))
        assert "[Scene1]" in r.text


# ── compress ──────────────────────────────────────────────────────────────────

class TestCompressHierarchyMultiScene:
    def test_compress_preserves_scene_headers(self):
        from unity_mcp.tools.scene import compress_hierarchy
        text = "[MainScene]\nPlayer $a\n[AdditiveScene]\nEnemy $b"
        result = compress_hierarchy(text)
        assert "[MainScene]" in result
        assert "[AdditiveScene]" in result


class TestCompressSceneHeaders:
    def test_scene_header_breaks_slot_grouping(self):
        text = "[Main]\nslot_0 [] #a\nslot_1 [] #b\nslot_2 [] #c\n[Additive]\nslot_3 [] #d\nslot_4 [] #e\nslot_5 [] #f"
        result = compress_hierarchy(text)
        assert "[Main]" in result
        assert "[Additive]" in result
        assert "[3x slot]" in result

    def test_scene_header_passthrough_in_compress(self):
        text = "[Camera]\nObj $a"
        result = compress_hierarchy(text)
        assert "[Camera]" in result
        assert "Obj $a" in result
