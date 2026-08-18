"""Tests for generate_schema_page.py."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))
from generate_schema_page import _load_scores, _load_tools, _risk_badge, _score_badge, render_schema_page

SAMPLE_TOOLS = [
    {
        "name": "my_tool",
        "description": "Does stuff.\nExtra line.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "path": {"type": "string", "description": "The path"},
                "force": {"type": "boolean", "default": False},
            },
            "required": ["path"],
        },
    },
    {
        "name": "simple_tool",
        "description": "No params.",
        "inputSchema": {"type": "object", "properties": {}},
    },
]

SAMPLE_TOOLSMITH_AUDIT = {
    "passed": True,
    "tools": [
        {
            "tool": {"name": "my_tool"},
            "findings": [
                {"severity": "warning", "message": "Missing desc", "suggestion": "Add desc"},
            ],
            "error_count": 0,
            "warning_count": 1,
        },
        {
            "tool": {"name": "simple_tool"},
            "findings": [],
            "error_count": 0,
            "warning_count": 0,
        },
    ],
}

SAMPLE_TOOLSMITH_LINT = {
    "tools": [
        {
            "tool_name": "my_tool",
            "score": 85,
            "risk_level": "medium",
            "issues": [
                {"severity": "warning", "message": "Missing desc", "suggestion": "Add desc"},
            ],
        },
        {
            "tool_name": "simple_tool",
            "score": 100,
            "risk_level": "low",
            "issues": [],
        },
    ],
}


def test_load_tools_dict(tmp_path: Path) -> None:
    p = tmp_path / "tools.json"
    p.write_text(json.dumps({"tools": SAMPLE_TOOLS}))
    assert len(_load_tools(p)) == 2


def test_load_tools_array(tmp_path: Path) -> None:
    p = tmp_path / "tools.json"
    p.write_text(json.dumps(SAMPLE_TOOLS))
    assert len(_load_tools(p)) == 2


def test_load_scores_audit_format(tmp_path: Path) -> None:
    p = tmp_path / "ts.json"
    p.write_text(json.dumps(SAMPLE_TOOLSMITH_AUDIT))
    scores = _load_scores(p)
    assert "my_tool" in scores
    assert scores["my_tool"]["score"] == 98
    assert scores["my_tool"]["risk_level"] == "low"
    assert len(scores["my_tool"]["issues"]) == 1


def test_load_scores_lint_format(tmp_path: Path) -> None:
    p = tmp_path / "ts.json"
    p.write_text(json.dumps(SAMPLE_TOOLSMITH_LINT))
    scores = _load_scores(p)
    assert scores["my_tool"]["score"] == 85
    assert scores["my_tool"]["risk_level"] == "medium"
    assert scores["simple_tool"]["score"] == 100


def test_load_scores_none() -> None:
    assert _load_scores(None) == {}


def test_load_scores_missing(tmp_path: Path) -> None:
    assert _load_scores(tmp_path / "nope.json") == {}


def test_score_badge() -> None:
    assert "🟢" in _score_badge(80)
    assert "🟡" in _score_badge(60)
    assert "🟡" in _score_badge(79)
    assert "🔴" in _score_badge(59)


def test_risk_badge() -> None:
    assert "low" in _risk_badge("low")
    assert "🔴" in _risk_badge("high")
    assert _risk_badge("unknown") == "unknown"


def test_render_no_scores() -> None:
    page = render_schema_page(SAMPLE_TOOLS, {})
    assert "178" not in page
    assert "my_tool" in page
    assert "simple_tool" in page
    assert "## Overview" in page
    assert "## Tool Details" in page


def test_render_with_scores() -> None:
    scores_data = {
        "my_tool": {"score": 85, "risk_level": "medium", "issues": SAMPLE_TOOLSMITH_LINT["tools"][0]["issues"]},
        "simple_tool": {"score": 100, "risk_level": "low", "issues": []},
    }
    page = render_schema_page(SAMPLE_TOOLS, scores_data)
    assert "92.5" in page  # avg (85+100)/2
    assert "1 quality issues" in page
    assert "Missing desc" in page
    assert "Add desc" in page


def test_render_parameter_table() -> None:
    page = render_schema_page(SAMPLE_TOOLS, {})
    assert "| `path` | string | ✓ |" in page
    assert "| `force` | boolean |  |" in page
    assert "(default: `False`)" in page


def test_render_json_schema() -> None:
    page = render_schema_page(SAMPLE_TOOLS, {})
    assert "```json" in page
    assert '"required"' in page


def test_render_tool_count() -> None:
    page = render_schema_page(SAMPLE_TOOLS, {})
    assert "**2 registered tools**" in page


def test_render_high_risk() -> None:
    scores = {"my_tool": {"score": 50, "risk_level": "high", "issues": []}}
    page = render_schema_page(SAMPLE_TOOLS, scores)
    assert "🔴 50/100" in page
    assert "🔴 high" in page
