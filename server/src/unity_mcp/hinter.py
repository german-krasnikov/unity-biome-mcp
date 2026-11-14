"""ToolHinter — post-call discoverability hints for suboptimal tool-use patterns.

Appends a single [HINT: ... — saves ~Ntok] line when a known anti-pattern is detected.
Self-suppressing after 2 consecutive ignores. Cooldown: 8 calls between emits per pattern.
"""
from collections import deque

from .decisions.hinter_facts import hint_adoption, hint_gate, hint_pattern
from .hinter_patterns import (
    _PATTERNS,
    KNOWN_PATTERN_OUTCOMES,
    Call,
    Pattern,
    _key,
    reflect_on,
    window_facts,
)
from .middleware_types import is_write

# Re-exports for backward compatibility
__all__ = ["ToolHinter", "Call", "Pattern"]


class ToolHinter:
    def __init__(self, enabled: bool = True):
        self.enabled = enabled
        self._recent: deque[Call] = deque(maxlen=16)
        self._emitted_at: dict[str, int] = {}
        self._ignored: dict[str, int] = {}
        self._suppressed: set[str] = set()
        self._call_idx: int = 0
        self._last_hint_pattern: str | None = None
        self._patterns: list[Pattern] = list(_PATTERNS)

    def _check_adoption(self, cmd: str) -> None:
        """Check if previous hint was adopted or ignored."""
        if not self._last_hint_pattern:
            return
        pid = self._last_hint_pattern
        pat = next((p for p in self._patterns if p.id == pid), None)
        if pat is None:
            return

        from .metrics import METRICS
        verdict = hint_adoption(
            is_suggested=bool(pat.suggested_cmd) and cmd == pat.suggested_cmd,
            is_trigger=cmd == pat.trigger_cmd,
            ignored_before=self._ignored.get(pid, 0),
        )
        if verdict == "adopted":
            METRICS.inc(f"hint.adopted.{pid}")
            self._ignored[pid] = 0
            self._last_hint_pattern = None
        elif verdict in ("ignored", "suppress"):
            METRICS.inc(f"hint.ignored.{pid}")
            self._ignored[pid] = self._ignored.get(pid, 0) + 1
            if verdict == "suppress":
                self._suppressed.add(pid)
                METRICS.inc(f"hint.suppressed.{pid}")
            self._last_hint_pattern = None

    def _open(self, pattern_id: str) -> bool:
        calls_since_emit = self._call_idx - self._emitted_at.get(pattern_id, -999)
        return hint_gate(
            enabled=True,  # observe() already returned early when self.enabled is False
            suppressed=pattern_id in self._suppressed,
            calls_since_emit=calls_since_emit,
        ) == "open"

    def _known_pattern_hint(self, call: Call) -> str | None:
        """The one hint of the 6 known patterns, or None — hintPattern DECISION, first match wins."""
        from .metrics import METRICS
        try:
            facts = window_facts(self._recent, call)
            outcome = hint_pattern(
                cmd=call.cmd,
                reflect_on=reflect_on(),
                get_component8=facts["get_component8"],
                same_target_writes6=facts["same_target_writes6"],
                find_objects4=facts["find_objects4"],
                screenshots_since_write8=facts["screenshots_since_write8"],
                recompile6=facts["recompile6"],
                get_console6=facts["get_console6"],
                same_target_set_property3=facts["same_target_set_property3"],
                inspect_loop_open=self._open("inspect-loop"),
                batch_writes_open=self._open("batch-writes"),
                find_then_read_open=self._open("find-then-read"),
                screenshot_spam_open=self._open("screenshot-spam"),
                console_poll_open=self._open("console-poll"),
                redundant_verify_open=self._open("redundant-verify-read"),
            )
        except Exception:
            METRICS.inc("hinter.error")
            return None

        if outcome == "none":
            return None
        pid = next(k for k, v in KNOWN_PATTERN_OUTCOMES.items() if v == outcome)
        pat = next((p for p in self._patterns if p.id == pid), None)
        if pat is None:
            return None
        self._emitted_at[pid] = self._call_idx
        self._last_hint_pattern = pid
        METRICS.inc(f"hint.emitted.{pid}")
        return pat.hint

    def _custom_pattern_hint(self, call: Call) -> str | None:
        """Any pattern registered beyond the known 6 (test/extension seam) — native gate + predicate."""
        from .metrics import METRICS
        for pat in self._patterns:
            if pat.id in KNOWN_PATTERN_OUTCOMES or not self._open(pat.id):
                continue
            try:
                if pat.predicate(self._recent, call):
                    self._emitted_at[pat.id] = self._call_idx
                    self._last_hint_pattern = pat.id
                    METRICS.inc(f"hint.emitted.{pat.id}")
                    return pat.hint
            except Exception:
                METRICS.inc("hinter.error")
        return None

    def observe(self, cmd: str, args: dict) -> str | None:
        if not self.enabled:
            return None

        self._check_adoption(cmd)

        call = Call(cmd=cmd, key=_key(cmd, args), mutates=is_write(cmd, args))

        hint_result = self._known_pattern_hint(call)
        if hint_result is None:
            hint_result = self._custom_pattern_hint(call)

        self._recent.append(call)
        self._call_idx += 1
        return hint_result

    def note_adoption(self, cmd: str) -> None:
        """Called at START of wrap_send to track adoption before state changes."""
        self._check_adoption(cmd)
