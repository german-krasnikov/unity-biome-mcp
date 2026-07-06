"""F06: TIER1 tool descriptions stay terse but keep enums/grammar (anti-hallucination).

Two-sided lock: a char budget prevents prose creep; required-substring asserts
prevent a trim from silently dropping enum values the LLM needs to avoid hallucination.
"""
from unity_mcp.tools.screenshot import screenshot
from unity_mcp.tools.code_intel import compile_preflight
from unity_mcp.tools.runtime import run_playtest
from unity_mcp.tools.batch import batch


def test_screenshot_doc_terse_keeps_enums():
    doc = screenshot.__doc__
    assert len(doc) < 520, f"screenshot doc creeping back to bloat: {len(doc)} chars"
    for token in ("scene_view", "multi_view", "single_view", "overview_game",
                  "front|left|top|iso", "supersample", "highlight", "show_colliders"):
        assert token in doc, f"screenshot doc lost anti-hallucination token: {token}"


def test_compile_preflight_doc_terse():
    doc = compile_preflight.__doc__
    assert len(doc) < 320, f"compile_preflight doc bloat: {len(doc)} chars"
    assert "ROSLYN UNAVAILABLE" in doc


def test_run_playtest_doc_keeps_full_dsl_grammar():
    """run_playtest is mostly irreducible DSL grammar — must NOT be trimmed away."""
    doc = run_playtest.__doc__
    for cmd in ("MOVE TO", "WAIT_UNTIL", "ASSERT_CONSOLE_CLEAN", "ASSERT_CONSERVED",
                "ASSERT_CTA", "TRACE_FLOW", "INVARIANT", "SIMULATE"):
        assert cmd in doc, f"run_playtest lost DSL command (hallucination risk): {cmd}"


def test_batch_doc_terse_keeps_key_semantics():
    """B4a: batch is TIER1/core — its schema is sent every turn. Regression guard
    against re-bloat, while keeping the substrings an LLM needs to call it correctly."""
    doc = batch.__doc__
    assert len(doc) < 400, f"batch doc creeping back to bloat: {len(doc)} chars"
    for token in ("continue|stop", "default 75", "atomic", "Undo", "PREFER"):
        assert token in doc, f"batch doc lost key token: {token}"


# ── Phase A regression: [Play Mode] qualifier survives _short_description() ──

import pytest
from unity_mcp.server_filtering import _short_description
from unity_mcp.tools import runtime, diagnostics, watch as watch_mod

RUNTIME_TOOLS = [
    (runtime.invoke_method,        "[Play Mode]"),
    (runtime.set_runtime_property, "[Play Mode]"),
    (runtime.wait_until,           "[Play Mode]"),
    (runtime.move_to,              "[Play Mode]"),
    (runtime.query_state,          "[Play Mode]"),
    (runtime.test_step,            "[Play Mode]"),
    (diagnostics.get_perf,         "[Play Mode]"),
    (diagnostics.debug_animator,   "[Play Mode]"),
    (diagnostics.debug_physics,    "[Play Mode]"),
    (watch_mod.watch,              "[Play Mode]"),
    (runtime.run_playtest,         "[Play Mode]"),
]


@pytest.mark.parametrize("fn,qualifier", RUNTIME_TOOLS, ids=[f[0].__name__ for f in RUNTIME_TOOLS])
def test_runtime_qualifier_survives_short_description(fn, qualifier):
    short = _short_description(fn.__doc__)
    assert qualifier in short, (
        f"{fn.__name__}: '{qualifier}' not in _short_description result: {short!r}"
    )


@pytest.mark.parametrize("fn,_", RUNTIME_TOOLS, ids=[f[0].__name__ for f in RUNTIME_TOOLS])
def test_short_description_does_not_exceed_120(fn, _):
    short = _short_description(fn.__doc__)
    base = short.rstrip("…")
    assert len(base) <= 120, (
        f"{fn.__name__}: _short_description returned {len(base)} meaningful chars (max 120): {short!r}"
    )
