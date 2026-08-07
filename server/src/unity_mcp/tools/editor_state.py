"""Utility: parse C# EditorStateHelper.GetState() key:value responses.

C# format: "playing:True\\npaused:False\\ncompiling:False\\nscene:SceneName"
Use these helpers instead of inline substring checks across the codebase.
"""
from __future__ import annotations


def parse_editor_field(state: str | None, field: str) -> str | None:
    """Extract a single field value from C# editor state response.

    Returns the raw value string, or None if the field is absent.
    Uses partition so colons inside values (e.g. scene paths) are preserved.
    Field lookup is case-insensitive.
    """
    for line in (state or "").splitlines():
        k, sep, v = line.partition(":")
        if sep and k.strip().lower() == field.lower():
            return v.strip()
    return None


def is_play_mode(state: str | None) -> bool:
    """Return True if editor is in Play Mode (playing:True in state response)."""
    return (parse_editor_field(state, "playing") or "").lower() == "true"


def is_compiling(state: str | None) -> bool:
    """Return True if editor is compiling (compiling:True in state response)."""
    return (parse_editor_field(state, "compiling") or "").lower() == "true"


def is_paused(state: str | None) -> bool:
    """Return True if editor is paused (paused:True in state response)."""
    return (parse_editor_field(state, "paused") or "").lower() == "true"
