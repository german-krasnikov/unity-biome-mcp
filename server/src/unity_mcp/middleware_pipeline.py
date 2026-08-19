"""Full middleware pipeline — wrap_send function."""
import asyncio
import os
import time
from typing import Any, NamedTuple

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


@register_post("get_hierarchy")
def _hook_track_hierarchy_call(cmd: str, args: dict, result: str, mw) -> str:
    mw._last_hierarchy_call = mw.call_count
    return result


_INTERNAL_FLAGS = frozenset(
    {"_no_reflect", "_no_distill", "_explicit_path", "_no_validate", "_no_strip"}
)


class _PipelineCtx(NamedTuple):
    probe_active: bool
    watchdog_alert: str | None
    taint_warn: str | None
    dead_warn: str | None
    blast_warn: str | None
    verif_warn: str | None
    batch_warn: str | None
    lessons_hint: str | None
    inferred_tags: list
    resolve_marker: str
    flags: dict


def _strip_flags(args: dict) -> tuple[dict, dict]:
    """Remove internal marker flags from args before sending to bridge.

    Returns (clean_args, flags_dict).
    """
    flags = {k: bool(args.get(k, False)) for k in _INTERNAL_FLAGS}
    clean = {k: v for k, v in args.items() if k not in _INTERNAL_FLAGS}
    return clean, flags


def _serve_cached_prefetch(pre_cached: str, mw: Any) -> str:
    """Format a prefetch cache hit; record circuit half-open success if needed."""
    if mw.circuit.state == mw.circuit.HALF_OPEN:
        mw.circuit.record_success()
    if pre_cached.startswith("[CACHED:"):
        return pre_cached
    return f"[CACHED]\n{pre_cached}"


def _check_find_objects_cache(cmd: str, args: dict, mw: Any) -> str | None:
    """Return cached result for find_objects when tag/layer/component absent."""
    if (
        cmd == "find_objects"
        and not args.get("tag")
        and not args.get("layer")
        and not args.get("component")
    ):
        return mw.find_from_cache(args.get("name"))
    return None


def _check_prefetch_and_circuit(cmd: str, args: dict, mw: Any) -> str | None:
    """F05 cache-above-circuit read + circuit breaker check.

    Returns an early-exit string, or None to continue the pipeline.
    """
    if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
        pre_cached = mw._prefetch_cache.get(cmd, args)
        if pre_cached is not None:
            return _serve_cached_prefetch(pre_cached, mw)

    if not mw.circuit.allow_request():
        secs = int(mw.circuit.remaining()) + 1
        return f"⚡ Circuit OPEN: Unity unavailable. Auto-retry in {secs}s"

    return None


def _build_ctx(cmd: str, args: dict, mw: Any, flags: dict) -> _PipelineCtx:
    """Build the pipeline context from pre-TCP guard state."""
    return _PipelineCtx(
        probe_active=mw.circuit._probe_in_flight,
        watchdog_alert=mw.watchdog.consume_alert() if mw.watchdog else None,
        taint_warn=mw.check_taint(cmd, args),
        dead_warn=mw.check_dead_write(cmd, args),
        blast_warn=mw.check_blast_radius(cmd, args),
        verif_warn=mw.check_verification_needed(cmd, args),
        batch_warn=(
            mw.scan_batch_conflicts(args.get("commands", "")) if cmd == "batch" else None
        ),
        lessons_hint=mw.lessons.hint_for(cmd, args) if mw.lessons else None,
        inferred_tags=[],
        resolve_marker="",
        flags=flags,
    )


