"""Full middleware pipeline — wrap_send function."""
import asyncio
import os
import time
from typing import TYPE_CHECKING, Optional

from mcp.server.fastmcp.exceptions import ToolError

from . import middleware_alias as _alias_hooks  # noqa: F401 — trigger hook registration
from .bridge_result import unwrap_bridge_result
from .compressor import strip_defaults
from .middleware_hooks import register_post, run_post_hooks
from .middleware_types import (
    _READ_CACHEABLE,
    _STRIP_CMDS,
    SCENE_STATE_NEUTRAL_WRITES,
    WRITE_CMDS,
)
from .prefetch_cache import GATE_PRIORS

if TYPE_CHECKING:
    from .middleware import Middleware


@register_post("get_hierarchy")
def _hook_track_hierarchy_call(cmd: str, args: dict, result: str, mw) -> str:
    mw._last_hierarchy_call = mw.call_count
    return result


def wrap_send(send_fn, mw: Optional["Middleware"] = None):
    """Return a wrapped _send that runs all middleware checks."""
    from .middleware import Middleware as _Middleware
    if mw is None:
        mw = _Middleware()

    async def wrapped(cmd: str, args: dict, timeout: float = 0) -> str:
        # ToolHinter: adoption check at call start
        if mw.hinter is not None:
            mw.hinter.note_adoption(cmd)

        # Strip internal flags BEFORE sending to bridge — must not leak to Unity
        _no_reflect = bool(args.get("_no_reflect", False))
        _no_distill = bool(args.get("_no_distill", False))
        _explicit_path = bool(args.get("_explicit_path", False))
        _no_validate = bool(args.get("_no_validate", False))
        _no_strip = bool(args.get("_no_strip", False))
        args = {k: v for k, v in args.items() if k not in (
            "_no_reflect", "_no_distill", "_explicit_path", "_no_validate", "_no_strip"
        )}

        # Alias resolution: $name → cached pipe value (Hook 1)
        if mw._alias_cache:
            from .middleware_alias import resolve_aliases_in_args
            args = resolve_aliases_in_args(args, mw._alias_cache)

        # F05: Cache-above-circuit — serve cacheable reads from PrefetchCache even when OPEN
        if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
            _pre_cached = mw._prefetch_cache.get(cmd, args)
            if _pre_cached is not None:
                # Cache hit while HALF_OPEN: evidence system is healthy → heal circuit
                if mw.circuit.state == mw.circuit.HALF_OPEN:
                    mw.circuit.record_success()
                if _pre_cached.startswith("[CACHED:"):
                    return _pre_cached
                return f"[CACHED]\n{_pre_cached}"

        # Circuit breaker check
        if not mw.circuit.allow_request():
            secs = int(mw.circuit.remaining()) + 1
            return f"⚡ Circuit OPEN: Unity unavailable. Auto-retry in {secs}s"

        _probe_active = mw.circuit._probe_in_flight

        def _early_return(val):
            if _probe_active:
                mw.circuit.record_success()
            return val

        # Tier C: watchdog pending alert
        watchdog_alert = mw.watchdog.consume_alert() if mw.watchdog else None

        # Pre-call checks — guards see ORIGINAL cmd/args (before any rerouting)
        retry_warn = mw.check_retry(cmd, args)
        if retry_warn:
            return _early_return(retry_warn)
        taint_warn = mw.check_taint(cmd, args)
        dead_warn = mw.check_dead_write(cmd, args)
        blast_warn = mw.check_blast_radius(cmd, args)
        verif_warn = mw.check_verification_needed(cmd, args)
        batch_warn = mw.scan_batch_conflicts(args.get("commands", "")) if cmd == "batch" else None

        # Fail-fast: block runtime-only commands when confirmed in edit mode (before TCP)
        pm_block = mw.check_play_mode_required(cmd)
        if pm_block:
            return _early_return(pm_block)

        # P-324: block mutation commands for read-only endpoints (before TCP)
        ro_block = mw.check_read_only(cmd, args)
        if ro_block:
            raise ToolError(ro_block)

        # Play mode auto-routing — AFTER guards so they see original cmd
        cmd, args = mw.reroute_cmd(cmd, args)

        # Tier C: speculation hit tracking — sees rerouted cmd (what's actually sent)
        if mw.speculation is not None:
            mw.speculation.record_actual_next(cmd)

        # Tier C: lessons hint (prepend to result later) — sees rerouted cmd
        lessons_hint = mw.lessons.hint_for(cmd, args) if mw.lessons else None

        # Tier C: argument inference
        inferred_tags: list = []
        if mw.inferrer is not None and mw.session is not None:
            args, inferred_tags = mw.inferrer.infer(cmd, args, mw.session)

        # find_objects cache bypass
        if cmd == "find_objects" and not args.get("tag") and not args.get("layer") and not args.get("component"):
            cached = mw.find_from_cache(args.get("name"))
            if cached is not None:
                return _early_return(cached)

        # P1: Pre-flight path resolution via live search
        resolve_marker = ""
        if "path" in args and args["path"] and not _explicit_path:
            resolved, resolve_marker = await mw.resolve_path_live(args["path"], send_fn)
            if resolved.startswith("__DISAMBIG_BLOCK__"):
                return _early_return(resolved.split("\n", 1)[1])
            if resolved != args["path"]:
                args = {**args, "path": resolved}

        # SchemaGuard pre-flight validation
        if mw.schema_guard is not None and not _no_validate:
            block = await mw.schema_guard.validate(cmd, args, send_fn)
            if block is not None:
                from .metrics import METRICS
                METRICS.inc("validate.blocked")
                return _early_return(block)

        # P1: Component existence pre-check (blocks when cache confirms absence)
        if cmd == "set_property" and "component" in args:
            comp_warn = mw.check_component_exists(args.get("path", ""), args["component"])
            if comp_warn:
                return _early_return(comp_warn)

        # PrefetchCache: serve cached reads before TCP round-trip
        if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
            cached = mw._prefetch_cache.get(cmd, args)
            if cached is not None:
                # Synthetic entries already carry [CACHED:<source>] tag — don't double-wrap
                if cached.startswith("[CACHED:"):
                    return _early_return(cached)
                return _early_return(f"[CACHED]\n{cached}")

        # Alive check: quick ping if last success was >30s ago
        if not mw.check_alive():
            try:
                await send_fn("ping", {}, timeout=3.0)
            except Exception:
                mw.circuit.record_failure()
                if _probe_active:
                    mw.circuit.release_probe()
                raise

        # Execute
        from .metrics import METRICS
        METRICS.inc(f"cmd.{cmd}.calls")
        try:
            with METRICS.timer(f"cmd.{cmd}.ms"):
                result = await send_fn(cmd, args, timeout=timeout)
        except Exception:
            METRICS.inc(f"cmd.{cmd}.fail")
            mw.circuit.record_failure()
            if _probe_active:
                mw.circuit.release_probe()
            raise
        mw.circuit.record_success()
        mw._last_success = time.time()

        # Extract string from dict response (when send_fn is raw bridge.send)
        protocol_err = False
        if isinstance(result, dict):
            # T15: extract receipt before unwrapping — backward-compat (old Python ignores it)
            _receipt = result.get("receipt")
            if _receipt:
                from .changeset_coordinator import get_coordinator
                _c = get_coordinator()
                if _c is not None:
                    _c.append(cmd, _receipt)
            result, ok = unwrap_bridge_result(result)
            protocol_err = not ok

        # F08: strip defaults unconditionally for component reads
        if cmd in _STRIP_CMDS and not _no_strip:
            result = strip_defaults(result)

        # F16: dedup only GENUINE protocol errors — never success payloads that merely
        # contain "Error" as data (e.g. get_console / an object named "ErrorHandler").
        if protocol_err:
            result = mw.dedup_error(cmd, result)

        # PrefetchCache: on write, invalidate path + fire background prefetch
        if (
            cmd in WRITE_CMDS
            and cmd not in SCENE_STATE_NEUTRAL_WRITES
            and mw._prefetch_cache is not None
        ):
            path = args.get("path", "")
            if path:
                mw._prefetch_cache.invalidate_path(path)
            prior_fn = GATE_PRIORS.get(cmd)
            if prior_fn:
                predicted = prior_fn(args)
                if predicted:
                    p_cmd, p_args = predicted
                    t = asyncio.create_task(mw._background_prefetch(p_cmd, p_args, send_fn))
                    mw._bg_tasks.add(t)
                    t.add_done_callback(mw._bg_tasks.discard)

        # HierarchyDiff: reset on writes, apply diff on get_hierarchy reads
        if cmd in WRITE_CMDS and cmd not in SCENE_STATE_NEUTRAL_WRITES:
            mw._last_hierarchy_full = None
            # F17: a create/rename may make a previously-absent path resolvable
            if mw._negative_path_cache:
                mw._negative_path_cache.clear()

        # P-416: after successful manage_component, clear stale component cache
        if cmd == "manage_component" and not result.startswith("err"):
            _mc_path = args.get("path", "")
            if _mc_path:
                mw.invalidate_component_cache(_mc_path)

        # Post-call updates
        mw.log_mutation(cmd, args, result)
        mw.cache_components(cmd, args, result)  # P1: populate component cache
        result = mw.categorize_console_errors(result)  # P1: append error hints
        mw.record_read(cmd, args, result)
        mw.clear_write_on_read(cmd, args)
        mw.update_path_cache(cmd, result)
        mw.call_count += 1
        result = await run_post_hooks(cmd, args, result, mw)
        # Track focus for distiller
        mw._track_focus(cmd, args, result)
        # HierarchyDiff: compress repeated get_hierarchy calls
        if cmd == "get_hierarchy" and not _no_distill:
            result = mw._maybe_diff_hierarchy(result)
        # Seed preimage cache from reflect snapshots (after diff)
        mw._seed_preimage(cmd, args, result)
        mw.track_editor_state(cmd, result, args=args)
        if cmd == "set_property" and args.get("prop") and args.get("value") \
                and os.environ.get("UNITY_MCP_REFLECT", "1") == "0":
            result = mw.verify_snapshot(result, prop=args["prop"], value=args["value"])
        result = await mw.maybe_inject_state(send_fn, result, cmd, args)
        # P2: Scene Brief — ensure() first, then inject if ready
        if mw.scene_brief is not None and not mw.scene_brief._injected:
            await mw.scene_brief.ensure(send_fn)
            if mw.scene_brief.should_inject(cmd):
                result = f"--- SCENE CONTEXT ---\n{mw.scene_brief.brief}\n---\n{result}"
                mw.scene_brief.mark_injected()
        result = mw.check_starvation(result)
        result = mw.update_confidence(cmd, args, result)
        result = await mw.maybe_verify_visual(cmd, args, result)

        # Tier C post-call
        if mw.session is not None:
            mw.session.record(cmd, args, result)
        if inferred_tags:
            result += f"\n[INFERRED: {', '.join(inferred_tags)}]"
        if mw.watchdog is not None:
            mw.watchdog.maybe_trigger(cmd, args)
        if mw.recorder is not None:
            # F16: classify on the protocol ok-flag, not a substring scan — a success
            # payload containing "Error" (e.g. get_console logs) must NOT count as a fail.
            mw.recorder.record(cmd, args, result, not protocol_err)
        if mw.speculation is not None:
            result = await mw.speculation.maybe_prefetch(cmd, args, result)

        # Asymmetric Reflection: compare args vs snapshot
        _reflect_on = os.environ.get("UNITY_MCP_REFLECT", "1") != "0"
        if result.startswith("[DEGRADED:"):
            _reflect_on = False
        if cmd in WRITE_CMDS and _reflect_on and not _no_reflect:
            from .reflect import reflect
            mismatch = await reflect(cmd, args, result, send_fn)
            if mismatch is not None:
                safe_msg = mismatch.msg.replace("]", ")")
                result += f"\n[REFLECT: {safe_msg}]"

        # ToolHinter: append hint (after all other markers, skip on DEGRADED)
        if mw.hinter is not None and not result.startswith("[DEGRADED:"):
            try:
                hint = mw.hinter.observe(cmd, args)
                if hint:
                    result += "\n" + hint
            except Exception:
                METRICS.inc("hinter.error")

        # Distill large reads (before prepend so warnings aren't distilled away)
        # Guard: extract [REFLECT:] lines so distiller cannot strip them, then re-append.
        _reflect_lines = [l for l in result.splitlines() if l.startswith("[REFLECT:")]
        if _reflect_lines:
            _reflect_block = "\n".join(_reflect_lines)
            _body = "\n".join(l for l in result.splitlines() if not l.startswith("[REFLECT:"))
            _body = await mw._maybe_distill(cmd, args, _body, no_distill=_no_distill)
            result = "\n".join(filter(None, [_body, _reflect_block]))
        else:
            result = await mw._maybe_distill(cmd, args, result, no_distill=_no_distill)

        # Prepend resolve marker if path was auto-disambiguated
        if resolve_marker:
            result = resolve_marker + "\n" + result

        # Prepend warnings
        fsm_warn = mw.transition(cmd, args)
        warnings = [w for w in (taint_warn, dead_warn, blast_warn, verif_warn, fsm_warn, batch_warn) if w]
        prepend = [w for w in (watchdog_alert, lessons_hint) if w] + warnings
        if prepend:
            result = "\n".join(prepend) + "\n" + result
        return result

    return wrapped
