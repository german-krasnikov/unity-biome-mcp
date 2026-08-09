#!/usr/bin/env python3
"""Run MCP conformance tests against a live Unity+MCP endpoint.

Usage:
    python scripts/conformance_runner.py --port 9500 --project /path/to/UnityProject
    python scripts/conformance_runner.py --port 9500 --project /path --second-port 9548
    python scripts/conformance_runner.py --port 9500 --project /path --markers "conformance and not cross_project"
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path

DEFAULT_MARKERS = "conformance and live and not requires_graphics"


def build_env(args) -> dict[str, str]:
    """Build the subprocess environment dict from parsed args."""
    env = os.environ.copy()
    env["UNITY_MCP_PORT"] = str(args.port)
    env["UNITY_MCP_PROJECT_PATH"] = str(Path(args.project).resolve())
    if getattr(args, "second_port", 0):
        env["UNITY_MCP_SECOND_PORT"] = str(args.second_port)
    if getattr(args, "second_project", ""):
        env["UNITY_MCP_SECOND_PROJECT_PATH"] = str(Path(args.second_project).resolve())
    if getattr(args, "record", None):
        env["UNITY_MCP_TRACE_FILE"] = args.record
    return env


def main() -> int:
    parser = argparse.ArgumentParser(description="MCP conformance test runner")
    parser.add_argument("--port", type=int, default=9500, help="Unity MCP port (default: 9500)")
    parser.add_argument("--project", required=True, help="Unity project path")
    parser.add_argument("--second-port", type=int, default=0, help="Second Unity port for cross-project tests")
    parser.add_argument("--second-project", default="", help="Second Unity project path (Worker B)")
    parser.add_argument("--timeout", type=int, default=300, help="Pytest timeout in seconds (default: 300)")
    parser.add_argument("--markers", default=DEFAULT_MARKERS, help="Pytest marker expression")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--record", metavar="FILE", help="Record trace to JSONL file (sets UNITY_MCP_TRACE_FILE)")
    args = parser.parse_args()

    if not 1 <= args.port <= 65535:
        print(f"ERROR: invalid port {args.port}", file=sys.stderr)
        return 1

    project = Path(args.project)
    if not (project / "Assets").is_dir():
        print(f"ERROR: {args.project} does not look like a Unity project (no Assets/ dir)", file=sys.stderr)
        return 1

    env = build_env(args)

    # Build pytest command
    tests_root = Path(__file__).resolve().parent.parent / "server" / "tests"
    test_dirs = [tests_root / "conformance"]
    if args.second_port:
        test_dirs.append(tests_root / "cross_project")

    for d in test_dirs:
        if not d.is_dir():
            print(f"ERROR: test directory not found at {d}", file=sys.stderr)
            return 1

    cmd = [
        sys.executable, "-m", "pytest",
        *[str(d) for d in test_dirs],
        "-m", args.markers,
        f"--timeout={args.timeout}",
    ]
    if args.verbose:
        cmd.append("-v")
    else:
        cmd.append("-q")

    print(f"Running conformance tests against :{args.port} ({args.project})")
    print(f"Command: {' '.join(cmd)}")

    try:
        result = subprocess.run(cmd, env=env, timeout=args.timeout + 60)
    except subprocess.TimeoutExpired:
        print("ERROR: conformance runner timed out", file=sys.stderr)
        return 1
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
