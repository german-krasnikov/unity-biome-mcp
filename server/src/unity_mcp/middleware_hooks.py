"""Post-call hook registry for middleware pipeline."""

import inspect
from collections.abc import Callable
from typing import Any

PostHookFn = Callable[..., str]
POST_HOOKS: dict[str, list[PostHookFn]] = {}


def register_post(cmd: str) -> Callable[[PostHookFn], PostHookFn]:
    """Decorator: register a post-call hook for cmd (in registration order)."""
    def decorator(fn: PostHookFn) -> PostHookFn:
        POST_HOOKS.setdefault(cmd, []).append(fn)
        return fn
    return decorator


async def run_post_hooks(cmd: str, args: dict, result: str, mw: Any) -> str:
    """Run all registered post-call hooks for cmd in registration order."""
    for hook in POST_HOOKS.get(cmd, []):
        if inspect.iscoroutinefunction(hook):
            result = await hook(cmd, args, result, mw)
        else:
            result = hook(cmd, args, result, mw)
    return result
