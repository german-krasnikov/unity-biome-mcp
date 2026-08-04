"""Single source of truth for Haiku output normalization.

Used by ALL consumers of SamplingService output: visual_diff, screenshot_describe,
intent tools, do_intent executor.

Pure functions, no state, no I/O, no async.
"""
from __future__ import annotations

import re
from typing import Literal

OutputKind = Literal["sentinel", "description", "dsl", "verdict"]

REFUSAL_PATTERNS = (
    "i cannot ", "i can't ", "i'm unable", "i won't ",
    "i am unable", "sorry, i",
)
HEDGE_CONJUNCTIONS = (" but i ", " but the ", " however")

# Regex for markdown code fences: ```lang\n...\n``` or ```\n...\n```
_FENCE_RE = re.compile(r"^\s*```(?:\w+)?\s*\n?(.*?)\n?```\s*$", re.DOTALL)
# Conversational prefixes (case-insensitive prefix match).
# NOTE: "i see" is INTENTIONALLY lossy — strips both prefix AND following content.
# Haiku often pads with "I see X" where X is not new info ("I see a button"
# adds nothing to "a button"). Acceptable trade-off; strict-match risks false negatives.
_CONV_PREFIXES = (
    "sure!", "sure,", "of course!", "of course,",
    "looking at this image,", "looking at these images,",
    "based on the image,", "based on the images,",
    "here's", "here is", "i see",
)


def is_refusal(text: str) -> bool:
    """True if text is a Haiku refusal (vs hedged description)."""
    if not text:
        return False
    head = text.strip().lower()[:80]
    if any(c in head for c in HEDGE_CONJUNCTIONS):
        return False
    return any(head.startswith(p) for p in REFUSAL_PATTERNS)


def strip_fences(text: str) -> str:
    """Remove leading/trailing markdown ``` code fences and outer whitespace. Idempotent."""
    if not text:
        return text
    stripped = text.strip()
    m = _FENCE_RE.match(stripped)
    if m:
        return m.group(1).strip()
    return stripped


def strip_conversational(text: str) -> str:
    """Remove leading 'Sure!', 'Of course,', 'Looking at this image,' etc."""
    if not text:
        return text
    stripped = text.strip()
    lower = stripped.lower()
    for prefix in _CONV_PREFIXES:
        if lower.startswith(prefix):
            stripped = stripped[len(prefix):].lstrip(" ,.:")
            break
    return stripped


def normalize(text: str | None, kind: OutputKind = "description") -> tuple[str | None, bool]:
    """Returns (normalized_text, is_refusal).

    - None input → (None, False) — degraded upstream, don't double-wrap
    - Refusal → (None, True) — caller should degrade
    - Otherwise → (cleaned_text, False)

    Per-kind tail behavior:
    - 'verdict': collapse to first non-empty line (PASS/FAIL/short answer)
    - 'sentinel': return first non-empty word/token
    - 'dsl': preserve full text (FAIL: lines must remain)
    - 'description': preserve full cleaned text
    """
    if text is None:
        return None, False
    if is_refusal(text):
        return None, True

    cleaned = strip_fences(text.strip())
    cleaned = strip_conversational(cleaned)
    cleaned = strip_fences(cleaned.strip())  # second pass: fences after conv prefix
    cleaned = cleaned.strip()

    if not cleaned:
        return None, False

    if kind == "verdict":
        first_line = next((ln.strip() for ln in cleaned.splitlines() if ln.strip()), "")
        return first_line or None, False
    if kind == "sentinel":
        first_token = cleaned.split()[0] if cleaned.split() else ""
        return first_token or None, False
    # 'dsl' and 'description': full cleaned
    return cleaned, False
