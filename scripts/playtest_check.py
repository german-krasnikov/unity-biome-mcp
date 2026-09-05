"""CI gate: validate a Player playtest receipt's step/failed counts against
its `.playtest` file's `@expect` header (B18's `playtest_header.scan`).

Replaces the two hardcoded `!= 14` / `!= 4` step-count literals that used to
live directly in `unity-player-playtest.yml`'s receipt-validation steps: the
expected counts now come from the fixture's own `# @expect steps=N
failed=M` header instead of a magic number in CI. A fixture with no
`@expect` header is not forced to gain one just to keep CI green — it is
reported as skipped, not failed (legacy-compatible).

Usage: python scripts/playtest_check.py <playtest_path> <receipt_json_path>
Exits 0 on pass or skip; exits 1 with a message naming the file and both
counts on a mismatch.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import playtest_header  # noqa: E402


def check(playtest_path: Path, receipt_path: Path) -> str:
    """Validate one receipt against its `.playtest`'s `@expect` header.

    Returns a human-readable pass/skip message. Raises ValueError, naming
    the file and both counts, on a mismatch."""
    header = playtest_header.scan(playtest_path.read_text(encoding="utf-8"))
    if header.expect_steps is None:
        return f"{playtest_path.name}: no @expect, skipped"

    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    actual_steps = len(receipt.get("steps", []))
    if actual_steps != header.expect_steps:
        raise ValueError(
            f"{playtest_path.name}: expected {header.expect_steps} steps, "
            f"receipt has {actual_steps}"
        )

    if header.expect_failed is not None:
        actual_failed = receipt.get("failed", 0)
        if actual_failed != header.expect_failed:
            raise ValueError(
                f"{playtest_path.name}: expected {header.expect_failed} failed, "
                f"receipt has {actual_failed}"
            )

    return f"{playtest_path.name}: steps={actual_steps} OK"


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: playtest_check.py <playtest_path> <receipt_json_path>", file=sys.stderr)
        return 2
    try:
        print(check(Path(argv[0]), Path(argv[1])))
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
