"""Factory functions for common reflect rule patterns.

All rules are fail-open: return None on unexpected response format.
"""
from . import Mismatch, ReflectFn, register_rule

_ERROR_TOKENS = ("Error:", "Failed", "err:", "Exception")


def _has_error(response: str) -> bool:
    return any(t in response for t in _ERROR_TOKENS)


def _make_no_error_fn(cmd: str) -> ReflectFn:
    """Create an unregistered no-error check function."""
    async def _rule(args: dict, response: str, send_fn) -> Mismatch | None:
        if _has_error(response):
            return Mismatch(f"{cmd}: error in response: {response[:80]!r}")
        return None
    return _rule


def make_ok_rule(cmd: str, ok_tokens: tuple[str, ...]) -> None:
    """Register rule that checks one of ok_tokens in response (case-insensitive).
    Fail-open when response contains an error token.
    """
    async def _rule(args: dict, response: str, send_fn) -> Mismatch | None:
        if _has_error(response):
            return None
        low = response.lower()
        if any(t.lower() in low for t in ok_tokens):
            return None
        return Mismatch(f"{cmd}: expected one of {ok_tokens!r} in response")
    register_rule(cmd)(_rule)


def make_no_error_rule(cmd: str) -> None:
    """Register rule that returns Mismatch only if error token in response."""
    register_rule(cmd)(_make_no_error_fn(cmd))


def make_action_guard(
    cmd: str, read_actions: frozenset[str], inner: ReflectFn
) -> ReflectFn:
    """Wrap inner rule: skip (return None) when action in read_actions.
    Does NOT register — caller must call register_rule separately.
    """
    async def _guarded(args: dict, response: str, send_fn) -> Mismatch | None:
        if args.get("action", "") in read_actions:
            return None
        return await inner(args, response, send_fn)
    return _guarded


def make_action_guarded_no_error_rule(cmd: str, read_actions: frozenset[str]) -> None:
    """Register an action-aware no-error rule (skips on read actions)."""
    inner = _make_no_error_fn(cmd)
    register_rule(cmd)(make_action_guard(cmd, read_actions, inner))
