"""Pure selection helpers for run_unity_tests.py.

Kept as a sibling module (not inlined in run_unity_tests.py) so the two
"selection" concerns -- parsing a --tests-file and reproducing Unity's
TestRunSelection.ComputeSha256 canonical form -- stay unit-testable without
growing the main script past its established size, mirroring how
scripts/tests/*.py already import small sibling support modules (e.g.
release_gate_test_support.py).

Ordinal-sort equivalence caveat: Python's default str sort is codepoint
order; C#'s StringComparer.Ordinal sorts UTF-16 code units. These are
verified byte-for-byte identical only for BMP inputs (the frozen vectors in
TestRunServiceTests.ComputeSha256_KnownInput_MatchesFrozenVector /
test_selection_sha256_matches_csharp_vectors). Astral-plane characters
(surrogate pairs) are NOT verified and may sort differently between the two
languages.
"""

from __future__ import annotations

import hashlib
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence
    from pathlib import Path


def parse_tests_file(path: Path) -> list[str]:
    """One full test name per line; blank lines and '#'-comment lines are
    stripped, and duplicate lines are collapsed to their first occurrence
    (order-preserving) -- mirrors TestRunSelection.Canonicalize's
    ordinal-distinct dedupe so a repeated line never inflates the
    file-length --minimum-tests default or the wire selection payload."""
    lines = path.read_text(encoding="utf-8").splitlines()
    stripped = (line.strip() for line in lines)
    filtered = [line for line in stripped if line and not line.startswith("#")]
    return list(dict.fromkeys(filtered))


def canonicalize_selection_list(values: Sequence[str]) -> str:
    """Ordinal-distinct (deduped) + ordinal-sorted, newline-joined form.
    Must match TestRunSelection.Canonicalize (TestRunSelection.cs)
    byte-for-byte -- Python's default str sort is codepoint order, which
    equals C#'s StringComparer.Ordinal for the BMP characters exercised by
    TestRunServiceTests.ComputeSha256_KnownInput_MatchesFrozenVector.
    Astral/surrogate-pair ordering is NOT verified: C# sorts UTF-16 code
    units, which diverges from Python's codepoint-order sort outside the
    BMP."""
    return "\n".join(sorted(set(values)))


def compute_selection_sha256(
    mode: str,
    filter_name: str,
    group: str,
    categories: Sequence[str],
    assemblies: Sequence[str],
    tests: Sequence[str],
) -> str:
    """Reproduce TestRunSelection.ComputeSha256's canonical form
    byte-for-byte: "mode|filter|group|categories|assemblies|tests", each
    array canonicalized via canonicalize_selection_list, joined with "|" and
    hashed as UTF-8 SHA-256 hex."""
    canonical = "|".join(
        (
            mode or "",
            filter_name or "",
            group or "",
            canonicalize_selection_list(categories),
            canonicalize_selection_list(assemblies),
            canonicalize_selection_list(tests),
        )
    )
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()
