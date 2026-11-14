"""TDD tests for reflect/rules_system.py."""
import pytest
from unity_mcp.reflect import reflect, Mismatch


async def _r(cmd, args, response):
    return await reflect(cmd, args, response, None)


# ── set_llm_config ────────────────────────────────────────────────────────────

async def test_set_llm_config_ok():
    result = await _r("set_llm_config", {}, "ok")
    assert result is None


async def test_set_llm_config_mismatch():
    result = await _r("set_llm_config", {}, "applied config")
    assert isinstance(result, Mismatch)


async def test_set_llm_config_error_fail_open():
    result = await _r("set_llm_config", {}, "Error: invalid config")
    assert result is None


# ── watch ─────────────────────────────────────────────────────────────────────

async def test_watch_clean():
    result = await _r("watch", {}, "watching /Obj")
    assert result is None


async def test_watch_error():
    result = await _r("watch", {}, "Error: object not found")
    assert isinstance(result, Mismatch)


# ── checkpoint ────────────────────────────────────────────────────────────────

async def test_checkpoint_clean():
    result = await _r("checkpoint", {}, "Checkpoint 'before_change' created at G5")
    assert result is None


async def test_checkpoint_error():
    result = await _r("checkpoint", {}, "Error: undo stack empty")
    assert isinstance(result, Mismatch)


# ── sync_playtest_aliases_from_defs ──────────────────────────────────────────

async def test_sync_playtest_aliases_clean():
    result = await _r("sync_playtest_aliases_from_defs", {}, "loaded 5 aliases")
    assert result is None


async def test_sync_playtest_aliases_error():
    result = await _r("sync_playtest_aliases_from_defs", {}, "Error: file not found")
    assert isinstance(result, Mismatch)


# ── export_playtest_aliases_to_defs ──────────────────────────────────────────

async def test_export_playtest_aliases_clean():
    result = await _r("export_playtest_aliases_to_defs", {}, "exported 3 aliases to aliases.defs")
    assert result is None


async def test_export_playtest_aliases_error():
    result = await _r("export_playtest_aliases_to_defs", {}, "Failed to write file")
    assert isinstance(result, Mismatch)


# ── uitk_element (action-aware) ───────────────────────────────────────────────

async def test_uitk_element_write_clean():
    result = await _r("uitk_element", {"action": "set"}, "ok: element updated")
    assert result is None


async def test_uitk_element_write_error():
    result = await _r("uitk_element", {"action": "set"}, "Error: element not found")
    assert isinstance(result, Mismatch)


async def test_uitk_element_failed():
    result = await _r("uitk_element", {"action": "add"}, "Failed to add element")
    assert isinstance(result, Mismatch)


# ── uitk_file (action-aware, read={"read"}) ───────────────────────────────────

async def test_uitk_file_read_skips():
    result = await _r("uitk_file", {"action": "read"}, "Error: blah")
    assert result is None


async def test_uitk_file_write_clean():
    result = await _r("uitk_file", {"action": "write"}, "saved .uss file")
    assert result is None


async def test_uitk_file_write_error():
    result = await _r("uitk_file", {"action": "write"}, "Error: permission denied")
    assert isinstance(result, Mismatch)


# ── menu (action-aware, read={"list"}) ────────────────────────────────────────

async def test_menu_read_skips():
    result = await _r("menu", {"action": "list"}, "Error: blah")
    assert result is None


async def test_menu_write_clean():
    result = await _r("menu", {"action": "invoke"}, "Menu invoked: Assets/Create")
    assert result is None


async def test_menu_write_error():
    result = await _r("menu", {"action": "invoke"}, "Error: menu item not found")
    assert isinstance(result, Mismatch)
