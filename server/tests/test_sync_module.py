"""N1b: SyncModule instance binding, owned-vs-consumed inventory, and the
production SDK registration route. Mirrors test_watch.py's WatchModule A/B
pattern (Plans/N1b-second-module-sync.md section 3.1).

Self-contained: patches unity_mcp.tools.sync.editor_log directly instead of
relying on test_sync.py's autouse fixtures, and restores every module global
tools/sync_module.py touches (_default) or tools/sync.py touches (_send,
_args, _bump_used) after each test -- these are process-wide singletons
(same shape as every other tools/*.py module), so leaking a fake send into
them would pollute later tests/production state.
"""
from unittest.mock import AsyncMock, patch

import pytest

# Import unity_mcp.server BEFORE anything captures sync_module._default: its
# module-level register_all() call is what first sets _default to the real
# production instance. If that import happened lazily inside a test body,
# the autouse restore fixture below would capture/restore None instead of
# the production instance, corrupting test_production_sync_unity_route_*.
import unity_mcp.server  # noqa: F401
from unity_mcp.tools import sync as _sync
from unity_mcp.tools import sync_module
from unity_mcp.tools.sync_module import SyncModule, register
from unity_mcp.tools.tool_specs import _SPEC_OWNERS


def _plain_args(**kwargs) -> dict:
    """Mirrors server.py's own _args(**kwargs) factory (drop None values)."""
    return {k: v for k, v in kwargs.items() if v is not None}


def _clean_send(tag: str):
    """Fast will_compile=false round trip: enough to prove which instance's
    send a sync_unity() call actually reached, without depending on
    test_sync.py's autouse fixtures (this file patches editor_log itself)."""
    async def _send(cmd: str, args: dict | None = None, **kwargs):
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "compile_status":
            return "idle|1"
        if cmd == "get_compile_errors":
            return "No compilation errors"
        if cmd == "warm_type_cache":
            return "ok"
        if cmd == "diagnose":
            # _get_errors() always reaches this final check when state=='idle'
            # and errors=='' -- main_mvid=absent short-circuits _verdict() to
            # no-failure (mirrors test_sync.py's _make_send helper).
            return "main_mvid=absent"
        raise AssertionError(f"[{tag}] unexpected cmd {cmd!r}")

    return AsyncMock(side_effect=_send)


@pytest.fixture(autouse=True)
def _patch_editor_log():
    async def _corroborated(send, *, compile_status=""):
        csharp = await send("get_compile_errors", {})
        return "" if csharp.strip() == "No compilation errors" else csharp

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.get_corroborated_errors = _corroborated
        mock_el.init_corroboration = lambda *a, **k: None
        yield mock_el


@pytest.fixture(autouse=True)
def _restore_module_globals():
    """sync_module._default and sync.{_send,_args,_bump_used} are process-wide
    singletons production code depends on -- restore whatever this test
    session already had (production register_all() runs at server.py import
    time, before any test runs)."""
    orig_default = sync_module._default
    orig_send = _sync._send
    orig_args = getattr(_sync, "_args", None)
    orig_bump = _sync._bump_used
    yield
    sync_module._default = orig_default
    _sync._send = orig_send
    _sync._args = orig_args
    _sync._bump_used = orig_bump


class _FakeMcp:
    """Minimal FastMCP-shaped stand-in for register()'s structural checks that
    don't need the real SDK's schema machinery."""
    def __init__(self):
        self.registered = {}

    def tool(self, **_kwargs):
        def decorator(fn):
            self.registered[fn.__name__] = fn
            return fn
        return decorator


# ── Instance binding (checkbox 1) ────────────────────────────────────────

