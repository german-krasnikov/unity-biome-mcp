"""Tests for sampling_postproc — Haiku output normalization."""
import pytest
from unity_mcp.sampling_postproc import (
    is_refusal, strip_fences, strip_conversational, normalize,
)


def test_strip_fences_idempotent():
    assert strip_fences(strip_fences("```\ncode\n```")) == strip_fences("```\ncode\n```")
    assert strip_fences("```\ncode\n```") == "code"

def test_strip_fences_with_lang():
    assert strip_fences("```python\nx = 1\n```") == "x = 1"

def test_strip_fences_no_fence_unchanged():
    assert strip_fences("plain text") == "plain text"

@pytest.mark.parametrize("text", [
    "I cannot help with that",
    "I can't process this image",
    "Sorry, I can't describe",
    "I'm unable to analyze",
    "I am unable to process",
])
def test_is_refusal_positive(text):
    assert is_refusal(text)

@pytest.mark.parametrize("text", [
    "I see a red cube",
    "A button labeled Login",
    "I can't tell exactly, but I see a red cube",
    "I cannot say precisely, however the player is visible",
])
def test_is_refusal_negative(text):
    assert not is_refusal(text)

def test_strip_conversational_sure():
    assert strip_conversational("Sure! PASS: button visible") == "PASS: button visible"

def test_strip_conversational_looking():
    assert strip_conversational("Looking at this image, I see red") == "I see red"

def test_normalize_none_passthrough():
    assert normalize(None, "verdict") == (None, False)

def test_normalize_refusal_returns_none():
    text, refused = normalize("I cannot describe this", "description")
    assert text is None and refused is True

def test_normalize_verdict_first_line():
    text, refused = normalize("PASS\n\nThe button is correctly placed.", "verdict")
    assert text == "PASS"
    assert refused is False

def test_normalize_dsl_preserves_failed():
    text, _ = normalize("```\nset_property /A x 1\nFAIL: invalid\n```", "dsl")
    assert "FAIL:" in text

def test_normalize_combo_strip_fences_and_conversational():
    text, _ = normalize("Sure! ```\nPASS\n```", "verdict")
    assert text == "PASS"

def test_normalize_empty_after_cleanup():
    text, refused = normalize("```\n```", "description")
    assert text is None
    assert refused is False


# ── P2: normalize with sentinel kind ─────────────────────────────────────────

def test_normalize_sentinel_returns_first_token():
    text, refused = normalize("CHANGED more words follow", "sentinel")
    assert text == "CHANGED"
    assert refused is False


def test_normalize_sentinel_strips_fences_then_first_token():
    text, refused = normalize("```\nPASS extra\n```", "sentinel")
    assert text == "PASS"
    assert refused is False


def test_normalize_sentinel_empty_after_strip_returns_none():
    text, refused = normalize("```\n```", "sentinel")
    assert text is None
    assert refused is False


def test_normalize_sentinel_none_input():
    text, refused = normalize(None, "sentinel")
    assert text is None
    assert refused is False
