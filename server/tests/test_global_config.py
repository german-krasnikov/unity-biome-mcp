"""Tests for GlobalConfig — load/save/effective methods."""
import json
import threading
from unittest.mock import patch

from unity_mcp.global_config import GlobalConfig

# ─── load ──────────────────────────────────────────────────────────────────

def test_load_missing_file_returns_defaults(tmp_path):
    config_path = tmp_path / "global-config.json"
    with patch("unity_mcp.global_config.CONFIG_PATH", config_path):
        cfg = GlobalConfig.load()
    assert cfg.idle_timeout_min == 30
    assert cfg.idle_auto_suspend is True
    assert cfg.bridge_terminate_orphan is True
    assert cfg.bridge_orphan_grace_min == 2
    assert cfg._from_file is False


def test_load_valid_json_parses_correctly(tmp_path):
    config_path = tmp_path / "global-config.json"
    config_path.write_text(json.dumps({
        "idle_timeout_min": 10,
        "idle_auto_suspend": False,
        "bridge_terminate_orphan": False,
        "bridge_orphan_grace_min": 5,
    }), encoding="utf-8")
    with patch("unity_mcp.global_config.CONFIG_PATH", config_path):
        cfg = GlobalConfig.load()
    assert cfg.idle_timeout_min == 10
    assert cfg.idle_auto_suspend is False
    assert cfg.bridge_terminate_orphan is False
    assert cfg.bridge_orphan_grace_min == 5
    assert cfg._from_file is True


def test_load_malformed_json_returns_defaults(tmp_path):
    config_path = tmp_path / "global-config.json"
    config_path.write_text("not valid json {{{", encoding="utf-8")
    with patch("unity_mcp.global_config.CONFIG_PATH", config_path):
        cfg = GlobalConfig.load()
    assert cfg.idle_timeout_min == 30
    assert cfg._from_file is False


# ─── save ──────────────────────────────────────────────────────────────────

def test_save_atomic_write(tmp_path):
    config_path = tmp_path / "global-config.json"
    cfg = GlobalConfig(idle_timeout_min=15, idle_auto_suspend=False)
    with patch("unity_mcp.global_config.CONFIG_PATH", config_path):
        cfg.save()
    assert not config_path.with_suffix(".tmp").exists()
    saved = json.loads(config_path.read_text(encoding="utf-8"))
    assert saved["idle_timeout_min"] == 15
    assert saved["idle_auto_suspend"] is False


# ─── effective_idle_timeout_s ──────────────────────────────────────────────

def test_effective_idle_timeout_env_wins(monkeypatch):
    cfg = GlobalConfig(idle_timeout_min=10, _from_file=True)
    monkeypatch.setenv("UNITY_MCP_IDLE_TIMEOUT", "120")
    val, src = cfg.effective_idle_timeout_s()
    assert val == 120
    assert src == "env"


def test_effective_idle_timeout_config_wins_over_default(monkeypatch):
    cfg = GlobalConfig(idle_timeout_min=10, _from_file=True)
    monkeypatch.delenv("UNITY_MCP_IDLE_TIMEOUT", raising=False)
    val, src = cfg.effective_idle_timeout_s()
    assert val == 600
    assert src == "config"


def test_effective_idle_timeout_default(monkeypatch):
    cfg = GlobalConfig()  # _from_file=False
    monkeypatch.delenv("UNITY_MCP_IDLE_TIMEOUT", raising=False)
    val, src = cfg.effective_idle_timeout_s()
    assert val == 1800
    assert src == "default"


# ─── effective_auto_suspend ─────────────────────────────────────────────────

def test_effective_auto_suspend_env_off(monkeypatch):
    cfg = GlobalConfig()
    monkeypatch.setenv("UNITY_MCP_AUTO_SUSPEND", "0")
    val, src = cfg.effective_auto_suspend()
    assert val is False
    assert src == "env"


# ─── effective_orphan_grace_s ───────────────────────────────────────────────

def test_effective_orphan_grace_s(monkeypatch):
    cfg = GlobalConfig(bridge_orphan_grace_min=5, _from_file=True)
    monkeypatch.delenv("UNITY_MCP_ORPHAN_GRACE_MIN", raising=False)
    val, src = cfg.effective_orphan_grace_s()
    assert val == 300
    assert src == "config"


def test_effective_idle_timeout_malformed_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_IDLE_TIMEOUT", "abc")
    cfg = GlobalConfig()
    timeout, source = cfg.effective_idle_timeout_s()
    assert source == "default"
    assert timeout == 1800


def test_effective_orphan_grace_malformed_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_ORPHAN_GRACE_MIN", "xyz")
    cfg = GlobalConfig()
    grace, source = cfg.effective_orphan_grace_s()
    assert source == "default"
    assert grace == 120


# ─── watchdog config reload ─────────────────────────────────────────────────

def test_watchdog_reloads_config_after_n_cycles(monkeypatch):
    """GlobalConfig.load() must be called again after 10 sleep cycles in watchdog."""
    from unity_mcp import server as srv_module
    from unity_mcp.server import _start_idle_watchdog

    load_calls = []

    def mock_load():
        load_calls.append(1)
        return GlobalConfig()  # default: 1800s timeout → watchdog starts, won't exit

    stop = threading.Event()
    cycle = [0]

    def mock_sleep(_):
        cycle[0] += 1
        if cycle[0] >= 11:
            stop.set()

    monkeypatch.delenv("UNITY_MCP_IDLE_TIMEOUT", raising=False)
    monkeypatch.delenv("UNITY_MCP_USEFUL_IDLE_TIMEOUT", raising=False)

    with patch("unity_mcp.global_config.GlobalConfig.load", side_effect=mock_load), \
         patch("unity_mcp.server.time.sleep", side_effect=mock_sleep), \
         patch.object(srv_module, "_watchdog_stop", stop):
        stop.clear()
        t = _start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    # load() called once at loop start + once at cycle 10
    assert len(load_calls) == 2
