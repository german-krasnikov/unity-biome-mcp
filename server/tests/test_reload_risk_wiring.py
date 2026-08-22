"""Integration tests — reload_risk wiring into asset.py, batch.py, sync.py, code_intel.py."""
import pytest
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.asset as asset_mod
import unity_mcp.tools.batch as batch_mod
import unity_mcp.tools.code_intel as ci_mod
import unity_mcp.tools.sync as sync_mod
from unity_mcp import reload_risk


def _make_args(**kw):
    return {k: v for k, v in kw.items() if v is not None}


@pytest.fixture(autouse=True)
def _reset_tracker():
    reload_risk.reset()
    yield
    reload_risk.reset()


@pytest.fixture(autouse=True)
def _patch_asset_deps():
    """Ensure changeset deps return None (no store/coordinator setup)."""
    with patch("unity_mcp.changeset_store.get_store", return_value=None), \
         patch("unity_mcp.changeset_coordinator.get_coordinator", return_value=None):
        yield


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


# --- asset.py ---

async def test_asset_write_cs_increments_tracker():
    """_write_text_with_capture with .cs path must touch() the tracker."""
    asset_mod._send = AsyncMock(return_value="ok")
    asset_mod._args = _make_args

    await asset_mod._write_text_with_capture("Assets/Foo.cs", "// code")

    assert reload_risk.current_count() == 1


async def test_asset_write_prefab_no_increment():
    """_write_text_with_capture with .prefab path must NOT touch() the tracker."""
    asset_mod._send = AsyncMock(return_value="ok")
    asset_mod._args = _make_args

    await asset_mod._write_text_with_capture("Assets/Foo.prefab", "data")

    assert reload_risk.current_count() == 0


# --- batch.py ---

async def test_batch_with_cs_write_increments_tracker():
    """batch() containing a .cs write must touch() the tracker."""
    batch_mod._send = AsyncMock(return_value="ok:1")

    await batch_mod.batch("asset action=write_text path=Assets/Foo.cs content=x")

    assert reload_risk.current_count() == 1


async def test_batch_without_script_writes_no_increment():
    """batch() with no script writes must NOT touch() the tracker."""
    batch_mod._send = AsyncMock(return_value="ok:1")

    await batch_mod.batch("create_object name=Cube")

    assert reload_risk.current_count() == 0


# --- code_intel.py ---

async def test_await_compile_short_circuits_when_no_touches():
    """await_compile must return immediately (no TCP) when no script writes this session."""
    send_mock = AsyncMock()
    ci_mod._send = send_mock
    ci_mod._mm_cached = False  # not HR mode

    result = await ci_mod.await_compile(timeout=60)

    assert result == "compile clean (no script writes)"
    send_mock.assert_not_called()


async def test_await_compile_proceeds_when_has_touches():
    """await_compile must NOT short-circuit when script writes happened."""
    reload_risk.touch()
    send_calls = []

    async def _fake_send(cmd, args=None, **kwargs):
        send_calls.append(cmd)
        if cmd == "get_status":
            return "mutation_mode=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    ci_mod._send = _fake_send
    ci_mod._mm_cached = False

    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        with patch.dict("os.environ", {"UNITY_MCP_COMPILE_SETTLE_SECS": "0"}):
            await ci_mod.await_compile(timeout=60)

    assert len(send_calls) > 0, "await_compile must call _send when touches exist"