async def _resolve_path_and_validate(
    cmd: str, args: dict, mw: Any, send_fn, flags: dict,
) -> tuple[dict, str] | str:
    """Resolve path, validate schema, check component existence.

    Returns (args, resolve_marker) on success, or a block string for early exit.
    """
    resolve_marker = ""
    if "path" in args and args["path"] and not flags["_explicit_path"]:
        resolved, resolve_marker = await mw.resolve_path_live(args["path"], send_fn)
        if resolved.startswith("__DISAMBIG_BLOCK__"):
            return resolved.split("\n", 1)[1]
        if resolved != args["path"]:
            args = {**args, "path": resolved}

    if mw.schema_guard is not None and not flags["_no_validate"]:
        block = await mw.schema_guard.validate(cmd, args, send_fn)
        if block is not None:
            from .metrics import METRICS
            METRICS.inc("validate.blocked")
            return block

    if cmd == "set_property" and "component" in args:
        comp_warn = mw.check_component_exists(args.get("path", ""), args["component"])
        if comp_warn:
            return comp_warn

    return args, resolve_marker


async def _pre_tcp_guards(
    cmd: str, args: dict, mw: Any, send_fn, ctx: _PipelineCtx
) -> tuple[str, dict, str, list] | str:
    """Pre-TCP guards: retry, pm_block, ro_block, reroute, inference, path resolution.

    Returns (cmd, args, resolve_marker, inferred_tags) on success, or a string for early exit.
    """
    flags = ctx.flags

    # Pre-call checks — guards see ORIGINAL cmd/args (before any rerouting)
    retry_warn = mw.check_retry(cmd, args)
    if retry_warn:
        return retry_warn

    pm_block = mw.check_play_mode_required(cmd)
    if pm_block:
        return pm_block

    ro_block = mw.check_read_only(cmd, args)
    if ro_block:
        raise ToolError(ro_block)

    # Play mode auto-routing — AFTER guards so they see original cmd
    cmd, args = mw.reroute_cmd(cmd, args)

    # Tier C: speculation hit tracking — sees rerouted cmd (what's actually sent)
    if mw.speculation is not None:
        mw.speculation.record_actual_next(cmd)

    # Tier C: argument inference
    inferred_tags: list = []
    if mw.inferrer is not None and mw.session is not None:
        args, inferred_tags = mw.inferrer.infer(cmd, args, mw.session)

    # find_objects cache bypass
    cached = _check_find_objects_cache(cmd, args, mw)
    if cached is not None:
        return cached

    # P1: path resolution, schema guard, component pre-check
    resolved = await _resolve_path_and_validate(cmd, args, mw, send_fn, flags)
    if isinstance(resolved, str):
        return resolved
    args, resolve_marker = resolved

    # PrefetchCache: serve cached reads before TCP round-trip
    if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
        pre_cached = mw._prefetch_cache.get(cmd, args)
        if pre_cached is not None:
            return _serve_cached_prefetch(pre_cached, mw)

    return cmd, args, resolve_marker, inferred_tags


def _on_send_failure(mw, probe_active: bool) -> None:
    """Record circuit failure and release probe slot when active."""
    mw.circuit.record_failure()
    if probe_active:
        mw.circuit.release_probe()


async def _execute_cmd(
    cmd: str, args: dict, send_fn, mw: Any,
    timeout: float, probe_active: bool, no_strip: bool = False,
) -> tuple[str, bool]:
    """Alive check, TCP send, result unwrap, strip defaults, dedup.

    Returns (result, protocol_err).
    """
    # Alive check: quick ping if last success was >30s ago
    if not mw.check_alive():
        try:
            await send_fn("ping", {}, timeout=3.0)
        except Exception:
            _on_send_failure(mw, probe_active)
            raise

    from .metrics import METRICS
    METRICS.inc(f"cmd.{cmd}.calls")
    try:
        with METRICS.timer(f"cmd.{cmd}.ms"):
            result = await send_fn(cmd, args, timeout=timeout)
    except Exception:
        METRICS.inc(f"cmd.{cmd}.fail")
        _on_send_failure(mw, probe_active)
        raise
    mw.circuit.record_success()
    mw._last_success = time.time()

    # Extract string from dict response (when send_fn is raw bridge.send)
    protocol_err = False
    if isinstance(result, dict):
        _receipt = result.get("receipt")
        if _receipt:
            from .changeset_coordinator import get_coordinator
            _c = get_coordinator()
            if _c is not None:
                _c.append(cmd, _receipt)
        result, ok = unwrap_bridge_result(result)
        protocol_err = not ok

    # F08: strip defaults unconditionally for component reads
    if cmd in _STRIP_CMDS and not no_strip:
        result = strip_defaults(result)

    # F16: dedup only GENUINE protocol errors
    if protocol_err:
        result = mw.dedup_error(cmd, result)

    return result, protocol_err


