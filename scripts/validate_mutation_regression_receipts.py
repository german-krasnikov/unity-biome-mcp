#!/usr/bin/env python3
"""Mutation regression lane aggregate gate: reads one receipt.json per cell
(each cell's downloaded `actions/upload-artifact` directory, e.g.
`receipts/mutation-regression-<cell>/receipt.json`) and asserts every one is
a clean, comparable PASS -- same checkout, same resolved provider SHA, a
requested ref matching the tracked pin, and both lanes green.

Sibling to validate_fsr_qualification_receipts.py: does NOT reuse
fq.assert_base_sha_untouched -- the mutation lane floats its provider pin on
`master` by design (Plans/MUTATION-REGRESSION-MODULE.md section 6), so there
is no frozen base SHA to guard against product drift. See
Plans/MUTATION-REGRESSION-MODULE.md section 7 "Not reused from FSR cell".
"""
import argparse
import json
import sys
from collections.abc import Mapping, Sequence  # noqa: TC003
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PIN = REPO_ROOT / "scripts" / "source_patch_provider_pin.json"
DEFAULT_REQUIRED_CELLS = ("min-macos-arm64", "min-linux-x64")
ARTIFACT_PREFIX = "mutation-regression-"


class MutationRegressionReceiptError(RuntimeError):
    pass


def _cell_name(receipt_dir_name: str) -> str:
    if receipt_dir_name.startswith(ARTIFACT_PREFIX):
        return receipt_dir_name[len(ARTIFACT_PREFIX):]
    return receipt_dir_name


