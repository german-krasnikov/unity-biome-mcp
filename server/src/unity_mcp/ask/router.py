"""Keyword regex router — deterministic 80% of ask() questions."""
import re

from ..decisions.askroute_facts import ask_route
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

# Broken/scene-health/editor-state/compile regexes feed askRoute's facts (router priority is in the DECISION, not here).
_BROKEN_REFS_RE = re.compile(r"\bbroken\b.*\b(refs?|references?|links?)|\b(refs?|references?|links?).*\bbroken\b", re.I)
_SCENE_HEALTH_RE = re.compile(r"\b(errors?|wrong|issues?|problems?)\b.*\b(scene|console)|\b(scene|console).*\b(errors?|issues?|problems?)\b", re.I)
_EDITOR_STATE_RE = re.compile(r"\bplay(ing|mode| mode)\b|\bpaused\b|\bdirty\b", re.I)
_COMPILE_RE = re.compile(r"\bcompil(e|ation|ing)\b", re.I)



def is_mutating(question: str) -> bool:
    return bool(_MUTATING_RE.match(question))


def ask_facts(question: str) -> dict:
    """The regex facts of `question`, folded for askRoute. Each fact is one cheap regex evaluation."""
    return {
        "mutating": is_mutating(question),
        "unity_noun": bool(_UNITY_NOUNS_RE.search(question)),
        "broken_refs": bool(_BROKEN_REFS_RE.search(question)),
        "scene_health": bool(_SCENE_HEALTH_RE.search(question)),
        "editor_state": bool(_EDITOR_STATE_RE.search(question)),
        "compile": bool(_COMPILE_RE.search(question)),
    }


def route(question: str) -> ToolPlan | None:
    """
    Return ToolPlan for question, or None if out-of-scope / no match.
    Returns None (not error) — caller decides what to do with unmatched questions.
    """
    return CANONICAL_PLANS.get(ask_route(**ask_facts(question)))
