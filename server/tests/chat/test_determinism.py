"""Determinism invariants — f(seed) reproducible across PYTHONHASHSEED.

Regression for SoM Tier 2c memorial fix: hash(path) was non-deterministic across
processes. assign_indices migrated to sha256. These tests pin the contract.
"""
import os
import subprocess
import sys

import pytest

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def _run(code: str, seed: int) -> str:
    env = {
        "PYTHONHASHSEED": str(seed),
        "PATH": os.environ.get("PATH", ""),
        "PYTHONPATH": os.path.join(PROJECT_ROOT, "src"),
    }
    out = subprocess.run(
        [sys.executable, "-c", code],
        env=env, capture_output=True, text=True, encoding="utf-8", check=True, timeout=10,
    )
    return out.stdout


@pytest.mark.parametrize("seed_a,seed_b", [(0, 999), (1, 42), (777, 123456)])
def test_assign_indices_stable_across_pythonhashseed(seed_a, seed_b):
    """SoM Tier 2c regression — assign_indices must use sha256, not hash()."""
    code = (
        "from unity_mcp.som.extract import assign_indices\n"
        "rects = [{'path': '/A'}, {'path': '/B/C'}, {'path': '/D'}, {'path': '/E/F/G'}]\n"
        "print([(i, r['path']) for i, r in assign_indices(rects)])\n"
    )
    a = _run(code, seed_a)
    b = _run(code, seed_b)
    assert a == b, f"non-deterministic (PYTHONHASHSEED={seed_a} vs {seed_b}):\n  {a}\n  {b}"
