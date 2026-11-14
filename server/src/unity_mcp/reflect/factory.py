"""Factory functions for common reflect rule patterns.

All rules are fail-open: return None on unexpected response format.
"""
import inspect

from . import Mismatch, ReflectFn, register_rule

_ERROR_TOKENS = ("Error:", "Failed", "err:", "Exception")


def _has_error(response: str) -> bool:
    return any(t in response for t in _ERROR_TOKENS)


def _make_no_error_fn(cmd: str) -> ReflectFn:
    """Create an unregistered no-error check function (sync — no I/O needed)."""
    def _rule(args: dict, response: str, send_fn) -> Mismatch | None:  # S7503: sync
        if _has_error(response):
            return Mismatch(f"{cmd}: error in response: {response[:80]!r}")
        return None
    return _rule


def make_ok_rule(cmd: str, ok_tokens: tuple[str, ...], *, strict: bool = False) -> None:
    """Register rule that checks one of ok_tokens in response (case-insensitive).

    Fail-open (skip on an error token) unless strict=True, in which case an
    error response is just another response that lacks the expected token —
    SV5 catalog EXPECT rows (e.g. wire_event, delete_object) where an error
    means the mutation itself failed, not merely "can't verify".
    """
    def _rule(args: dict, response: str, send_fn) -> Mismatch | None:  # S7503: sync
        if not strict and _has_error(response):
            return None
        low = response.lower()
        if any(t.lower() in low for t in ok_tokens):
            return None
        return Mismatch(f"{cmd}: expected one of {ok_tokens!r} in response")
    register_rule(cmd)(_rule)


def make_bool_token_rule(cmd: str, argname: str, true_token: str, false_token: str) -> None:
    """Register a rule whose expected token depends on a bool arg (e.g.
    region_clear: dry_run=true expects "DRY", else "DELETED"). No fail-open —
    an error response simply lacks the expected token like any other miss.
    """
    def _rule(args: dict, response: str, send_fn) -> Mismatch | None:  # S7503: sync
        is_true = str(args.get(argname, "false")).lower() == "true"
        token = true_token if is_true else false_token
        if token not in response:
            return Mismatch(f"{cmd}: expected '{token}' in response")
        return None
    register_rule(cmd)(_rule)


def make_arg_echo_rule(cmd: str, argnames: tuple[str, ...], *, strict: bool = False) -> None:
    """Register a rule where each supplied arg must echo back as "<name>="
    in the response (e.g. set_material: shader given -> "shader=" expected).
    An arg that was not supplied is not checked. Fail-open by default.
    """
    def _rule(args: dict, response: str, send_fn) -> Mismatch | None:  # S7503: sync
        if not strict and _has_error(response):
            return None
        for name in argnames:
            if args.get(name) and f"{name}=" not in response:
                return Mismatch(f"{cmd}: expected '{name}=' token in response")
        return None
    register_rule(cmd)(_rule)


def make_no_error_rule(cmd: str) -> None:
    """Register rule that returns Mismatch only if error token in response."""
    register_rule(cmd)(_make_no_error_fn(cmd))


def make_action_guard(
    _cmd: str, read_actions: frozenset[str], inner: ReflectFn  # S1172: _cmd unused
) -> ReflectFn:
    """Wrap inner rule: skip (return None) when action in read_actions.
    Does NOT register — caller must call register_rule separately.
    Supports both sync and async inner rules.
    """
    async def _guarded(args: dict, response: str, send_fn) -> Mismatch | None:
        if args.get("action", "") in read_actions:
            return None
        raw = inner(args, response, send_fn)
        return await raw if inspect.isawaitable(raw) else raw
    return _guarded


def make_action_guarded_no_error_rule(cmd: str, read_actions: frozenset[str]) -> None:
    """Register an action-aware no-error rule (skips on read actions)."""
    inner = _make_no_error_fn(cmd)
    register_rule(cmd)(make_action_guard(cmd, read_actions, inner))
