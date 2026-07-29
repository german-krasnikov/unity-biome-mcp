"""Tests for full-path cache and fuzzy resolve_path."""
import pytest
from unity_mcp.middleware import Middleware

HIERARCHY = """\
Scene
├─ Gameplay $123
│  ├─ Characters $456
│  │  ├─ Player $789
│  │  └─ Enemy $012
│  └─ Enemies $345
└─ UI $678
   └─ HUD $901
"""


@pytest.fixture
def mw():
    m = Middleware()
    m.update_path_cache("get_hierarchy", HIERARCHY)
    return m


# ─── update_path_cache stores full paths ──────────────────────────────────────

def test_path_cache_stores_full_path(mw):
    assert "/Gameplay/Characters/Player" in mw.known_paths


def test_path_cache_stores_nested_path(mw):
    assert "/Gameplay/Characters/Enemy" in mw.known_paths


def test_path_cache_stores_root_child(mw):
    assert "/Gameplay" in mw.known_paths


def test_path_cache_stores_ui_hud(mw):
    assert "/UI/HUD" in mw.known_paths


def test_path_cache_no_bare_names(mw):
    # Should NOT store bare leaf names like "Player" without slash prefix
    assert "Player" not in mw.known_paths
    assert "Enemy" not in mw.known_paths


# ─── validate_path uses full paths ────────────────────────────────────────────

def test_validate_path_full_path_ok(mw):
    assert mw.validate_path("/Gameplay/Characters/Player") is None


def test_validate_path_unknown_warns(mw):
    result = mw.validate_path("/Gameplay/Characters/Ghost")
    assert result is not None
    assert "PATH WARNING" in result


def test_validate_path_empty_cache_allows_all():
    m = Middleware()
    assert m.validate_path("/Any/Path") is None


def test_validate_path_ref_syntax_always_ok(mw):
    assert mw.validate_path("$ref:123") is None


# ─── resolve_path fuzzy matching ──────────────────────────────────────────────

def test_resolve_path_exact_match(mw):
    assert mw.resolve_path("/Gameplay/Characters/Player") == "/Gameplay/Characters/Player"


def test_resolve_path_leaf_match(mw):
    # "Player" → "/Gameplay/Characters/Player"
    result = mw.resolve_path("Player")
    assert result == "/Gameplay/Characters/Player"


def test_resolve_path_partial_path(mw):
    # "Characters/Player" → "/Gameplay/Characters/Player"
    result = mw.resolve_path("Characters/Player")
    assert result == "/Gameplay/Characters/Player"


def test_resolve_path_no_match_returns_original(mw):
    result = mw.resolve_path("NonExistent")
    assert result == "NonExistent"


def test_resolve_path_empty_cache_returns_original():
    m = Middleware()
    assert m.resolve_path("Anything") == "Anything"


# ─── P1: multi-scene scene-qualified path cache ───────────────────────────────

MULTI_SCENE_HIERARCHY = """\
[Scene1]
├─ Player $123
└─ Enemy $456

[Scene2]
├─ Player $789
└─ Boss $012
"""


def test_path_cache_multiscene_same_path_both_scenes_tracked():
    """P1: both scene-qualified paths should be stored in multi-scene hierarchy."""
    m = Middleware()
    m.update_path_cache("get_hierarchy", MULTI_SCENE_HIERARCHY)
    assert "Scene1:/Player" in m.known_paths
    assert "Scene2:/Player" in m.known_paths
