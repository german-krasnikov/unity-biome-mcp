"""AnimatorControllerHelper "not found" wire-text characterization (T3 slice,
dsl-bottlenecks-2026-11, Plans/t3-object-lookup-measure.md "First slice").

AnimatorControllerHelper.cs had 19 raw `throw new ArgumentException(...)` /
`throw new InvalidOperationException(...)` call sites with inline "X not found: Y"
/ "X 'Y' not found" interpolated strings (12 distinct wordings, grep -n -i
"not found" unity-plugin/Editor/AnimatorControllerHelper.cs, captured before any
text was touched). This file pins that exact wording and proves the migration
onto two typed `ErrorHelper.NotFound(kind, id)` / `ErrorHelper.QuotedNotFound(kind,
id)` methods (unity-plugin/Editor/ErrorHelper.cs) is a mechanical move — same
text, same call shape, now written once per wording-shape instead of 19 times.

No Unity/dotnet needed (same shape as test_relay_frame_contract.py): a Python
mirror of the two C# format templates reproduces every pinned literal, and a
static scan of the .cs file proves none of the 19 raw inline throws survived
and the two new ErrorHelper methods exist with the expected body.
"""
from pathlib import Path

_CS_PATH = (
    Path(__file__).parents[2] / "unity-plugin" / "Editor" / "AnimatorControllerHelper.cs"
)
_CS_SOURCE = _CS_PATH.read_text(encoding="utf-8")

_ERROR_HELPER_PATH = (
    Path(__file__).parents[2] / "unity-plugin" / "Editor" / "ErrorHelper.cs"
)
_ERROR_HELPER_SOURCE = _ERROR_HELPER_PATH.read_text(encoding="utf-8")


def _not_found(kind: str, ident: str) -> str:
    """Mirrors ErrorHelper.NotFound: $"{kind} not found: {id}"."""
    return f"{kind} not found: {ident}"


def _quoted_not_found(kind: str, ident: str) -> str:
    """Mirrors ErrorHelper.QuotedNotFound: $"{kind} '{id}' not found"."""
    return f"{kind} '{ident}' not found"


# Pinned BEFORE the refactor, byte-identical to the former raw throw sites in
# AnimatorControllerHelper.cs. (kind, id, expected-literal). "id" is the value a
# real call site would substitute — for Clip it already carries its own quotes,
# matching the original `$"Clip not found: '{clipName}'"` shape exactly.
_PINNED_NOT_FOUND = [
    ("Target state", "Walk", "Target state not found: Walk"),
    ("Source state", "Idle", "Source state not found: Idle"),
    ("State", "Walk", "State not found: Walk"),
    ("Clip", "'Walk'", "Clip not found: 'Walk'"),
    ("Parameter", "Speed", "Parameter not found: Speed"),
    ("Layer", "FX", "Layer not found: FX"),
    ("Avatar", "Assets/Avatars/Hero.asset", "Avatar not found: Assets/Avatars/Hero.asset"),
]

_PINNED_QUOTED_NOT_FOUND = [
    ("state", "Walk", "state 'Walk' not found"),
    ("AnyState transition to", "Walk", "AnyState transition to 'Walk' not found"),
    ("Transition", "Idle → Walk", "Transition 'Idle → Walk' not found"),
]


def test_not_found_template_reproduces_pinned_wording():
    for kind, ident, expected in _PINNED_NOT_FOUND:
        assert _not_found(kind, ident) == expected


def test_quoted_not_found_template_reproduces_pinned_wording():
    for kind, ident, expected in _PINNED_QUOTED_NOT_FOUND:
        assert _quoted_not_found(kind, ident) == expected


def test_error_helper_defines_the_two_typed_methods():
    assert "public static string NotFound(string kind, string id)" in _ERROR_HELPER_SOURCE, (
        "ErrorHelper.NotFound signature changed or was removed"
    )
    assert '$"{kind} not found: {id}"' in _ERROR_HELPER_SOURCE, (
        "ErrorHelper.NotFound body no longer matches the pinned wire-text template"
    )
    assert "public static string QuotedNotFound(string kind, string id)" in _ERROR_HELPER_SOURCE, (
        "ErrorHelper.QuotedNotFound signature changed or was removed"
    )
    assert "$\"{kind} '{id}' not found\"" in _ERROR_HELPER_SOURCE, (
        "ErrorHelper.QuotedNotFound body no longer matches the pinned wire-text template"
    )


def test_no_raw_inline_not_found_throws_remain():
    """Every former raw inline '... not found ...' throw in AnimatorControllerHelper.cs
    must now route through ErrorHelper.NotFound/QuotedNotFound — guards against a future
    raw throw creeping back in for this wording family (a regression, not a reword)."""
    for line in _CS_SOURCE.splitlines():
        if "not found" not in line.lower():
            continue
        assert "ErrorHelper." in line, (
            f"raw inline 'not found' throw reintroduced (bypasses ErrorHelper): {line.strip()!r}"
        )


def test_error_helper_not_found_methods_used_at_expected_site_count():
    # 19 former raw throws: 13 collapse onto NotFound (Target/Source state, State,
    # Clip, Parameter, Layer, Avatar), 6 onto QuotedNotFound (state, AnyState
    # transition, Transition) — see Plans/t3-object-lookup-measure.md census.
    assert _CS_SOURCE.count("ErrorHelper.NotFound(") == 13
    assert _CS_SOURCE.count("ErrorHelper.QuotedNotFound(") == 6
