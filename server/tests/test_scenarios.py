"""Tests for playtest scenario persistence tools."""
import pytest
from unittest.mock import AsyncMock, patch
import unity_mcp.tools.scenarios as scenarios


# ── helpers ────────────────────────────────────────────────────────────────

def _filter_none(**kwargs) -> dict:
    return {k: v for k, v in kwargs.items() if v is not None}


@pytest.fixture(autouse=True)
def reset_module_globals():
    """Prevent _send/_args from leaking between tests."""
    orig_send, orig_args = scenarios._send, scenarios._args
    scenarios._send = None
    scenarios._args = _filter_none
    yield
    scenarios._send = orig_send
    scenarios._args = orig_args


@pytest.fixture
def fake_dir(tmp_path, monkeypatch):
    monkeypatch.setattr(scenarios, "_scenarios_dir", lambda create=False: str(tmp_path))
    return tmp_path


@pytest.fixture
def mock_send():
    send = AsyncMock(return_value="OK: done")
    scenarios._send = send
    scenarios._args = _filter_none
    return send


# ── save_scenario ──────────────────────────────────────────────────────────

async def test_save_scenario_creates_file(fake_dir):
    result = await scenarios.save_scenario("my_test", "ASSERT /Player|HP == 100")
    assert (fake_dir / "my_test.playtest").read_text(encoding="utf-8") == "ASSERT /Player|HP == 100"
    assert "Saved" in result


async def test_save_scenario_invalid_name_rejects(fake_dir):
    result = await scenarios.save_scenario("bad name!", "script")
    assert result.startswith("Error:")
    assert not any(fake_dir.iterdir())


async def test_save_scenario_overwrites(fake_dir):
    await scenarios.save_scenario("s", "first")
    await scenarios.save_scenario("s", "second")
    assert (fake_dir / "s.playtest").read_text(encoding="utf-8") == "second"


# ── load_scenario ──────────────────────────────────────────────────────────

async def test_load_scenario_reads_file(fake_dir):
    (fake_dir / "demo.playtest").write_text("ASSERT_CONSOLE_CLEAN", encoding="utf-8")
    result = await scenarios.load_scenario("demo")
    assert result == "ASSERT_CONSOLE_CLEAN"


async def test_load_scenario_not_found(fake_dir):
    result = await scenarios.load_scenario("missing")
    assert result.startswith("Error:")


async def test_load_scenario_path_traversal_rejected(fake_dir):
    result = await scenarios.load_scenario("../etc/passwd")
    assert result.startswith("Error:") and "Invalid" in result


# ── list_scenarios ─────────────────────────────────────────────────────────

async def test_list_scenarios_returns_names(fake_dir):
    for name in ("charlie", "alpha", "bravo"):
        (fake_dir / f"{name}.playtest").write_text("x", encoding="utf-8")
    result = await scenarios.list_scenarios()
    assert result == "alpha\nbravo\ncharlie"


async def test_list_scenarios_empty(fake_dir):
    result = await scenarios.list_scenarios()
    assert result == "No scenarios found"


async def test_list_scenarios_ignores_other_files(fake_dir):
    (fake_dir / "keep.playtest").write_text("x", encoding="utf-8")
    (fake_dir / "ignore.txt").write_text("y", encoding="utf-8")
    result = await scenarios.list_scenarios()
    assert result == "keep"


# ── run_scenario ───────────────────────────────────────────────────────────

async def test_run_scenario_loads_and_sends(fake_dir, mock_send):
    (fake_dir / "smoke.playtest").write_text("ASSERT /P|C|f == v", encoding="utf-8")
    result = await scenarios.run_scenario("smoke")
    assert result == "OK: done"
    mock_send.assert_called_once()
    call_args = mock_send.call_args[0]
    assert call_args[0] == "run_playtest"
    assert call_args[1]["script"] == "ASSERT /P|C|f == v"


async def test_run_scenario_not_found_skips_send(fake_dir, mock_send):
    result = await scenarios.run_scenario("nonexistent")
    assert result.startswith("Error:")
    mock_send.assert_not_called()


async def test_run_scenario_abort_on_fail_passes_arg(fake_dir, mock_send):
    (fake_dir / "t.playtest").write_text("ASSERT x", encoding="utf-8")
    await scenarios.run_scenario("t", abort_on_fail=True)
    sent = mock_send.call_args[0][1]
    assert sent["abort_on_fail"] == "true"


async def test_run_scenario_default_abort_omits_arg(fake_dir, mock_send):
    (fake_dir / "t.playtest").write_text("ASSERT x", encoding="utf-8")
    await scenarios.run_scenario("t", abort_on_fail=False)
    sent = mock_send.call_args[0][1]
    assert "abort_on_fail" not in sent
