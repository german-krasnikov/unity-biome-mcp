"""quality_delta.py — parse linter reports, compute delta, write data files.

Mode A: compute + write (default)
Mode B: --append latest to history
"""
from __future__ import annotations

import argparse
import contextlib
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
        sev = s.get("issues_by_severity", {})
        return {
            "errors": int(sev.get("error", 0)) + int(sev.get("critical", 0)),
            "warnings": int(sev.get("warning", 0)),
            "avg_score": float(s.get("score", 0.0)),
        }
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


def render_report(
    current: dict,
    toolsmith_path: pathlib.Path,
    mcplint_path: pathlib.Path,
    *,
    test_results_paths: list[pathlib.Path] | None = None,
    coverage_path: pathlib.Path | None = None,
) -> str:
    """Full public REPORT.md with all quality data."""
    lines = [
        "# Quality Report",
        "",
        f"> Auto-generated on **{current['date']}** from commit "
        f"`{current['commit']}` (v{current['version']})",
        "",
    ]

    # --- Project Overview ---
    lines += [
        "## Project Overview",
        "",
        "| Metric | Value |",
        "|--------|-------|",
        f"| Version | v{current['version']} |",
        f"| Commit | `{current['commit']}` |",
        f"| Date | {current['date']} |",
    ]
    try:
        data = json.loads(toolsmith_path.read_text(encoding="utf-8"))
        tool_count = data.get("summary", {}).get("tools_scanned", 0)
        if tool_count:
            lines.append(f"| MCP Tools | {tool_count} |")
    except Exception:
        pass
    lines.append("")

    # --- Test Results ---
    all_suites: list[dict] = []
    for trp in test_results_paths or []:
        with contextlib.suppress(Exception):
            data = json.loads(trp.read_text(encoding="utf-8"))
            all_suites.extend(data.get("suites", []))
    if all_suites:
        lines += [
            "## Test Results",
            "",
            "| Suite | Passed | Failed | Skipped | Total | Status |",
            "|-------|--------|--------|---------|-------|--------|",
        ]
        for suite in all_suites:
            name = suite.get("name", "?")
            passed = suite.get("passed", 0)
            failed = suite.get("failed", 0)
            skipped = suite.get("skipped", 0)
            total = suite.get("total", passed + failed + skipped)
            status = "✅" if failed == 0 else "❌"
            lines.append(f"| {name} | {passed} | {failed} | {skipped} | {total} | {status} |")
        lines.append("")

    # --- Coverage ---
    cov_data = None
    if coverage_path:
        with contextlib.suppress(Exception):
            cov_data = json.loads(coverage_path.read_text(encoding="utf-8"))
    if cov_data:
        lines += [
            "## Code Coverage",
            "",
            "| Module | Statements | Covered | Missed | Coverage |",
            "|--------|------------|---------|--------|----------|",
        ]
        for mod in cov_data.get("modules", []):
            name = mod.get("name", "?")
            stmts = mod.get("statements", 0)
            covered = mod.get("covered", 0)
            missed = mod.get("missed", 0)
            pct = mod.get("coverage", 0.0)
            lines.append(f"| {name} | {stmts} | {covered} | {missed} | {pct:.1f}% |")
        total = cov_data.get("total", {})
        if total:
            lines.append(
                f"| **Total** | **{total.get('statements', 0)}** "
                f"| **{total.get('covered', 0)}** "
                f"| **{total.get('missed', 0)}** "
                f"| **{total.get('coverage', 0.0):.1f}%** |"
            )
        lines.append("")

    # --- Tool Quality Summary ---
    lines += [
        "## Tool Quality",
        "",
        "| Linter | Errors | Warnings | Score |",
        "|--------|--------|----------|-------|",
        f"| mcp-tool-card-linter | {current['toolsmith_errors']} "
        f"| {current['toolsmith_warnings']} | {current['toolsmith_avg_score']}/100 |",
        f"| mcp-lint | {current['mcplint_errors']} "
        f"| {current['mcplint_warnings']} | — |",
        "",
    ]

    # Per-tool breakdown from toolsmith JSON
    try:
        data = json.loads(toolsmith_path.read_text(encoding="utf-8"))
        tools = data.get("tools", [])
        if tools:
            lines += [
                "### Per-Tool Scores",
                "",
                "<details>",
                f"<summary>{len(tools)} tools scored (click to expand)</summary>",
                "",
                "| Tool | Score | Errors | Warnings | Risk |",
                "|------|-------|--------|----------|------|",
            ]
            for t in sorted(tools, key=lambda x: x.get("score", 0)):
                name = t.get("name", "?")
                score = t.get("score", 0)
                findings = t.get("findings", [])
                errs = sum(1 for f in findings if f.get("severity") in ("error", "critical"))
                warns = sum(1 for f in findings if f.get("severity") == "warning")
                risk = t.get("risk", "—")
                lines.append(f"| `{name}` | {score} | {errs} | {warns} | {risk} |")
            lines += ["", "</details>", ""]
    except Exception:
        pass

    # mcplint issues grouped by tool
    try:
        text = mcplint_path.read_text(encoding="utf-8")
        mcplint_lines = text.strip().splitlines()
        if mcplint_lines:
            lines += [
                "### Cross-Client Issues (mcp-lint)",
                "",
                "<details>",
                "<summary>Issues by tool (click to expand)</summary>",
                "",
            ]
            current_tool = None
            issues: list[str] = []
            for raw in mcplint_lines:
                stripped = raw.strip()
                if not stripped:
                    continue
                if not raw.startswith(" ") and not raw.startswith("\t"):
                    if current_tool and issues:
                        lines.append(f"#### `{current_tool}`")
                        lines.append("")
                        for iss in issues:
                            lines.append(f"- {iss}")
                        lines.append("")
                    current_tool = stripped
                    issues = []
                elif "✖" in stripped or "⚠" in stripped:
                    issues.append(stripped)
            if current_tool and issues:
                lines.append(f"#### `{current_tool}`")
                lines.append("")
                for iss in issues:
                    lines.append(f"- {iss}")
                lines.append("")
            lines += ["</details>", ""]
    except Exception:
        pass

    return "\n".join(lines)


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
    parser.add_argument("--out-report", type=pathlib.Path)
    parser.add_argument("--test-results", type=pathlib.Path, nargs="*", default=[])
    parser.add_argument("--coverage", type=pathlib.Path)
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

    if args.out_report:
        report = render_report(
            current, args.toolsmith, args.mcplint,
            test_results_paths=args.test_results or [],
            coverage_path=args.coverage,
        )
        _write_text(args.out_report, report)

    sys.exit(1 if regression else 0)


if __name__ == "__main__":
    main()
