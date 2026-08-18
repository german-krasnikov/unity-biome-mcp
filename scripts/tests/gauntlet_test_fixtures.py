"""Neutral package and evidence fixtures shared by Gauntlet unit tests."""


import base64
import csv
import gzip
import hashlib
import io
import json
import tarfile
import zipfile
from collections.abc import Callable, Iterable, Mapping  # noqa: TC003
from pathlib import Path  # noqa: TC003
from xml.sax.saxutils import quoteattr

from gauntlet.receipts import ReceiptJournal, content_hash


def write_wheel(
    path: Path,
    version: str = "1.27.0",
    *,
    name: str = "unity-biome-mcp",
) -> None:
    dist_info = f"{name.replace('-', '_')}-{version}.dist-info"
    members = {
        f"{dist_info}/METADATA": (
            f"Metadata-Version: 2.1\nName: {name}\nVersion: {version}\n\n"
        ).encode(),
        f"{dist_info}/WHEEL": (
            b"Wheel-Version: 1.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n"
        ),
        "unity_mcp/__init__.py": b"__version__ = 'test'\n",
    }
    record = io.StringIO(newline="")
    writer = csv.writer(record, lineterminator="\n")
    for member_name, payload in sorted(members.items()):
        encoded = base64.urlsafe_b64encode(hashlib.sha256(payload).digest()).rstrip(b"=")
        writer.writerow((member_name, f"sha256={encoded.decode('ascii')}", len(payload)))
    record_name = f"{dist_info}/RECORD"
    writer.writerow((record_name, "", ""))
    members[record_name] = record.getvalue().encode()
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for member_name, payload in members.items():
            info = zipfile.ZipInfo(member_name)
            info.date_time = (2020, 2, 2, 0, 0, 0)
            mode = 0o100644 if member_name.startswith("unity_mcp/") else 0o644
            info.external_attr = mode << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, payload)


def write_upm(
    path: Path,
    version: str = "1.27.0",
    *,
    name: str = "com.unity-biome-mcp.editor",
) -> None:
    payload = json.dumps({"name": name, "version": version}).encode()
    info = tarfile.TarInfo("package/package.json")
    info.size = len(payload)
    info.mtime = 499_162_500
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        archive.addfile(info, io.BytesIO(payload))
    compressed = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=compressed, mtime=0) as stream:
        stream.write(output.getvalue())
    path.write_bytes(compressed.getvalue())


def write_release_artifacts(
    root: Path,
    product_version: str = "1.27.0",
    reload_version: str = "0.1.4",
) -> dict[str, Path]:
    root.mkdir(parents=True, exist_ok=True)
    wheel = root / f"unity_biome_mcp-{product_version}-py3-none-any.whl"
    editor = root / f"com.unity-biome-mcp.editor-{product_version}.tgz"
    reload = root / f"com.unity-biome-mcp.reload-{reload_version}.tgz"
    write_wheel(wheel, product_version)
    write_upm(editor, product_version, name="com.unity-biome-mcp.editor")
    write_upm(reload, reload_version, name="com.unity-biome-mcp.reload")
    return {
        "python_wheel": wheel,
        "unity_editor_upm": editor,
        "unity_reload_upm": reload,
    }


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
