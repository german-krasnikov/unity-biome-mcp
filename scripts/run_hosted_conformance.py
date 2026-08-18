#!/usr/bin/env python3
"""Run live MCP conformance on disposable Unity projects in hosted CI."""


import argparse
import sys
from pathlib import Path

from create_unity_test_worker import DEFAULT_SOURCE_PROJECT
from gauntlet.hosted_conformance import (
    HostedConformanceError,
    WorkerCreationError,
    run_hosted_conformance,
)

DEFAULT_PORT_A = 9600
DEFAULT_PORT_B = 9699


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--source-project", type=Path, default=DEFAULT_SOURCE_PROJECT)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--reports", type=Path, default=Path("reports"))
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port-a", type=int, default=DEFAULT_PORT_A)
    parser.add_argument("--port-b", type=int, default=DEFAULT_PORT_B)
    parser.add_argument("--startup-timeout", type=int, default=420)
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--verbose", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if not args.unity.is_file():
        print(f"ERROR: Unity executable is missing: {args.unity}", file=sys.stderr)
        return 1
    if args.work_root.exists():
        print(f"ERROR: work root already exists: {args.work_root}", file=sys.stderr)
        return 1
    try:
        return run_hosted_conformance(
            unity=args.unity,
            source_project=args.source_project,
            work_root=args.work_root,
            reports=args.reports,
            host=args.host,
            port_a=args.port_a,
            port_b=args.port_b,
            startup_timeout=args.startup_timeout,
            timeout=args.timeout,
            verbose=args.verbose,
        )
    except (OSError, WorkerCreationError, HostedConformanceError) as exc:
        print(f"ERROR: hosted conformance failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
