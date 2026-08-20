"""Post-call hook registry for middleware pipeline."""

import inspect
import logging
from collections.abc import Callable
from typing import Any

log = logging.getLogger(__name__)

PostHookFn = Callable[..., str]
POST_HOOKS: dict[str, list[PostHookFn]] = {}


def register_post(cmd: str) -> Callable[[PostHookFn], PostHookFn]:
    """Decorator: register a post-call hook for cmd (in registration order)."""
    def decorator(fn: PostHookFn) -> PostHookFn:
        POST_HOOKS.setdefault(cmd, []).append(fn)
        return fn
    return decorator


async def run_post_hooks(cmd: str, args: dict, result: str, mw: Any) -> str:
    """Run all registered post-call hooks for cmd in registration order.

    A failing hook is logged and skipped; remaining hooks still execute.
    """
    for hook in POST_HOOKS.get(cmd, []):
        try:
            if inspect.iscoroutinefunction(hook):
                result = await hook(cmd, args, result, mw)
            else:
                result = hook(cmd, args, result, mw)
        except Exception:
            log.exception("post-hook %s for cmd=%r raised", hook.__name__, cmd)
    return result
