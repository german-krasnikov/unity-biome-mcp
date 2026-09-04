"""Pure selection helpers for run_unity_tests.py.

Kept as a sibling module (not inlined in run_unity_tests.py) so the two
"selection" concerns -- parsing a --tests-file and reproducing Unity's
TestRunSelection.ComputeSha256 canonical form -- stay unit-testable without
growing the main script past its established size, mirroring how
scripts/tests/*.py already import small sibling support modules (e.g.
release_gate_test_support.py).
"""

from __future__ import annotations

import hashlib
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence
    from pathlib import Path


def parse_tests_file(path: Path) -> list[str]:
    """One full test name per line; blank lines and '#'-comment lines are
    stripped. Mirrors --tests-file's documented format."""
    lines = path.read_text(encoding="utf-8").splitlines()
    stripped = (line.strip() for line in lines)
    return [line for line in stripped if line and not line.startswith("#")]


def canonicalize_selection_list(values: Sequence[str]) -> str:
    """Ordinal-sorted, newline-joined form. Must match
    TestRunSelection.Canonicalize (TestRunSelection.cs) byte-for-byte --
    Python's default str sort is codepoint order, which equals C#'s
    StringComparer.Ordinal for the BMP characters exercised by
    TestRunServiceTests.ComputeSha256_KnownInput_MatchesFrozenVector."""
    return "\n".join(sorted(values))


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
