"""quality_delta.py — parse linter reports, compute delta, write data files.

Mode A: compute + write (default)
Mode B: --append latest to history
"""
from __future__ import annotations

import argparse
import datetime
import json
import pathlib
import re
import subprocess
import sys

# ---------------------------------------------------------------------------
# Parsers
# ---------------------------------------------------------------------------


def parse_toolsmith(path: pathlib.Path) -> dict:
    """Parse mcp-toolsmith --json-report JSON.

    Returns: {"errors": int, "warnings": int, "avg_score": float}
    """
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {"errors": 0, "warnings": 0, "avg_score": 0.0}

    if "summary" in data:
        s = data["summary"]
        return {
            "errors": int(s.get("total_errors", 0)),
            "warnings": int(s.get("total_warnings", 0)),
            "avg_score": float(s.get("average_score", 0.0)),
        }
    # top-level fallback
    return {
        "errors": int(data.get("errors", 0)),
        "warnings": int(data.get("warnings", 0)),
        "avg_score": float(data.get("avg_score", 0.0)),
    }


def parse_mcplint(path: pathlib.Path) -> dict:
    """Parse mcp-lint text output.

    Count ✖ (U+2716) → errors, ⚠ (U+26A0) → warnings.
    Returns zeros if file missing/unreadable.
    """
    try:
        text = path.read_text(encoding="utf-8")
    except Exception:
        return {"errors": 0, "warnings": 0}
    return {
        "errors": len(re.findall("✖", text)),
        "warnings": len(re.findall("⚠", text)),
    }


# ---------------------------------------------------------------------------
# Snapshot builder
# ---------------------------------------------------------------------------


def _get_version(repo_root: pathlib.Path) -> str:
    changelog = repo_root / "CHANGELOG.md"
    try:
        for line in changelog.read_text(encoding="utf-8").splitlines():
            m = re.search(r"##\s+v?(\d+\.\d+\.\d+)", line)
            if m:
                return m.group(1)
    except Exception:
        pass
    pyproject = repo_root / "server" / "pyproject.toml"
    try:
        for line in pyproject.read_text(encoding="utf-8").splitlines():
            m = re.match(r'\s*version\s*=\s*"([^"]+)"', line)
            if m:
                return m.group(1)
    except Exception:
        pass
    return "unknown"


def _get_commit() -> str:
    try:
        r = subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"],
            capture_output=True,
            text=True,
            timeout=5,
        )
        return r.stdout.strip() or "unknown"
    except Exception:
        return "unknown"


def build_snapshot(toolsmith: dict, mcplint: dict, repo_root: pathlib.Path) -> dict:
    """Assemble latest.json entry."""
    return {
        "date": datetime.date.today().isoformat(),
        "version": _get_version(repo_root),
        "commit": _get_commit(),
        "toolsmith_errors": toolsmith["errors"],
        "toolsmith_warnings": toolsmith["warnings"],
        "toolsmith_avg_score": toolsmith["avg_score"],
        "mcplint_errors": mcplint["errors"],
        "mcplint_warnings": mcplint["warnings"],
    }


# ---------------------------------------------------------------------------
# Delta + regression
# ---------------------------------------------------------------------------


def compute_delta(current: dict, baseline: dict | None) -> dict:
    """Per-metric signed delta. baseline=None → all 0, has_baseline=False."""
    if baseline is None:
        return {
            "has_baseline": False,
            "toolsmith_errors": 0,
            "toolsmith_warnings": 0,
            "toolsmith_avg_score": 0.0,
            "mcplint_errors": 0,
            "mcplint_warnings": 0,
        }
    keys = ["toolsmith_errors", "toolsmith_warnings", "toolsmith_avg_score",
            "mcplint_errors", "mcplint_warnings"]
    return {"has_baseline": True, **{k: current[k] - baseline[k] for k in keys}}


def is_regression(current: dict, baseline: dict | None) -> bool:
    """True if current errors > baseline errors (either linter)."""
    if baseline is None:
        return False
    return (
        current["toolsmith_errors"] > baseline["toolsmith_errors"]
        or current["mcplint_errors"] > baseline["mcplint_errors"]
    )


# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------


def render_pr_comment(current: dict, baseline: dict | None, delta: dict) -> str:
    """Markdown table with delta. Omits delta column when no baseline."""
    has_baseline = delta.get("has_baseline", False)
    regression = is_regression(current, baseline)

    def _fmt_delta(key: str) -> str:
        v = delta[key]
        if v == 0:
            return "±0"
        return f"+{v}" if v > 0 else str(v)

    rows = [
        ("toolsmith errors", "toolsmith_errors"),
        ("toolsmith warnings", "toolsmith_warnings"),
        ("avg score", "toolsmith_avg_score"),
        ("mcplint errors", "mcplint_errors"),
        ("mcplint warnings", "mcplint_warnings"),
    ]

    if has_baseline:
        header = "| Metric | Current | Baseline | Delta |\n|---|---|---|---|"
        lines = [
            f"| {label} | {current[key]} | {baseline[key]} | {_fmt_delta(key)} |"
            for label, key in rows
        ]
    else:
        header = "| Metric | Current |\n|---|---|"
        lines = [f"| {label} | {current[key]} |" for label, key in rows]

    table = "\n".join([header] + lines)

    parts = ["<!-- tool-quality-report -->", "## Tool Quality Report", table]
    if regression:
        parts.append(
            "\n> [!CAUTION]\n> **REGRESSION DETECTED** — error count increased."
        )
    return "\n\n".join(parts) + "\n"


def make_badge(current: dict) -> dict:
    """shields.io JSON badge."""
    total_errors = current["toolsmith_errors"] + current["mcplint_errors"]
    avg = current["toolsmith_avg_score"]
    if total_errors == 0:
        color = "green"
    elif total_errors <= 10:
        color = "yellow"
    else:
        color = "red"
    return {
        "schemaVersion": 1,
        "label": "tool quality",
        "message": f"{avg:.1f}/100 · {total_errors} errors",
        "color": color,
    }


# ---------------------------------------------------------------------------
# History
# ---------------------------------------------------------------------------


def load_history(path: pathlib.Path) -> list[dict]:
    """[] if missing/unreadable."""
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return []


def append_history(history: list, entry: dict, max_entries: int = 90) -> list:
    """Append + trim oldest from front."""
    result = history + [entry]
    if len(result) > max_entries:
        result = result[len(result) - max_entries:]
    return result


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def _write_json(path: pathlib.Path, data: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def _write_text(path: pathlib.Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--append", action="store_true")
    # Mode A
    parser.add_argument("--toolsmith", type=pathlib.Path)
    parser.add_argument("--mcplint", type=pathlib.Path)
    parser.add_argument("--history", type=pathlib.Path)
    parser.add_argument("--out-latest", type=pathlib.Path)
    parser.add_argument("--out-comment", type=pathlib.Path)
    parser.add_argument("--out-badge", type=pathlib.Path)
    # Mode B
    parser.add_argument("--latest", type=pathlib.Path)
    args = parser.parse_args()

    if args.append:
        # Mode B
        entry = json.loads(args.latest.read_text(encoding="utf-8"))
        history = load_history(args.history)
        updated = append_history(history, entry)
        _write_json(args.history, updated)
        sys.exit(0)

    # Mode A
    repo_root = pathlib.Path(__file__).parent.parent
    ts = parse_toolsmith(args.toolsmith)
    ml = parse_mcplint(args.mcplint)
    current = build_snapshot(ts, ml, repo_root)

    history = load_history(args.history) if args.history else []
    baseline = history[-1] if history else None
    delta = compute_delta(current, baseline)
    regression = is_regression(current, baseline)

    _write_json(args.out_latest, current)
    _write_text(args.out_comment, render_pr_comment(current, baseline, delta))
    _write_json(args.out_badge, make_badge(current))

    sys.exit(1 if regression else 0)


if __name__ == "__main__":
    main()
