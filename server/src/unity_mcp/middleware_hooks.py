"""Post-call hook registry for middleware pipeline."""
from __future__ import annotations
import asyncio
from typing import TYPE_CHECKING, Callable

if TYPE_CHECKING:
    from .middleware import Middleware

PostHookFn = Callable[..., str]
POST_HOOKS: dict[str, list[PostHookFn]] = {}


def register_post(cmd: str) -> Callable[[PostHookFn], PostHookFn]:
    """Decorator: register a post-call hook for cmd (in registration order)."""
    def decorator(fn: PostHookFn) -> PostHookFn:
        POST_HOOKS.setdefault(cmd, []).append(fn)
        return fn
    return decorator


async def run_post_hooks(cmd: str, args: dict, result: str, mw: "Middleware") -> str:
    """Run all registered post-call hooks for cmd in registration order."""
    for hook in POST_HOOKS.get(cmd, []):
        if asyncio.iscoroutinefunction(hook):
            result = await hook(cmd, args, result, mw)
        else:
            result = hook(cmd, args, result, mw)
    return result
