#!/usr/bin/env python3
"""Write built-player PlayTest evidence for CI artifacts."""


import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.json_io import JsonFileError, atomic_write_json  # noqa: E402
from gauntlet.player_playtest_evidence import (  # noqa: E402
    PlayerPlaytestEvidenceError,
    build_player_playtest_evidence,
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--head-sha", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-attempt", required=True)
    parser.add_argument("--runner-os", required=True)
    parser.add_argument("--matrix-name", required=True)
    parser.add_argument("--player-path", type=Path, required=True)
    parser.add_argument("--artifacts-dir", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()

    try:
        evidence = build_player_playtest_evidence(
            head_sha=args.head_sha,
            run_id=args.run_id,
            run_attempt=args.run_attempt,
            runner_os=args.runner_os,
            matrix_name=args.matrix_name,
            player_path=args.player_path,
            artifacts_dir=args.artifacts_dir,
        )
        atomic_write_json(args.out, evidence)
    except (JsonFileError, PlayerPlaytestEvidenceError) as exc:
        print(f"player PlayTest evidence error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
