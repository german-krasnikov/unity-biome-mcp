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


def main() -> int:
    parser = argparse.ArgumentParser(description="MCP conformance test runner")
    parser.add_argument("--port", type=int, default=9500, help="Unity MCP port (default: 9500)")
    parser.add_argument("--project", required=True, help="Unity project path")
    parser.add_argument("--second-port", type=int, default=0, help="Second Unity port for cross-project tests")
    parser.add_argument("--timeout", type=int, default=300, help="Pytest timeout in seconds (default: 300)")
    parser.add_argument("--markers", default="conformance and live", help="Pytest marker expression")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    args = parser.parse_args()

    if not 1 <= args.port <= 65535:
        print(f"ERROR: invalid port {args.port}", file=sys.stderr)
        return 1

    project = Path(args.project)
    if not (project / "Assets").is_dir():
        print(f"ERROR: {args.project} does not look like a Unity project (no Assets/ dir)", file=sys.stderr)
        return 1

    # Set env vars for the conformance fixtures
    env = os.environ.copy()
    env["UNITY_MCP_PORT"] = str(args.port)
    env["UNITY_MCP_PROJECT_PATH"] = str(project.resolve())
    if args.second_port:
        env["UNITY_MCP_SECOND_PORT"] = str(args.second_port)

    # Build pytest command
    server_tests = Path(__file__).resolve().parent.parent / "server" / "tests" / "conformance"
    if not server_tests.is_dir():
        print(f"ERROR: conformance test directory not found at {server_tests}", file=sys.stderr)
        return 1

    cmd = [
        sys.executable, "-m", "pytest",
        str(server_tests),
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
