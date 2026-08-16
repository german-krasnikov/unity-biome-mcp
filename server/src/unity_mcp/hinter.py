"""ToolHinter — post-call discoverability hints for suboptimal tool-use patterns.

Appends a single [HINT: ... — saves ~Ntok] line when a known anti-pattern is detected.
Self-suppressing after 2 consecutive ignores. Cooldown: 8 calls between emits per pattern.
"""
from collections import deque

from .hinter_patterns import _PATTERNS, Call, Pattern, _key
from .middleware_types import is_write

# Re-exports for backward compatibility
__all__ = ["ToolHinter", "Call", "Pattern"]


class ToolHinter:
    _COOLDOWN = 8

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
        # Adoption: called the suggested cmd
        if pat.suggested_cmd and cmd == pat.suggested_cmd:
            METRICS.inc(f"hint.adopted.{pid}")
            self._ignored[pid] = 0
            self._last_hint_pattern = None
            return

        # Ignore: repeated the suboptimal pattern (same triggering cmd)
        if cmd == pat.trigger_cmd:
            METRICS.inc(f"hint.ignored.{pid}")
            self._ignored[pid] = self._ignored.get(pid, 0) + 1
            if self._ignored[pid] >= 2:
                self._suppressed.add(pid)
                from .metrics import METRICS as M
                M.inc(f"hint.suppressed.{pid}")
            self._last_hint_pattern = None

    def observe(self, cmd: str, args: dict) -> str | None:
        if not self.enabled:
            return None

        from .metrics import METRICS
        self._check_adoption(cmd)

        call = Call(cmd=cmd, key=_key(cmd, args), mutates=is_write(cmd, args))

        hint_result: str | None = None
        for pat in self._patterns:
            if pat.id in self._suppressed:
                continue
            last = self._emitted_at.get(pat.id, -999)
            if self._call_idx - last < self._COOLDOWN:
                continue
            try:
                if pat.predicate(self._recent, call):
                    hint_result = pat.hint
                    self._emitted_at[pat.id] = self._call_idx
                    self._last_hint_pattern = pat.id
                    METRICS.inc(f"hint.emitted.{pat.id}")
                    break  # one hint per call
            except Exception:
                METRICS.inc("hinter.error")

        self._recent.append(call)
        self._call_idx += 1
        return hint_result

    def note_adoption(self, cmd: str) -> None:
        """Called at START of wrap_send to track adoption before state changes."""
        self._check_adoption(cmd)
