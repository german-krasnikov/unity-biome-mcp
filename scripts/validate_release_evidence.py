#!/usr/bin/env python3
"""Validate exact release artifacts and conformance evidence."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from gauntlet.release_gate import GateError, validate_release_gate


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Fail-closed release evidence validator",
    )
    parser.add_argument("--policy", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--artifact-manifest", type=Path, required=True)
    parser.add_argument("--artifact-root", type=Path, required=True)
    parser.add_argument("--head-sha", required=True)
    parser.add_argument(
        "--evidence",
        type=Path,
        action="append",
        default=[],
        help="Profile evidence file; repeat once per active profile",
    )
    parser.add_argument(
        "--player-playtest-evidence",
        type=Path,
        action="append",
        default=[],
        help="Player PlayTest evidence file; repeat once per required OS matrix",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        summary = validate_release_gate(
            policy_path=args.policy,
            source_root=args.source_root,
            artifact_manifest_path=args.artifact_manifest,
            artifact_root=args.artifact_root,
            evidence_paths=tuple(args.evidence),
            expected_head_sha=args.head_sha,
            player_playtest_evidence_paths=tuple(args.player_playtest_evidence),
        )
    except GateError as exc:
        print(f"RELEASE EVIDENCE FAIL: {exc}", file=sys.stderr)
        return 1

    print(
        "RELEASE EVIDENCE PASS: "
        f"product={summary.product_version} "
        f"profiles={len(summary.profiles)} "
        f"player_playtest={len(summary.player_playtest_matrices)} "
        f"manifest={summary.artifact_manifest_sha}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
