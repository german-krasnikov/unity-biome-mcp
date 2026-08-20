"""Lifespan helpers: middleware setup and budget initialization.

Extracted from server.py to keep the composition root concise.
All functions are pure-ish: they mutate the Middleware object passed in
but have no hidden side-effects beyond that.
"""
import os

from .middleware import Middleware


def build_middleware(send_raw_fn) -> Middleware | None:
    """Construct and configure Middleware from environment flags.

    Returns None if no middleware feature is enabled.
    send_raw_fn is the raw bridge send callable (needed by speculation/watchdog).
    """
    mw: Middleware | None = None

    def _mw() -> Middleware:
        nonlocal mw
        if mw is None:
            mw = Middleware()
        return mw

    if os.environ.get("UNITY_MCP_MIDDLEWARE"):
        from .sampling import SamplingService
        _mw().sampling = SamplingService()

    if os.environ.get("UNITY_MCP_HINTS", "1") != "0":
        from .hinter import ToolHinter
        _mw().hinter = ToolHinter(enabled=True)

    if os.environ.get("UNITY_MCP_SCENE_BRIEF"):
        from .scene_brief import SceneBrief
        _mw().scene_brief = SceneBrief()

    if os.environ.get("UNITY_MCP_SPECULATION"):
        from .speculation import SpeculativeLayer
        _mw().speculation = SpeculativeLayer(send_raw_fn)

    if os.environ.get("UNITY_MCP_LESSONS"):
        from .lessons import LessonRecorder, LessonStore
        from .paths import unity_mcp_dir
        store = LessonStore(unity_mcp_dir() / "lessons.json")
        _mw().lessons = store
        _mw().recorder = LessonRecorder(store)

    if os.environ.get("UNITY_MCP_WATCHDOG"):
        from .watchdog import ProactiveWatchdog
        _mw().watchdog = ProactiveWatchdog(send_raw_fn)

    if os.environ.get("UNITY_MCP_INFERENCE"):
        from .inference import Inferrer, SessionContext
        _mw().session = SessionContext()
        _mw().inferrer = Inferrer()

    return mw


def init_budget(mw: Middleware | None) -> tuple:
    """Initialize budget tracker+router. Returns (tracker, router) or (None, None)."""
    if os.environ.get("UNITY_MCP_BUDGET", "1") == "0":
        return None, None

    from .budget import BudgetRouter, CostTracker
    from .sampling import init_budget as _init_budget_sampling

    session_cap = float(os.environ.get("UNITY_MCP_HAIKU_BUDGET", "0.50"))
    day_cap = float(os.environ.get("UNITY_MCP_HAIKU_DAY_CAP", "5.00"))
    tracker = CostTracker(session_cap=session_cap, day_cap=day_cap)

    def _hit_rate(feature: str):
        if feature == "speculation" and mw and mw.speculation:
            spec = mw.speculation
            total = spec._hits + spec._misses
            return spec._hits / total if total > 0 else None
        return None

    router = BudgetRouter(tracker, _hit_rate)
    _init_budget_sampling(tracker, router)
    if mw is not None and mw.watchdog is not None:
        mw.watchdog._budget_gate = lambda: router.should_run("watchdog", 0.3).run
    return tracker, router


def wire_circuit_breaker(mw: Middleware | None, bridge_or_getter) -> None:
    """Wire compile-state probe into middleware circuit breaker.

    bridge_or_getter: either a UnityBridge instance (legacy) or a callable that
    returns the current bridge.  Use a getter (e.g. ``lambda: slot.bridge``) so
    the closure always evaluates the *live* bridge after a reconnect instead of
    the stale one captured at wire time.
    """
    if mw is None or bridge_or_getter is None:
        return

    def _get_bridge():
        return bridge_or_getter() if callable(bridge_or_getter) else bridge_or_getter

    def ready_fn() -> bool:
        bridge = _get_bridge()
        probe = getattr(bridge, "_probe", None)
        if probe is None:
            return True
        return not probe.has_strong_busy_signal()

    mw._circuit_ready_fn = ready_fn
    mw.circuit._is_ready_fn = ready_fn
