"""Golden test for DIFF_PROMPTS sentinels. Pin contracts; refactor must not silently change behavior."""
import pytest
from unity_mcp.visual_diff import DIFF_PROMPTS

SENTINELS = {
    "general":    ["NO_CHANGE", "Max 5 bullets"],
    "ui_layout":  ["LAYOUT_OK", "moved:", "broken:"],
    "animation":  ["NO_MOTION"],
    "color":      ["NO_COLOR_CHANGE"],
    "position":   ["screen quadrants"],
    "regression": ["PASS", "FAIL:"],
    "targeted":   ["{question}", "YES or NO"],
}


@pytest.mark.parametrize("key,sentinels", list(SENTINELS.items()))
def test_diff_prompts_sentinels_present(key, sentinels):
    """Required sentinels (NO_CHANGE, LAYOUT_OK, PASS, etc) must remain in prompts."""
    p = DIFF_PROMPTS[key]
    for s in sentinels:
        assert s in p, f"DIFF_PROMPTS[{key!r}] missing required sentinel {s!r}"


def test_structural_dispatch_resolves_to_general():
    """visual_diff.py — `structural` mode dispatches to `general` prompt key. Pin the contract."""
    assert "general" in DIFF_PROMPTS
    assert "structural" not in DIFF_PROMPTS  # no separate structural prompt