async def test_sync_module_instances_bind_send_independently():
    """Two SyncModule instances registered on two real FastMCP hosts must
    never cross-contaminate _send. A's calls reach only send_a; after B
    registers later (on its own host), A's calls still go to send_a --
    late registration of B must not rebind A."""
    from unity_mcp.server import _UnstructuredMCP

    host_a = _UnstructuredMCP("test_sync_module_host_a")
    send_a = _clean_send("a")
    mod_a = register(host_a, send_a, _plain_args)
    assert "sync_unity" in host_a._tool_manager._tools
    assert host_a._tool_manager._tools["sync_unity"].fn.__self__ is mod_a

    result_a1 = await mod_a.sync_unity()
    assert "sync clean" in result_a1
    send_a.assert_awaited()

    host_b = _UnstructuredMCP("test_sync_module_host_b")
    send_b = _clean_send("b")
    mod_b = register(host_b, send_b, _plain_args)
    assert mod_a is not mod_b
    assert mod_a._send is send_a
    assert mod_b._send is send_b

    send_a.reset_mock()
    result_b1 = await mod_b.sync_unity()
    assert "sync clean" in result_b1
    send_b.assert_awaited()
    send_a.assert_not_awaited()

    # Late registration of B must not rebind A.
    send_a.reset_mock()
    send_b.reset_mock()
    result_a2 = await mod_a.sync_unity()
    assert "sync clean" in result_a2
    send_a.assert_awaited()
    send_b.assert_not_awaited()


async def test_negative_control_cross_instance_binding_leaks_to_wrong_send():
    """Proves the assertions above are sensitive to a real cross-instance
    leak (not vacuously true): force mod_a to hold mod_b's send -- as a
    naive shared module-global bind(globals(), ...) design would after a
    second register() call -- and confirm the call now reaches send_b."""
    send_a = _clean_send("a")
    send_b = _clean_send("b")
    mod_a = SyncModule(send_a, _plain_args)
    mod_b = SyncModule(send_b, _plain_args)
    mod_a._send = mod_b._send  # simulate the leak

    await mod_a.sync_unity()

    send_a.assert_not_awaited()
    send_b.assert_awaited()


def test_register_called_twice_returns_independently_bound_modules():
    """The module-level register(mcp, send, args) compat shim remains a
    process-wide singleton pointer (documented, accepted scope limit) --
    but each call must still return a fully independent SyncModule bound to
    its own send/args."""
    mcp_a, mcp_b = _FakeMcp(), _FakeMcp()
    send_a, send_b = AsyncMock(), AsyncMock()

    mod_a = register(mcp_a, send_a, _plain_args)
    mod_b = register(mcp_b, send_b, _plain_args)

    assert mod_a is not mod_b
    assert mod_a._send is send_a
    assert mod_b._send is send_b


# ── Owned vs consumed inventory (checkbox 2) ─────────────────────────────

def test_sync_module_owned_and_consumed_listed_separately():
    """The module declares owned public->wire names and consumed
    dependencies as distinct data (not just prose); _SPEC_OWNERS routes
    sync_unity to 'sync'; consumed names are never owned."""
    owned_wire = set(sync_module.OWNED_WIRE_COMMANDS["sync_unity"])
    consumed = sync_module.CONSUMED_DEPENDENCIES

    assert owned_wire == {"sync", "sync_status"}
    assert not (owned_wire & consumed), "owned wire commands leaked into consumed dependencies"
    assert "get_compile_errors" in consumed
    assert "warm_type_cache" in consumed
    assert "force_refresh" in consumed  # C# Reload/recovery owns it; Sync only consumes it

    assert _SPEC_OWNERS["sync_unity"] == "sync"


def test_negative_control_wrong_owner_mapping_fails_ownership_check(monkeypatch):
    """Proves the ownership assertion above is load-bearing: corrupt
    _SPEC_OWNERS['sync_unity'] to a different owner and confirm the same
    check now fails."""
    monkeypatch.setitem(_SPEC_OWNERS, "sync_unity", "watch")
    with pytest.raises(AssertionError):
        assert _SPEC_OWNERS["sync_unity"] == "sync"


# ── Production route (checkbox 3) ────────────────────────────────────────

def test_production_sync_unity_route_uses_module_instance():
    """server.py's register_all() -> tool_specs build produces a sync_unity
    tool whose handler is a SyncModule instance's bound method, not the
    module-level legacy compat adapter."""
    from unity_mcp.server import mcp as production_mcp

    tool = production_mcp._tool_manager._tools["sync_unity"]
    handler = tool.fn

    assert getattr(handler, "__self__", None) is not None
    assert isinstance(handler.__self__, SyncModule)
    assert handler.__func__ is SyncModule.sync_unity
    assert handler is not sync_module.sync_unity
    assert handler.__self__ is sync_module._default
