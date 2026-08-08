#!/usr/bin/env python3
"""Delta-debug a JSONL trace file to the minimal failing subset.

Works on static trace files — no live server replay.
Finds the smallest subset of steps that still contains the failure pattern.

Usage:
    python scripts/minimize_repro.py trace.jsonl --output minimal.jsonl
    python scripts/minimize_repro.py trace.jsonl --output minimal.jsonl --fail-on-cmd get_hierarchy
    python scripts/minimize_repro.py trace.jsonl --output minimal.jsonl --fail-fn my_check.py
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Callable


def load_steps(path: str) -> list[dict]:
    lines = Path(path).read_text(encoding="utf-8").splitlines()
    return [json.loads(line) for line in lines if line.strip()]


def save_steps(steps: list[dict], path: str) -> None:
    content = "\n".join(json.dumps(s) for s in steps)
    Path(path).write_text(content + "\n" if steps else "", encoding="utf-8")


def make_criterion(args) -> Callable[[list[dict]], bool]:
    if getattr(args, "fail_fn", None):
        spec = importlib.util.spec_from_file_location("fail_fn_mod", args.fail_fn)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)  # type: ignore[union-attr]
        return mod.is_failure

    if getattr(args, "fail_on_cmd", None):
        cmd = args.fail_on_cmd
        return lambda steps: any(s["cmd"] == cmd and not s.get("ok", True) for s in steps)

    # Default: any step with ok=false
    return lambda steps: any(not s.get("ok", True) for s in steps)


def _dd(steps: list[dict], criterion: Callable[[list[dict]], bool]) -> list[dict]:
    """Recursively find the 1-minimal failing subset."""
    if not steps:
        return []
    if len(steps) == 1:
        return steps if criterion(steps) else []

    mid = len(steps) // 2
    first, second = steps[:mid], steps[mid:]

    if criterion(first):
        return _dd(first, criterion)
    if criterion(second):
        return _dd(second, criterion)

    # Neither half fails alone — try removing one step at a time
    for i in range(len(steps)):
        candidate = steps[:i] + steps[i + 1:]
        if criterion(candidate):
            return _dd(candidate, criterion)

    return steps  # already 1-minimal


def minimize(steps: list[dict], criterion: Callable[[list[dict]], bool]) -> list[dict]:
    """Return minimal subset of steps that still satisfies criterion.

    If the full trace does not fail, returns the original steps unchanged.
    """
    if not steps or not criterion(steps):
        return steps
    return _dd(steps, criterion)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Delta-debug a JSONL trace to the minimal failing subset"
    )
    parser.add_argument("trace", help="Input JSONL trace file")
    parser.add_argument("--output", required=True, help="Output JSONL file for minimized trace")

    group = parser.add_mutually_exclusive_group()
    group.add_argument(
        "--fail-on-error", action="store_true",
        help="Failure = any step with ok=false (default behaviour)",
    )
    group.add_argument(
        "--fail-on-cmd", metavar="CMD",
        help="Failure = this command returns ok=false",
    )
    group.add_argument(
        "--fail-fn", metavar="FILE",
        help="Path to Python file with is_failure(steps: list[dict]) -> bool",
    )

    args = parser.parse_args()
    steps = load_steps(args.trace)
    criterion = make_criterion(args)
    result = minimize(steps, criterion)
    save_steps(result, args.output)

    print(f"Minimized: {len(steps)} → {len(result)} steps")
    if not result and steps:
        print("No failure found in trace — output matches input.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