def _maybe_prefetch_background(cmd: str, args: dict, mw: Any, send_fn) -> None:
    """On write: invalidate prefetch path + fire background prefetch task."""
    if cmd not in WRITE_CMDS or cmd in SCENE_STATE_NEUTRAL_WRITES:
        return
    if mw._prefetch_cache is None:
        return
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


def _reset_write_caches(cmd: str, args: dict, result: str, mw: Any) -> None:
    """HierarchyDiff reset + component cache invalidate on writes."""
    if cmd in WRITE_CMDS and cmd not in SCENE_STATE_NEUTRAL_WRITES:
        mw._last_hierarchy_full = None
        if mw._negative_path_cache:
            mw._negative_path_cache.clear()
    if cmd == "manage_component" and not result.startswith("err"):
        mc_path = args.get("path", "")
        if mc_path:
            mw.invalidate_component_cache(mc_path)


def _apply_state_tracking(
    cmd: str, args: dict, result: str, mw: Any, flags: dict,
) -> str:
    """HierarchyDiff, preimage seed, editor state, verify snapshot."""
    if cmd == "get_hierarchy" and not flags["_no_distill"]:
        result = mw._maybe_diff_hierarchy(result)
    mw._seed_preimage(cmd, args, result)
    mw.track_editor_state(cmd, result, args=args)
    if (
        cmd == "set_property" and args.get("prop") and args.get("value")
        and os.environ.get("UNITY_MCP_REFLECT", "1") == "0"
    ):
        result = mw.verify_snapshot(result, prop=args["prop"], value=args["value"])
    return result


async def _apply_scene_brief(cmd: str, result: str, mw: Any, send_fn) -> str:
    """Inject scene brief on first eligible call."""
    if mw.scene_brief is not None and not mw.scene_brief._injected:
        await mw.scene_brief.ensure(send_fn)
        if mw.scene_brief.should_inject(cmd):
            result = f"--- SCENE CONTEXT ---\n{mw.scene_brief.brief}\n---\n{result}"
            mw.scene_brief.mark_injected()
    return result


async def _apply_reflection_and_hinter(
    cmd: str, args: dict, result: str, mw: Any, send_fn, no_reflect: bool,
) -> str:
    """Asymmetric reflection + ToolHinter hint append."""
    _reflect_on = os.environ.get("UNITY_MCP_REFLECT", "1") != "0"
    if result.startswith("[DEGRADED:"):
        _reflect_on = False
    if cmd in WRITE_CMDS and _reflect_on and not no_reflect:
        from .reflect import reflect
        mismatch = await reflect(cmd, args, result, send_fn)
        if mismatch is not None:
            result += f"\n[REFLECT: {mismatch.msg.replace(']', ')')}]"
    if mw.hinter is not None and not result.startswith("[DEGRADED:"):
        try:
            hint = mw.hinter.observe(cmd, args)
            if hint:
                result += "\n" + hint
        except Exception:
            from .metrics import METRICS
            METRICS.inc("hinter.error")
    return result


