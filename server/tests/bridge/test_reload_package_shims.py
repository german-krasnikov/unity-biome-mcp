"""Back-compat shim pins for the reload/ package extraction (Phase 3.1).

7 modules became sys.modules-aliased shims pointing at their new
unity_mcp.reload.* home: 5 moved out of tools/ (sync, sync_module,
sync_algorithm, sync_spec, reload_ladder), 2 moved out of bridge/
(compile_state, reload_state) -- see Plans/task-p3-reload-package.md. Each pin
here is two assertions, following test_bridge_package_shims.py's pattern:
  1. identity — old and new dotted names resolve to the SAME module object,
     not a copy (sys.modules aliasing, not the read-only __getattr__/PEP-562
     pattern used elsewhere in this codebase).
  2. patch-propagation — monkeypatching the OLD path must be observable
     through the NEW path. This is the false-green gap a `__getattr__` shim
     would reopen: ~20 existing tests patch these modules by old string path,
     and a distinct shim module object would silently stop affecting the
     real code those patches target.

compile_state and reload_state are double-aliased: the pre-existing root
shims unity_mcp.compile_state / unity_mcp.bridge_reload_state (Phase 4) point
at unity_mcp.bridge.compile_state / unity_mcp.bridge.reload_state, which this
phase turns into shims of their own pointing at
unity_mcp.reload.compile_state / unity_mcp.reload.reload_state. CPython's
import machinery re-reads sys.modules after exec_module (to support exactly
this self-reassignment pattern), so all three names resolve to one object;
this module checks that chain holds too.
"""
import unity_mcp.bridge.compile_state
import unity_mcp.bridge.reload_state
import unity_mcp.bridge_reload_state
import unity_mcp.compile_state
import unity_mcp.reload.reload_ladder
import unity_mcp.reload.sync
import unity_mcp.reload.sync_algorithm
import unity_mcp.reload.sync_module
import unity_mcp.reload.sync_spec
import unity_mcp.tools.reload_ladder
import unity_mcp.tools.sync
import unity_mcp.tools.sync_algorithm
import unity_mcp.tools.sync_module
import unity_mcp.tools.sync_spec


def test_sync_shim_is_same_module_object():
    assert unity_mcp.tools.sync is unity_mcp.reload.sync


def test_sync_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.sync._bump_used", "sentinel")
    assert unity_mcp.reload.sync._bump_used == "sentinel"


def test_sync_module_shim_is_same_module_object():
    assert unity_mcp.tools.sync_module is unity_mcp.reload.sync_module


def test_sync_module_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.sync_module.SyncModule", "sentinel")
    assert unity_mcp.reload.sync_module.SyncModule == "sentinel"


def test_sync_algorithm_shim_is_same_module_object():
    assert unity_mcp.tools.sync_algorithm is unity_mcp.reload.sync_algorithm


def test_sync_algorithm_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.sync_algorithm.BumpFlag", "sentinel")
    assert unity_mcp.reload.sync_algorithm.BumpFlag == "sentinel"


def test_sync_spec_shim_is_same_module_object():
    assert unity_mcp.tools.sync_spec is unity_mcp.reload.sync_spec


def test_sync_spec_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.sync_spec.OWNED_WIRE_COMMANDS", "sentinel")
    assert unity_mcp.reload.sync_spec.OWNED_WIRE_COMMANDS == "sentinel"


def test_reload_ladder_shim_is_same_module_object():
    assert unity_mcp.tools.reload_ladder is unity_mcp.reload.reload_ladder


def test_reload_ladder_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.reload_ladder._T1_POLL_S", "sentinel")
    assert unity_mcp.reload.reload_ladder._T1_POLL_S == "sentinel"


def test_compile_state_shim_is_same_module_object():
    assert unity_mcp.bridge.compile_state is unity_mcp.reload.compile_state
    assert unity_mcp.compile_state is unity_mcp.reload.compile_state


def test_compile_state_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge.compile_state.CompileStateProbe", "sentinel")
    assert unity_mcp.reload.compile_state.CompileStateProbe == "sentinel"
    assert unity_mcp.compile_state.CompileStateProbe == "sentinel"


def test_reload_state_shim_is_same_module_object():
    assert unity_mcp.bridge.reload_state is unity_mcp.reload.reload_state
    assert unity_mcp.bridge_reload_state is unity_mcp.reload.reload_state


def test_reload_state_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge.reload_state.DomainReloadTracker", "sentinel")
    assert unity_mcp.reload.reload_state.DomainReloadTracker == "sentinel"
    assert unity_mcp.bridge_reload_state.DomainReloadTracker == "sentinel"
