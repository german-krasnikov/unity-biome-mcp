"""Async/background operations for Middleware (mixin)."""
import asyncio
import json
import os
from typing import Callable, Awaitable


class MiddlewareAsyncMixin:
    """Async background ops: prefetch, distill, state injection. Attrs in Middleware.__init__."""

    # ── Feature 4: Periodic State Injection ───────────────────────────────

    async def maybe_inject_state(
        self,
        send_fn: Callable[..., Awaitable[str]],
        result: str,
        cmd: str = "",
        args: dict | None = None,
    ) -> str:
        from .middleware_types import is_write
        if os.environ.get("UNITY_MCP_AUTO_STATE", "1") == "0":
            return result
        if cmd and not is_write(cmd, args):
            return result
        if self.call_count % 10 == 0 and (self.call_count - self._last_hierarchy_call) > 5:
            try:
                hierarchy = await send_fn("get_hierarchy", {"summary": "true"})
                self._last_hierarchy_call = self.call_count
                return result + f"\n--- AUTO STATE (call #{self.call_count}) ---\n{hierarchy}"
            except Exception:
                pass
        return result

    # ── Feature: Visual Verification via MCP Sampling ────────────────────────────

    async def maybe_verify_visual(self, cmd: str, args: dict, result: str) -> str:
        from .middleware_types import is_write
        if self.sampling is None or not self.sampling.enabled:
            return result
        if not is_write(cmd, args):
            return result
        if self.confidence >= 0.5:
            return result
        prompt = f"Verify that '{cmd}' succeeded. Args: {args}. Result: {result[:200]}"
        verdict = await self.sampling.verify_visual(prompt)
        if verdict:
            result = result + f"\n[VERIFY: {verdict}]"
        return result

    # ── Item 1: PrefetchCache ──────────────────────────────────────────────────

    async def _background_prefetch(self, cmd: str, args: dict, send_fn) -> None:
        """Fire a predicted read in background, populate cache on success."""
        try:
            result = await send_fn(cmd, args)
            text = result.get("data", "") if isinstance(result, dict) else str(result)
            if text and self._prefetch_cache is not None:
                self._prefetch_cache.put(cmd, args, text)
        except Exception:
            from .metrics import METRICS
            METRICS.inc("prefetch.error")

    # ── Cycle 5d: Distiller ───────────────────────────────────────────────────

    async def _maybe_distill(self, cmd: str, args: dict, result: str, no_distill: bool = False) -> str:
        """Apply heuristic distillation + Haiku background cache (Cycle 5d)."""
        if not self._distiller_enabled or no_distill:
            return result

        if self._distiller is None:
            from .distiller import ResponseDistiller
            sampling = None
            if os.environ.get("UNITY_MCP_DISTILL_HAIKU", "0") == "1":
                try:
                    from .sampling import SamplingService
                    svc = SamplingService()
                    if svc.enabled:
                        sampling = svc
                except Exception:
                    sampling = None
            self._distiller = ResponseDistiller(sampling=sampling)

        focus = tuple(self._recent_focus)

        # Check Haiku cache first (cheap key)
        sig_args = {k: v for k, v in sorted(args.items()) if not k.startswith("_")}
        cache_key = (cmd, json.dumps(sig_args, sort_keys=True), focus)
        cached = self._distill_cache.get(cache_key)
        if cached is not None:
            self._distill_cache.move_to_end(cache_key)
            return f"{cached}\n[DISTILLED haiku-cached; full: re-call with full=true]"

        res = self._distiller.distill_heuristic(cmd, result, focus)

        # Schedule background Haiku for next call if heuristic was weak
        if (
            self._distiller._sampling is not None
            and res.method in ("passthrough", "skip")
            and len(result) > 1500
            and bool(focus)
            and cmd in self._distiller._haiku_cmds
            and cache_key not in self._haiku_in_flight
        ):
            self._haiku_in_flight.add(cache_key)
            t = asyncio.create_task(self._haiku_to_cache(cmd, result, focus, cache_key))
            self._bg_tasks.add(t)
            t.add_done_callback(self._bg_tasks.discard)

        if res.method in ("skip", "passthrough"):
            return result
        return (
            f"{res.text}\n"
            f"[DISTILLED {res.method} {res.original_size}→{res.distilled_size} chars; "
            f"full: re-call with full=true]"
        )

    async def _haiku_to_cache(self, cmd: str, text: str, focus: tuple, cache_key: tuple) -> None:
        """Background Haiku distillation. Fire-and-forget. Populates _distill_cache."""
        try:
            result = await self._distiller.distill_haiku(cmd, text, focus)
            if result is not None:
                self._distill_cache[cache_key] = result.text
                if len(self._distill_cache) > self._MAX_DISTILL_CACHE:
                    self._distill_cache.popitem(last=False)
        except Exception:
            from .metrics import METRICS
            METRICS.inc("distill.haiku_error")
        finally:
            self._haiku_in_flight.discard(cache_key)
