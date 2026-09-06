"""cochange_report.py — thin, manual-only wrapper around the external
`cochange` tool (https://github.com/takahirom/cochange).

Scope decision (Plans/PR-08.md Component 4, following the PR-08 checklist's
"report-only, enable on real question" wording over the earlier plan's
"periodic report" framing): no CI wiring, no scheduled workflow. Invoked by
hand only when a real architectural question comes up.

cochange 0.5.0 (https://github.com/takahirom/cochange/releases/tag/0.5.0).
Install: `brew install takahirom/repo/cochange`, or from source via
`./gradlew installDist` (binary at build/install/cochange/bin/cochange).
Confirmed during PR-08 grounding (2026-09-06) via the tool's own README and
release list -- no live binary was run in this repo's environment, so exact
JSON field names beyond `options`/`commit`/`warnings` (documented in the
README's "How much to trust each line" section) are read defensively with
fallback to "unknown", never fabricated.
"""
import argparse
import json
import pathlib
import shutil
import subprocess

_COCHANGE_BINARY = "cochange"


class CochangeUnavailable(RuntimeError):
    """Raised when the pinned `cochange` binary is not on PATH."""


def run_cochange(paths: list[str], since: str, change_unit: str = "commit") -> dict:
    """Shells out to the pinned `cochange` binary; returns its parsed JSON.

    `paths` scope the analysis via repeated --module-root flags -- cochange
    analyzes one repository at a time, it has no multi-target analyze mode.

    Raises CochangeUnavailable if the binary isn't on PATH -- never silently
    returns an empty report that could be mistaken for '0 co-changes'.
    """
    if shutil.which(_COCHANGE_BINARY) is None:
        raise CochangeUnavailable(f"'{_COCHANGE_BINARY}' not found on PATH")

    cmd = [_COCHANGE_BINARY, "analyze", ".", "--since", since, "--change-unit", change_unit, "--json"]
    cmd += [f"--module-root={path}" for path in paths]
    result = subprocess.run(cmd, capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def render_report(data: dict, denominator_note: str) -> str:
    """Markdown table + the fixed inputs used (plan: record denominator,
    change-unit, window, SHA and warnings every time -- never just a score)."""
    options = data.get("options") or data.get("requestedOptions") or {}
    change_unit = options.get("changeUnit", "unknown")
    window = options.get("since", "unknown")
    sha = data.get("commit") or options.get("commit", "unknown")
    warnings = data.get("warnings", [])

    lines = [
        "# Cochange Report",
        "",
        f"- change unit: {change_unit}",
        f"- window: {window}",
        f"- SHA: {sha}",
        f"- denominator: {denominator_note}",
    ]
    if warnings:
        lines.append(f"- warnings: {', '.join(warnings)}")
    lines.append("")
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--paths", required=True, help="comma-separated module-root paths")
    parser.add_argument("--since", required=True)
    parser.add_argument("--change-unit", default="commit")
    parser.add_argument("--denominator-note", default="see cochange README 'evidence' tier")
    parser.add_argument("--out", type=pathlib.Path)
    args = parser.parse_args()

    data = run_cochange(args.paths.split(","), args.since, args.change_unit)
    report = render_report(data, args.denominator_note)

    if args.out:
        args.out.write_text(report, encoding="utf-8")
    else:
        print(report)


if __name__ == "__main__":
    main()
