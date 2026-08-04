"""Keyword regex router — deterministic 80% of ask() questions."""
import re

from .plans import CANONICAL_PLANS, ToolPlan

# Mutating verbs that make a question a command, not a query
_MUTATING_RE = re.compile(
    r"^\s*(delete|remove|set|add|create|move|change|update|destroy|disable|enable|apply|fix|reset)\b",
    re.IGNORECASE,
)

# Unity-specific nouns that indicate scene-related questions
_UNITY_NOUNS_RE = re.compile(
    r"\b(scenes?|objects?|gameobjects?|components?|references?|refs?|links?|errors?|warnings?|compil(e|ation|ing)|"
    r"play\s*mode|playing|paused|dirty|hierarchy|prefabs?|materials?|shaders?|scripts?|assets?|"
    r"console|issues?|problems?|health|active|enabled|tags?|"
    r"transforms?|positions?|waypoints?|paths?|regions?|nodes?|bounds?|coords?|coordinates?|"
    r"locations?|colliders?|renderers?|lights?|cameras?|meshes?|terrains?|particles?|rigidbod(y|ies)|"
    r"children|parent)\b",
    re.IGNORECASE,
)

# Pattern → canonical plan key (order matters: SPATIAL before OBJECT)
_PATTERNS: list[tuple[re.Pattern, str]] = [
    (re.compile(r"\bbroken\b.*\b(refs?|references?|links?)|\b(refs?|references?|links?).*\bbroken\b", re.I), "BROKEN_REFS"),
    (re.compile(r"\b(errors?|wrong|issues?|problems?)\b.*\b(scene|console)|\b(scene|console).*\b(errors?|issues?|problems?)\b", re.I), "SCENE_HEALTH"),
    (re.compile(r"\bhow many\b|\bcount of\b.*\b(active|enabled|tag|comp)\b", re.I), "COUNT_ACTIVE"),
    (re.compile(r"\bplay(ing|mode| mode)\b|\bpaused\b|\bdirty\b", re.I), "EDITOR_STATE"),
    (re.compile(r"\bcompil(e|ation|ing)\b", re.I), "COMPILE_ERRORS"),
    (re.compile(r"\b(positions?|locations?|coords?|coordinates?|where\s+(is|are))\b", re.I), "SCENE_QUERY"),
    (re.compile(r"\b(what|which|where|list|show|find|get)\b.*\b(objects?|gameobjects?|transforms?|nodes?|children|parent)\b", re.I), "SCENE_QUERY"),
]


def is_mutating(question: str) -> bool:
    return bool(_MUTATING_RE.match(question))


def route(question: str) -> ToolPlan | None:
    """
    Return ToolPlan for question, or None if out-of-scope / no match.
    Returns None (not error) — caller decides what to do with unmatched questions.
    """
    if is_mutating(question):
        return None

    if not _UNITY_NOUNS_RE.search(question):
        return None

    for pattern, key in _PATTERNS:
        if pattern.search(question):
            plan = CANONICAL_PLANS.get(key)
            if plan is not None:
                return plan

    # Fallback: question has Unity noun but no specific pattern — generic scene query
    return CANONICAL_PLANS.get("SCENE_QUERY")
