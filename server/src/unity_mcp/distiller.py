"""Intent-aware response distiller. Heuristic-first filter on large reads.

Saves input tokens by trimming responses to focus paths. Optional async Haiku
fallback validates substring-of-original to prevent hallucinations.

Pure stateless transform — caller owns cache, focus tracking, scheduling.
"""

import re
from dataclasses import dataclass


@dataclass
class DistillResult:
    text: str
    original_size: int
    distilled_size: int
    method: str  # "skip" | "heuristic" | "haiku" | "passthrough"


_PATH_RE = re.compile(r"(/\S+)")
_SECTION_RE = re.compile(r"^---\s*(/\S+)\s*---\s*$")
_SCENE_HEADER_RE = re.compile(r"^\[.+\]$")


# Commands too small or destructive to distill
_SKIP_CMDS = frozenset({"set_property", "ping", "batch", "wire_event", "set_active",
                         "create_object", "delete_object", "manage_component", "recompile",
                         "execute_code"})


class ResponseDistiller:
    """Pure transform: text + focus → trimmed text. Stateless."""

    def __init__(self,
                 sampling=None,
                 min_size: int = 1500,
                 haiku_cmds: frozenset = frozenset({"get_hierarchy", "inspect", "scan_scene", "get_object_detail"})):
        self._sampling = sampling
        self._min_size = min_size
        self._haiku_cmds = haiku_cmds

    def distill_heuristic(self, cmd: str, text: str, focus: tuple[str, ...]) -> DistillResult:
        """Synchronous heuristic filter."""
        original_size = len(text)

        # Skip checks
        if cmd in _SKIP_CMDS or original_size < self._min_size or not focus:
            return DistillResult(text, original_size, original_size, "skip")

        # Section-mode for inspect/scan_scene (--- /path --- to next ---)
        if cmd in {"inspect", "scan_scene", "get_object_detail"}:
            distilled = self._distill_sections(text, focus)
        else:
            # Line-mode for hierarchy
            distilled = self._distill_lines(text, focus)

        distilled_size = len(distilled)

        # If kept >= 90% — no value, return original
        if distilled_size >= original_size * 0.9:
            return DistillResult(text, original_size, original_size, "passthrough")

        return DistillResult(distilled, original_size, distilled_size, "heuristic")

    def _distill_lines(self, text: str, focus: tuple[str, ...]) -> str:
        """Line-mode distillation for hierarchy. Keep lines whose path matches focus + ±1 sibling."""
        lines = text.splitlines()
        keep = [False] * len(lines)
        for i, line in enumerate(lines):
            if _SCENE_HEADER_RE.match(line.strip()):
                keep[i] = True
                continue
            paths = _PATH_RE.findall(line)
            if paths and any(self._paths_overlap(p, f) for p in paths for f in focus):
                keep[i] = True
                if i > 0:
                    keep[i - 1] = True
                if i + 1 < len(lines):
                    keep[i + 1] = True

        # Always keep first line if it looks like header
        if lines and (not _PATH_RE.search(lines[0]) or "Total" in lines[0] or "Scene" in lines[0]):
            keep[0] = True

        kept_lines = [lines[i] for i in range(len(lines)) if keep[i]]
        hidden_count = len(lines) - len(kept_lines)
        if hidden_count > 0:
            kept_lines.append(f"[... +{hidden_count} hidden]")
        return "\n".join(kept_lines)

    def _distill_sections(self, text: str, focus: tuple[str, ...]) -> str:
        """Section-mode for inspect output. Keep `--- /path ---` blocks matching focus."""
        current_section: list[str] = []
        current_path: str | None = None
        kept_sections: list[str] = []
        skipped_count = 0
        preamble: list[str] = []  # pre-header lines (e.g. "Objects: 12")

        for line in text.splitlines():
            sec_match = _SECTION_RE.match(line)
            if sec_match:
                # Flush previous — first flush becomes preamble
                if current_path is None and current_section:
                    preamble = current_section
                elif current_path is not None:
                    if any(self._paths_overlap(current_path, f) for f in focus):
                        kept_sections.append("\n".join(current_section))
                    else:
                        skipped_count += 1
                current_path = sec_match.group(1)
                current_section = [line]
            else:
                current_section.append(line)

        # Last section
        if current_path is None and current_section:
            preamble = current_section
        elif current_path is not None:
            if any(self._paths_overlap(current_path, f) for f in focus):
                kept_sections.append("\n".join(current_section))
            else:
                skipped_count += 1

        parts = []
        if preamble:
            parts.append("\n".join(preamble))
        parts.extend(kept_sections)
        result = "\n".join(parts)
        if skipped_count > 0:
            result += f"\n[... +{skipped_count} sections hidden]"
        return result

    @staticmethod
    def _paths_overlap(p1: str, p2: str) -> bool:
        """True if p1==p2, or one is direct ancestor/descendant of the other (path-boundary aware)."""
        if p1 == p2:
            return True
        p1n = p1.rstrip("/") + "/"
        p2n = p2.rstrip("/") + "/"
        return p1.startswith(p2n) or p2.startswith(p1n)

    @staticmethod
    def extract_paths(text: str) -> set[str]:
        """Extract all path-like strings from text for validation."""
        return set(_PATH_RE.findall(text))

    @staticmethod
    def validate_distilled(original: str, distilled: str) -> bool:
        """Check distilled is well-formed and all its paths are in original.

        Prevents Haiku hallucinations.
        """
        if len(distilled) >= len(original):
            return False
        # All paths in distilled must be substrings of original
        orig_paths = ResponseDistiller.extract_paths(original)
        dist_paths = ResponseDistiller.extract_paths(distilled)
        for p in dist_paths:
            if p not in orig_paths:
                return False
        # Well-formed: balanced brackets if any
        return distilled.count("[") == distilled.count("]")

    async def distill_haiku(self, cmd: str, text: str, focus: tuple[str, ...]) -> DistillResult | None:
        """Async Haiku-based distillation. Returns None if sampling disabled or validation fails."""
        if self._sampling is None or cmd not in self._haiku_cmds:
            return None

        original_size = len(text)
        focus_str = ", ".join(focus[:5]) if focus else "main scene"

        prompt = (
            f"Extract from this Unity {cmd} output ONLY items related to: {focus_str}\n"
            f"Format: same as input. Drop unrelated items. Mark drops as `... +N hidden`.\n"
            f"DO NOT invent or modify paths.\n\n"
            f"Input:\n{text[:4000]}"  # cap input
        )

        try:
            result = await self._sampling.generate(prompt, feature="distiller")
        except Exception:
            return None

        if not result:
            return None

        if not self.validate_distilled(text, result):
            return None

        return DistillResult(result, original_size, len(result), "haiku")
