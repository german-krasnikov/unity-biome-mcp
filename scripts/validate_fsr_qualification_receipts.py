#!/usr/bin/env python3
"""P1-20 aggregate gate: exactly 6/6 unique PASS receipts on one SHA set,
and the CI commits staged on top of the frozen base never touched product
code (unity-plugin/** or server/src/**).

Reads one receipt.json per cell from `--receipts-root` (each cell's
downloaded `actions/upload-artifact` directory, e.g.
`receipts/fsr-qualification-<cell>/receipt.json`).
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "scripts"))
import gauntlet.fsr_qualification as fq  # noqa: E402


def _git_changed_paths(base_sha: str) -> list[str]:
    try:
        result = subprocess.run(
            ["git", "diff", "--name-only", f"{base_sha}..HEAD"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=True,
        )
    except subprocess.CalledProcessError as error:
        raise fq.FsrQualificationError(
            f"git diff --name-only {base_sha}..HEAD failed (exit {error.returncode}): "
            f"{error.stderr or error.stdout}"
        ) from error
    return [line for line in result.stdout.splitlines() if line]


def _load_receipts(receipts_root: Path) -> list[dict[str, object]]:
    return [
        json.loads(receipt_path.read_text(encoding="utf-8"))
        for receipt_path in sorted(receipts_root.glob("*/receipt.json"))
    ]


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipts-root", type=Path, required=True)
    parser.add_argument("--lock", type=Path, default=REPO_ROOT / "scripts" / "fsr_qualification_lock.json")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        lock = fq.load_lock(args.lock)
        receipts = _load_receipts(args.receipts_root)
        fq.validate_receipt_set(receipts, lock)
        fq.assert_base_sha_untouched(_git_changed_paths(lock["base_product_sha"]))
    except fq.FsrQualificationError as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    print(f"PASS: 6/6 unique PASS receipts bound to base {lock['base_product_sha']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
