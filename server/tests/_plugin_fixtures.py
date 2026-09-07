"""Shared fixtures/helpers for test_plugin_atomicity.py (kept separate so that
file stays under the 300-line budget). Fixtures defined here are picked up by
pytest via a plain `from _plugin_fixtures import ...` re-export in the test
module — pytest collects autouse fixtures from any name visible in the test
module's namespace, imported or not.
"""
import types

import pytest

from unity_mcp.plugins import _atomic


@pytest.fixture(autouse=True)
def _reset_atomic_scoped_state():
    """_failed and _owner.{current,journal} are module-level globals that
    persist across tests (like _command_owner, which every test already
    resets via monkeypatch.setattr). Reset them here so `len(failed)`
    assertions are deterministic regardless of test order or what ran
    earlier in the same process."""
    from unity_mcp.plugins import _owner

    _atomic._failed.clear()
    _owner.current = None
    _owner.journal.clear()
    yield
    _atomic._failed.clear()
    _owner.current = None
    _owner.journal.clear()


@pytest.fixture(autouse=True)
def _restore_gating_state():
    """A successfully-registered plugin tool gets auto-gated into gating's shared
    module state (_ALL_KNOWN / _THEMED_CATEGORIES / CATEGORIES) — restore it exactly,
    the same wholesale snapshot/restore shape _atomic.py itself uses, so these tests
    don't leak fake tool names into test_schema_parity.py or other test files."""
    from unity_mcp.tools import gating
    all_known = set(gating._ALL_KNOWN)
    themed = {k: list(v) for k, v in gating._THEMED_CATEGORIES.items()}
    categories = gating.CATEGORIES
    yield
    gating._ALL_KNOWN.clear()
    gating._ALL_KNOWN.update(all_known)
    gating._THEMED_CATEGORIES.clear()
    gating._THEMED_CATEGORIES.update(themed)
    gating.CATEGORIES = categories


def _mk_module(register_fn):
    mod = types.ModuleType("fake_plugin_module")
    mod.register = register_fn
    return mod


def _declare_write_cmds_typo():
    from unity_mcp.plugin_api import register_write_cmds
    register_write_cmds("typo_tool")


def _declare_register_tools_typo():
    from unity_mcp.plugin_api import register_tools
    register_tools("SYSTEM", {"typo_tool"})


def _declare_dsl_tools_typo():
    from unity_mcp.plugin_api import register_dsl_tools
    register_dsl_tools("typo_tool")


def _declare_features_typo():
    from unity_mcp.plugin_api import register_features
    register_features({"typo_tool": {
        "priority": "low", "difficulty": 0.1, "est_in": 10, "est_out": 10, "image": False,
    }})
