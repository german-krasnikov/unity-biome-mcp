"""coverage_hotspots.py — changed-aware top-N coverage hotspot report.

Turns list[MethodRecord] (see opencover_parse.py) + a changed-lines map into
a ranked top-20, rendered as Markdown/JSON. Owns ranking policy and
rendering; does not parse OpenCover XML itself.

Ranking policy (Plans/PR-08.md Component 2): changed methods always sort
before unchanged; within a bucket, sort by cyclomatic_complexity *
(1 - coverage_fraction) descending, with coverage_fraction=None (unknown,
seq_total == 0) treated as the worst case (1 - 0 = 1) so unmapped/unknown
methods surface instead of hiding -- never rendered as "100%" or "0%".
"""
import argparse
import json
import pathlib
import re
import subprocess
import sys
from dataclasses import dataclass

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from opencover_parse import MethodRecord, parse_opencover  # noqa: E402

_HUNK_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def git_changed_lines(base_ref: str, head_ref: str = "HEAD") -> dict[str, set[int]]:
    """Runs `git diff --unified=0 {base_ref}...{head_ref}`, parses '@@ -a,b +c,d @@'
    hunks, returns {file: {changed line numbers in head}}."""
    result = subprocess.run(
        ["git", "diff", "--unified=0", f"{base_ref}...{head_ref}"],
        capture_output=True,
        text=True,
        check=True,
    )
    changed: dict[str, set[int]] = {}
    current_file = None
    for line in result.stdout.splitlines():
        if line.startswith("+++ "):
            raw_path = line[4:].strip()
            current_file = None if raw_path == "/dev/null" else raw_path.removeprefix("b/")
            if current_file is not None:
                changed.setdefault(current_file, set())
            continue
        match = _HUNK_RE.match(line)
        if match and current_file is not None:
            start = int(match.group(1))
            count = int(match.group(2)) if match.group(2) is not None else 1
            if count == 0:
                continue  # deletion-only hunk -- no added lines in head
            changed[current_file].update(range(start, start + count))
    return changed


@dataclass(frozen=True)
class HotspotRow:
    method: MethodRecord
    changed: bool
    coverage_fraction: float | None  # None => "unknown", never treated as 100%
    score: float
    scenario_ref: str  # "unknown" if no match


@dataclass(frozen=True)
class ReportMeta:
    source_sha: str
    lane: str            # "Linux" -- matches unity-tests.yml matrix.name
    unity_version: str   # "6000.0.65f1" -- matches unity-tests.yml
    generated_for_sha: str  # current HEAD at report-render time
    stale: bool           # generated_for_sha != source_sha


def load_scenario_map(path: pathlib.Path) -> dict[str, str]:
    """ClassName.MethodName<TAB>scenario-id, one per line. '#'-prefixed lines ignored."""
    mapping: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        key, _, value = line.partition("\t")
        if value:
            mapping[key.strip()] = value.strip()
    return mapping


def _is_changed(method: MethodRecord, changed_lines: dict[str, set[int]]) -> bool:
    lines_in_file = changed_lines.get(method.file)
    if not lines_in_file:
        return False
    return any(method.line_start <= ln <= method.line_end for ln in lines_in_file)


def rank_methods(
    methods: list[MethodRecord],
    changed_lines: dict[str, set[int]],
    scenario_map: dict[str, str],
    limit: int = 20,
) -> list[HotspotRow]:
    rows = []
    for method in methods:
        coverage_fraction = None if method.seq_total == 0 else method.seq_covered / method.seq_total
        uncovered_fraction = 1.0 if coverage_fraction is None else (1 - coverage_fraction)
        score = (method.cyclomatic_complexity or 0) * uncovered_fraction
        scenario_key = f"{method.class_name}.{method.method_name}"
        rows.append(HotspotRow(
            method=method,
            changed=_is_changed(method, changed_lines),
            coverage_fraction=coverage_fraction,
            score=score,
            scenario_ref=scenario_map.get(scenario_key, "unknown"),
        ))
    rows.sort(key=lambda r: (not r.changed, -r.score))
    return rows[:limit]


def render_markdown(rows: list[HotspotRow], meta: ReportMeta) -> str:
    lines = ["# Coverage Hotspots", ""]
    if meta.stale:
        lines.append(
            f"> **historical** -- report captured at `{meta.source_sha}`, "
            f"current HEAD is `{meta.generated_for_sha}`; treat as historical, not this PR's coverage."
        )
        lines.append("")
    lines.append(f"_lane: {meta.lane} &middot; unity: {meta.unity_version} &middot; source: `{meta.source_sha}`_")
    lines.append("")
    lines.append("| # | Method | File | Changed | CC | Coverage | Score | Scenario |")
    lines.append("|---|---|---|---|---|---|---|---|")
    for i, row in enumerate(rows, start=1):
        coverage = "unknown" if row.coverage_fraction is None else f"{row.coverage_fraction * 100:.1f}%"
        cc = row.method.cyclomatic_complexity if row.method.cyclomatic_complexity is not None else "?"
        lines.append(
            f"| {i} | `{row.method.class_name}.{row.method.method_name}` | {row.method.file} | "
            f"{'yes' if row.changed else ''} | {cc} | {coverage} | {row.score:.2f} | {row.scenario_ref} |"
        )
    return "\n".join(lines) + "\n"


def render_json(rows: list[HotspotRow], meta: ReportMeta) -> dict:
    return {
        "meta": {
            "source_sha": meta.source_sha,
            "lane": meta.lane,
            "unity_version": meta.unity_version,
            "generated_for_sha": meta.generated_for_sha,
            "stale": meta.stale,
        },
        "rows": [
            {
                "class_name": row.method.class_name,
                "method_name": row.method.method_name,
                "file": row.method.file,
                "changed": row.changed,
                "cyclomatic_complexity": row.method.cyclomatic_complexity,
                "coverage_fraction": row.coverage_fraction,
                "score": row.score,
                "scenario_ref": row.scenario_ref,
            }
            for row in rows
        ],
    }


def _current_head_sha() -> str:
    result = subprocess.run(
        ["git", "rev-parse", "HEAD"], capture_output=True, text=True, check=True
    )
    return result.stdout.strip()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--opencover", type=pathlib.Path, nargs="+", required=True)
    parser.add_argument("--base-ref", required=True)
    parser.add_argument("--scenario-map", type=pathlib.Path, required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--lane", required=True)
    parser.add_argument("--unity-version", required=True)
    parser.add_argument("--out-md", type=pathlib.Path, required=True)
    parser.add_argument("--out-json", type=pathlib.Path, required=True)
    args = parser.parse_args()

    methods: list[MethodRecord] = []
    for opencover_path in args.opencover:
        methods.extend(parse_opencover(opencover_path))

    changed_lines = git_changed_lines(args.base_ref)
    scenario_map = load_scenario_map(args.scenario_map)
    rows = rank_methods(methods, changed_lines, scenario_map)

    generated_for_sha = _current_head_sha()
    meta = ReportMeta(
        source_sha=args.source_sha,
        lane=args.lane,
        unity_version=args.unity_version,
        generated_for_sha=generated_for_sha,
        stale=args.source_sha != generated_for_sha,
    )

    args.out_md.parent.mkdir(parents=True, exist_ok=True)
    args.out_md.write_text(render_markdown(rows, meta), encoding="utf-8")
    args.out_json.parent.mkdir(parents=True, exist_ok=True)
    args.out_json.write_text(json.dumps(render_json(rows, meta), indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
