"""Tests for find_objects cache bypass in middleware."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware import Middleware, wrap_send


HIERARCHY = """\
SampleScene
├─ Gameplay &123
│  ├─ Characters &456
│  │  ├─ Player &789
│  │  └─ Enemy &012
│  └─ Enemies &345
└─ UI &678
   └─ HUD &901
"""


@pytest.fixture
def mw():
    m = Middleware()
    m.update_path_cache("get_hierarchy", HIERARCHY)
    return m


def test_find_objects_cache_hit_returns_path(mw):
    result = mw.find_from_cache("Player")
    assert result is not None
    assert "/Gameplay/Characters/Player" in result


def test_find_objects_cache_miss_returns_none(mw):
    assert mw.find_from_cache("Ghost") is None


def test_find_objects_empty_cache_returns_none():
    m = Middleware()
    assert m.find_from_cache("Player") is None


def test_find_objects_multiple_matches_returns_all(mw):
    # Both "Enemy" and "Enemies" end with different names, only "Enemy" exact-matches
    result = mw.find_from_cache("Enemy")
    assert result is not None
    assert "/Gameplay/Characters/Enemy" in result


def test_find_objects_none_name_returns_none(mw):
    assert mw.find_from_cache(None) is None


async def test_wrap_send_bypasses_unity_for_find_objects():
    mw = Middleware()
    mw.update_path_cache("get_hierarchy", HIERARCHY)
    fake_send = AsyncMock(return_value="unity result")
    wrapped = wrap_send(fake_send, mw)

    result = await wrapped("find_objects", {"name": "Player"})
    fake_send.assert_not_called()
    assert "/Gameplay/Characters/Player" in result


async def test_wrap_send_falls_through_on_cache_miss():
    mw = Middleware()
    mw.update_path_cache("get_hierarchy", HIERARCHY)
    fake_send = AsyncMock(return_value="unity result")
    wrapped = wrap_send(fake_send, mw)

    result = await wrapped("find_objects", {"name": "Ghost"})
    fake_send.assert_called_once()
