"""C16: single source for lane -> test-runner filter expression.

Reads Tests/biome-test-lanes.json (C15) + Tests/taxonomy-map.json (C13) and
renders one lane's `filter` as either a pytest `-m` expression or NUnit
`--category`/`--assembly` flags for A22/A23's run_unity_tests.py. C17 wires
this into CI so the marker expression is generated once, not hand-typed in
4 places that can silently drift from each other.

Usage: gen_lane_args.py <pytest|nunit> <lane-name>
"""

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LANES_PATH = REPO_ROOT / "Tests" / "biome-test-lanes.json"
TAXONOMY_PATH = REPO_ROOT / "Tests" / "taxonomy-map.json"


def load_lanes(path: Path = LANES_PATH) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))["lanes"]


def load_dimensions(path: Path = TAXONOMY_PATH) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))["dimensions"]


def build_pytest_expression(filter_: dict) -> str:
    """`not <cap1> and not <cap2> ...` from `exclude_capabilities`, JSON order."""
    return " and ".join(f"not {name}" for name in filter_["exclude_capabilities"])


def build_nunit_flags(filter_: dict, dimensions: dict) -> list[str]:
    """`--category '!^<CsharpCategory>$'` per excluded capability that has a C#
    equivalent (a null csharp_category, e.g. `live`, has no NUnit meaning and
    is skipped); `--assembly <layer>` per `layers` entry."""
    flags: list[str] = []
    for name in filter_["exclude_capabilities"]:
        category = dimensions.get(name, {}).get("csharp_category")
        if category:
            flags += ["--category", f"!^{category}$"]
    for layer in filter_["layers"]:
        flags += ["--assembly", layer]
    return flags


def render_lane(fmt: str, lane_name: str, lanes: dict, dimensions: dict) -> str:
    if lane_name not in lanes:
        raise ValueError(f"unknown lane {lane_name!r}; known lanes: {sorted(lanes)}")
    filter_ = lanes[lane_name]["filter"]
    if fmt == "pytest":
        return build_pytest_expression(filter_)
    return " ".join(build_nunit_flags(filter_, dimensions))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("format", choices=("pytest", "nunit"))
    parser.add_argument("lane")
    args = parser.parse_args(argv)

    lanes = load_lanes()
    dimensions = load_dimensions()
    try:
        output = render_lane(args.format, args.lane, lanes, dimensions)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 1

    print(output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
