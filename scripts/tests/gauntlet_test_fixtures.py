"""Neutral package and evidence fixtures shared by Gauntlet unit tests."""

from __future__ import annotations

import hashlib
import io
import json
import tarfile
import zipfile
from typing import TYPE_CHECKING
from xml.sax.saxutils import quoteattr

from gauntlet.receipts import ReceiptJournal, content_hash

if TYPE_CHECKING:
    from collections.abc import Callable, Iterable, Mapping
    from pathlib import Path


def write_wheel(
    path: Path,
    version: str = "1.27.0",
    *,
    name: str = "unity-biome-mcp",
) -> None:
    dist_info = f"{name.replace('-', '_')}-{version}.dist-info"
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            f"{dist_info}/METADATA",
            f"Metadata-Version: 2.1\nName: {name}\nVersion: {version}\n\n",
        )
        archive.writestr(
            f"{dist_info}/WHEEL",
            "Wheel-Version: 1.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        )
        archive.writestr("unity_mcp/__init__.py", "__version__ = 'test'\n")
        archive.writestr(f"{dist_info}/RECORD", "unity_mcp/__init__.py,,\n")


def write_upm(
    path: Path,
    version: str = "1.27.0",
    *,
    name: str = "com.unity-biome-mcp.editor",
) -> None:
    payload = json.dumps({"name": name, "version": version}).encode()
    info = tarfile.TarInfo("package/package.json")
    info.size = len(payload)
    with tarfile.open(path, "w:gz") as archive:
        archive.addfile(info, io.BytesIO(payload))


def write_junit(path: Path, scenario_ids: Iterable[str]) -> None:
    cases: list[str] = []
    for scenario_id in scenario_ids:
        classname, name = scenario_id.split("::", maxsplit=1)
        cases.append(f"  <testcase classname={quoteattr(classname)} name={quoteattr(name)} />")
    body = "\n".join(cases)
    path.write_text(
        f'<testsuite tests="{len(cases)}" failures="0" errors="0" skipped="0">\n{body}\n</testsuite>\n',
        encoding="utf-8",
    )


def write_attested_junit(
    path: Path,
    scenario_nodes: Iterable[tuple[str, str]],
) -> None:
    cases: list[str] = []
    for scenario_id, pytest_node_id in scenario_nodes:
        if "::" in scenario_id:
            classname, name = scenario_id.split("::", maxsplit=1)
        else:
            classname, name = "gauntlet", scenario_id
        cases.append(
            "\n".join(
                (
                    f"  <testcase classname={quoteattr(classname)} name={quoteattr(name)}>",
                    "    <properties>",
                    f"      <property name=\"gauntlet_scenario_id\" value={quoteattr(scenario_id)} />",
                    f"      <property name=\"gauntlet_pytest_node_id\" value={quoteattr(pytest_node_id)} />",
                    "    </properties>",
                    "  </testcase>",
                )
            )
        )
    body = "\n".join(cases)
    path.write_text(
        f'<testsuite tests="{len(cases)}" failures="0" errors="0" skipped="0">\n{body}\n</testsuite>\n',
        encoding="utf-8",
    )


def write_complete_journal(
    path: Path,
    scenario_ids: Iterable[str],
    *,
    run_id: str,
    run_manifest_sha: str,
    profile: str,
    timestamp: str,
    workers: Mapping[str, str] | None = None,
    lease_workers_after_scenarios: bool = False,
) -> None:
    scenarios = tuple(scenario_ids)
    journal = ReceiptJournal(path, run_id, clock=lambda: timestamp)
    journal.append(
        "run_started",
        {"profile": profile, "run_manifest_sha": run_manifest_sha},
    )
    worker_leases = sorted((workers or {}).items())
    if not lease_workers_after_scenarios:
        _append_worker_leases(journal, worker_leases)
    event_fields = {
        "identity_verified": "identity_hash",
        "scenario_started": "precondition_hash",
        "intent_recorded": "intent_hash",
        "request_transmitted": "request_hash",
        "action_observed": "response_hash",
        "post_state_observed": "state_hash",
        "cleanup_observed": "cleanup_hash",
        "scenario_finished": None,
    }
    for scenario_id in scenarios:
        for event_type, digest_field in event_fields.items():
            payload: dict[str, object] = {"contract_id": scenario_id}
            if digest_field is not None:
                payload[digest_field] = "2" * 64
            if event_type == "cleanup_observed":
                payload["clean"] = True
            if event_type == "scenario_finished":
                payload["verdict"] = "pass"
            journal.append(event_type, payload)
    if lease_workers_after_scenarios:
        _append_worker_leases(journal, worker_leases)
    journal.append(
        "run_finished",
        {
            "verdict": "pass",
            "scenario_count": len(scenarios),
            "scenario_manifest_sha": content_hash(sorted(scenarios)),
        },
    )


def write_json(path: Path, value: Mapping[str, object]) -> None:
    path.write_text(
        json.dumps(value, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )


def rewrite_journal_events(
    path: Path,
    mutate: Callable[[dict[str, object]], None],
) -> None:
    events = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
    previous_hash = "0" * 64
    rewritten: list[str] = []
    for event in events:
        mutate(event)
        event["prev_hash"] = previous_hash
        unhashed = {key: value for key, value in event.items() if key != "event_hash"}
        encoded = json.dumps(
            unhashed,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
        event["event_hash"] = hashlib.sha256(encoded).hexdigest()
        previous_hash = str(event["event_hash"])
        rewritten.append(json.dumps(event, separators=(",", ":"), sort_keys=True))
    path.write_text("\n".join(rewritten) + "\n", encoding="utf-8")


def _append_worker_leases(
    journal: ReceiptJournal,
    worker_leases: list[tuple[str, str]],
) -> None:
    for role, worker_id in worker_leases:
        journal.append(
            "worker_leased",
            {"role": role, "worker_id": worker_id, "lease_hash": "1" * 64},
        )