async def _apply_distill_and_finalize(
    cmd: str, args: dict, result: str, mw: Any, ctx: _PipelineCtx,
) -> str:
    """Distill with REFLECT guard, prepend resolve marker and warnings."""
    flags = ctx.flags
    _reflect_lines = [l for l in result.splitlines() if l.startswith("[REFLECT:")]
    if _reflect_lines:
        _body = "\n".join(l for l in result.splitlines() if not l.startswith("[REFLECT:"))
        _body = await mw._maybe_distill(cmd, args, _body, no_distill=flags["_no_distill"])
        result = "\n".join(filter(None, [_body, "\n".join(_reflect_lines)]))
    else:
        result = await mw._maybe_distill(cmd, args, result, no_distill=flags["_no_distill"])
    if ctx.resolve_marker:
        result = ctx.resolve_marker + "\n" + result
    fsm_warn = mw.transition(cmd, args)
    warnings = [
        w for w in (
            ctx.taint_warn, ctx.dead_warn, ctx.blast_warn,
            ctx.verif_warn, fsm_warn, ctx.batch_warn,
        ) if w
    ]
    prepend = [w for w in (ctx.watchdog_alert, ctx.lessons_hint) if w] + warnings
    if prepend:
        result = "\n".join(prepend) + "\n" + result
    return result


async def _post_process(
    cmd: str, args: dict, result: str,
    mw: Any, send_fn,
    ctx: _PipelineCtx, protocol_err: bool,
) -> str:
    """Post-TCP hooks, hints, metrics, warnings prepend.

    Returns final result string.
    """
    flags = ctx.flags
    inferred_tags = ctx.inferred_tags

    _maybe_prefetch_background(cmd, args, mw, send_fn)
    _reset_write_caches(cmd, args, result, mw)

    # Post-call updates
    mw.log_mutation(cmd, args, result)
    mw.cache_components(cmd, args, result)
    result = mw.categorize_console_errors(result)
    mw.record_read(cmd, args, result)
    mw.clear_write_on_read(cmd, args)
    mw.update_path_cache(cmd, result)
    mw.call_count += 1
    result = await run_post_hooks(cmd, args, result, mw)
    mw._track_focus(cmd, args, result)

    result = _apply_state_tracking(cmd, args, result, mw, flags)
    result = await mw.maybe_inject_state(send_fn, result, cmd, args)
    result = await _apply_scene_brief(cmd, result, mw, send_fn)
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
        mw.recorder.record(cmd, args, result, not protocol_err)
    if mw.speculation is not None:
        result = await mw.speculation.maybe_prefetch(cmd, args, result)

    result = await _apply_reflection_and_hinter(
        cmd, args, result, mw, send_fn, flags["_no_reflect"]
    )
    return await _apply_distill_and_finalize(cmd, args, result, mw, ctx)


def wrap_send(send_fn, mw: Any = None):
    """Return a wrapped _send that runs all middleware checks."""
    from .middleware import Middleware as _Middleware
    if mw is None:
        mw = _Middleware()

    async def wrapped(cmd: str, args: dict, timeout: float = 0) -> str:
        # ToolHinter: adoption check at call start
        if mw.hinter is not None:
            mw.hinter.note_adoption(cmd)

        args, flags = _strip_flags(args)

        # Alias resolution: $name → cached pipe value (Hook 1)
        if mw._alias_cache:
            from .middleware_alias import resolve_aliases_in_args
            args = resolve_aliases_in_args(args, mw._alias_cache)

        early = _check_prefetch_and_circuit(cmd, args, mw)
        if early is not None:
            return early

        ctx = _build_ctx(cmd, args, mw, flags)
        probe_active = ctx.probe_active

        pre = await _pre_tcp_guards(cmd, args, mw, send_fn, ctx)
        if isinstance(pre, str):
            if probe_active:
                mw.circuit.record_success()
            return pre
        cmd, args, resolve_marker, inferred_tags = pre

        result, protocol_err = await _execute_cmd(
            cmd, args, send_fn, mw, timeout, probe_active, no_strip=flags["_no_strip"]
        )

        ctx = ctx._replace(resolve_marker=resolve_marker, inferred_tags=inferred_tags)
        return await _post_process(cmd, args, result, mw, send_fn, ctx, protocol_err)

    return wrapped
