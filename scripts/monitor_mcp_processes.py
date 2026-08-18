#!/usr/bin/env python3
"""Read-only monitor for likely stuck Unity MCP processes, locks and ports."""


import argparse
import json
import os
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Iterable


@dataclass(frozen=True)
class ProcessRow:
    pid: int
    ppid: int
    stat: str
    etimes: int
    pcpu: float
    pmem: float
    command: str


_ACCESS_TOKEN_RE = re.compile(r"(-accessToken\s+)(\S+)")
_MCP_COMMAND_RE = re.compile(r"(^|\s)(?:\S*/)?unity-biome-mcp(?:\s|$)")
_RELAY_COMMAND_RE = re.compile(r"(^|\s)(?:\S*/)?unity-biome-mcp-relay(?:\s|$)")
_SERVER_LOCK_RE = re.compile(r"server-\d+-(\d+)\.lock$")


def redact_command(command: str) -> str:
    return _ACCESS_TOKEN_RE.sub(r"\1<redacted>", command)


def _run(args: tuple[str, ...]) -> str:
    try:
        return subprocess.run(
            args,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
        ).stdout
    except FileNotFoundError:
        return ""


def parse_age_seconds(value: str) -> int:
    if value.isdigit():
        return int(value)
    day_split = value.split("-", maxsplit=1)
    days = 0
    clock = value
    if len(day_split) == 2:
        days = int(day_split[0])
        clock = day_split[1]
    parts = [int(part) for part in clock.split(":")]
    if len(parts) == 2:
        hours = 0
        minutes, seconds = parts
    elif len(parts) == 3:
        hours, minutes, seconds = parts
    else:
        raise ValueError(f"unsupported age: {value}")
    return days * 86400 + hours * 3600 + minutes * 60 + seconds


def parse_ps(output: str) -> list[ProcessRow]:
    rows: list[ProcessRow] = []
    for line in output.splitlines()[1:]:
        parts = line.strip().split(maxsplit=6)
        if len(parts) < 7:
            continue
        try:
            rows.append(
                ProcessRow(
                    pid=int(parts[0]),
                    ppid=int(parts[1]),
                    stat=parts[2],
                    etimes=parse_age_seconds(parts[3]),
                    pcpu=float(parts[4]),
                    pmem=float(parts[5]),
                    command=redact_command(parts[6]),
                )
            )
        except ValueError:
            continue
    return rows


def parse_lsof_pids(output: str) -> set[int]:
    pids: set[int] = set()
    for line in output.splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 2 and parts[1].isdigit():
            pids.add(int(parts[1]))
    return pids


def read_lock_pids(root: Path) -> set[int]:
    pids: set[int] = set()
    for path in root.glob("server-*-*.lock"):
        match = _SERVER_LOCK_RE.search(path.name)
        if match:
            pids.add(int(match.group(1)))
    return pids


def _is_mcp_process(command: str) -> bool:
    return bool(_MCP_COMMAND_RE.search(command) or _RELAY_COMMAND_RE.search(command))


def _is_version_probe(command: str) -> bool:
    return bool(_RELAY_COMMAND_RE.search(command)) and "--version" in command


def _is_server(command: str) -> bool:
    return bool(_MCP_COMMAND_RE.search(command)) and not _is_version_probe(command)


def classify_state(
    *,
    processes: Iterable[ProcessRow],
    live_pids: set[int],
    lock_pids: set[int],
    listener_pids: set[int],
    max_version_seconds: int,
) -> dict[str, object]:
    process_list = [row for row in processes if _is_mcp_process(row.command)]
    known_pids = {row.pid for row in process_list} | live_pids
    issues: list[dict[str, object]] = []
    active_servers: list[dict[str, object]] = []

    for row in process_list:
        if _is_version_probe(row.command) and row.etimes > max_version_seconds:
            issues.append(
                {
                    "kind": "stale_version_probe",
                    "pid": row.pid,
                    "age_seconds": row.etimes,
                    "command": row.command,
                }
            )
        elif _is_server(row.command):
            active_servers.append(
                {
                    "pid": row.pid,
                    "age_seconds": row.etimes,
                    "has_lock": row.pid in lock_pids,
                    "has_listener": row.pid in listener_pids,
                    "command": row.command,
                }
            )

    issues.extend(
        {"kind": "stale_server_lock", "pid": pid}
        for pid in sorted(lock_pids - known_pids)
    )

    return {
        "issues": issues,
        "active_servers": sorted(active_servers, key=lambda item: int(item["pid"])),
        "listener_pids": sorted(listener_pids),
        "lock_pids": sorted(lock_pids),
    }


def collect_state(max_version_seconds: int) -> dict[str, object]:
    home = Path(os.environ.get("HOME", "~")).expanduser()
    state_root = home / ".unity-biome-mcp"
    ps_output = _run(("ps", "-axo", "pid,ppid,stat,etime,pcpu,pmem,command"))
    try:
        lsof_output = _run(("lsof", "-nP", "-iTCP:9500-9699", "-sTCP:LISTEN"))
    except FileNotFoundError:
        lsof_output = ""
    processes = parse_ps(ps_output)
    live_pids = {row.pid for row in processes}
    return classify_state(
        processes=processes,
        live_pids=live_pids,
        lock_pids=read_lock_pids(state_root),
        listener_pids=parse_lsof_pids(lsof_output),
        max_version_seconds=max_version_seconds,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--max-version-seconds", type=int, default=60)
    args = parser.parse_args()
    state = collect_state(args.max_version_seconds)
    print(json.dumps(state, indent=2, sort_keys=True))
    return 2 if state["issues"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