def load_receipts(receipts_root: Path) -> dict[str, dict[str, object]]:
    receipts: dict[str, dict[str, object]] = {}
    for receipt_path in sorted(receipts_root.glob("*/receipt.json")):
        cell = _cell_name(receipt_path.parent.name)
        try:
            receipts[cell] = json.loads(receipt_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as error:
            raise MutationRegressionReceiptError(f"Malformed JSON in {receipt_path}: {error}") from error
    return receipts


def load_tracked_ref(pin_path: Path) -> str:
    payload = json.loads(pin_path.read_text(encoding="utf-8"))
    ref = payload.get("ref")
    if not ref:
        raise MutationRegressionReceiptError(f"Provider pin {pin_path} has no 'ref' field")
    return str(ref)


def validate_receipt_set(
    receipts: Mapping[str, Mapping[str, object]],
    *,
    required_cells: Sequence[str],
    tracked_ref: str,
    allow_lock_drift: bool = False,
) -> None:
    """Every receipt found must be a clean PASS: outcome PASS, requested_ref
    matching the tracked pin, both lanes with zero failures, a positive
    csharp_lane.expected_count, and (unless allow_lock_drift) no
    provider.lock_sha_mismatch. All receipts must also share one
    checkout_sha and one provider.resolved_sha, and every required cell
    must be present."""
    if not receipts:
        raise MutationRegressionReceiptError("No receipts found")

    missing = sorted(set(required_cells) - set(receipts))
    if missing:
        raise MutationRegressionReceiptError(f"Missing receipt(s) for required cell(s): {missing}")

    for cell, receipt in receipts.items():
        outcome = receipt.get("outcome")
        if outcome != "PASS":
            raise MutationRegressionReceiptError(f"Cell {cell!r} outcome is not PASS: {outcome!r}")

        provider = receipt.get("provider") or {}
        requested_ref = provider.get("requested_ref")
        if requested_ref != tracked_ref:
            raise MutationRegressionReceiptError(
                f"Cell {cell!r} provider.requested_ref {requested_ref!r} != tracked pin ref {tracked_ref!r}"
            )

        python_lane = receipt.get("python_lane") or {}
        if python_lane.get("failed") != 0:
            raise MutationRegressionReceiptError(
                f"Cell {cell!r} python_lane.failed != 0: {python_lane.get('failed')!r}"
            )
        python_passed = python_lane.get("passed")
        # passed < 1 also catches an all-skipped lane (0 failed, 0 passed) --
        # pytest exits 0 in that case, which is not a clean PASS.
        if not isinstance(python_passed, int) or isinstance(python_passed, bool) or python_passed < 1:
            raise MutationRegressionReceiptError(
                f"Cell {cell!r} python_lane.passed must be an int >= 1, got {python_passed!r}"
            )

        csharp_lane = receipt.get("csharp_lane") or {}
        if csharp_lane.get("failed") != 0:
            raise MutationRegressionReceiptError(
                f"Cell {cell!r} csharp_lane.failed != 0: {csharp_lane.get('failed')!r}"
            )
        expected_count = csharp_lane.get("expected_count")
        # expected_count < 1 also catches a provider-absent worker whose Mutation C#
        # assembly was excluded by its defineConstraints (0 tests discovered).
        if not isinstance(expected_count, int) or isinstance(expected_count, bool) or expected_count < 1:
            raise MutationRegressionReceiptError(
                f"Cell {cell!r} csharp_lane.expected_count must be an int >= 1, got {expected_count!r}"
            )

        if not allow_lock_drift:
            mismatch = provider.get("lock_sha_mismatch")
            if mismatch:
                raise MutationRegressionReceiptError(
                    f"Cell {cell!r} has provider.lock_sha_mismatch={mismatch!r} "
                    "(pass --allow-lock-drift to allow)"
                )

    checkout_shas = {receipt.get("checkout_sha") for receipt in receipts.values()}
    if len(checkout_shas) != 1:
        raise MutationRegressionReceiptError(
            f"Receipts ran against different checkout SHAs: {sorted(sha for sha in checkout_shas if sha)}"
        )

    resolved_shas = {(receipt.get("provider") or {}).get("resolved_sha") for receipt in receipts.values()}
    if len(resolved_shas) != 1:
        raise MutationRegressionReceiptError(
            f"Receipts resolved different provider SHAs: {sorted(sha for sha in resolved_shas if sha)}"
        )


def _print_table(receipts: Mapping[str, Mapping[str, object]]) -> None:
    header = f"{'cell':<20} {'outcome':<8} {'checkout_sha':<12} {'resolved_sha':<12} {'py_failed':<9} {'cs_failed':<9}"
    print(header)
    print("-" * len(header))
    for cell in sorted(receipts):
        receipt = receipts[cell]
        provider = receipt.get("provider") or {}
        python_lane = receipt.get("python_lane") or {}
        csharp_lane = receipt.get("csharp_lane") or {}
        checkout_sha = str(receipt.get("checkout_sha") or "")[:12]
        resolved_sha = str(provider.get("resolved_sha") or "")[:12]
        print(
            f"{cell:<20} {str(receipt.get('outcome')):<8} {checkout_sha:<12} {resolved_sha:<12} "
            f"{str(python_lane.get('failed')):<9} {str(csharp_lane.get('failed')):<9}"
        )


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipts-root", type=Path, required=True)
    parser.add_argument("--pin", type=Path, default=DEFAULT_PIN)
    parser.add_argument("--required-cells", type=str, default=",".join(DEFAULT_REQUIRED_CELLS))
    parser.add_argument("--allow-lock-drift", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    required_cells = [cell.strip() for cell in args.required_cells.split(",") if cell.strip()]
    try:
        receipts = load_receipts(args.receipts_root)
        _print_table(receipts)
        tracked_ref = load_tracked_ref(args.pin)
        validate_receipt_set(
            receipts, required_cells=required_cells, tracked_ref=tracked_ref, allow_lock_drift=args.allow_lock_drift
        )
    except MutationRegressionReceiptError as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    print(f"PASS: {len(receipts)}/{len(receipts)} receipts PASS, required cells present: {required_cells}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())


__all__ = [
    "MutationRegressionReceiptError",
    "load_receipts",
    "load_tracked_ref",
    "validate_receipt_set",
    "parse_args",
    "main",
]
