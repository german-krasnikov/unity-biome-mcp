"""Live parametric conformance tests loaded from generated YAML files.

This file never changes when new tests are added — only YAML files do.
Requires: conformance_worker fixture (see conftest.py), YAML files in generated/

Markers: live + conformance (inherited via pytestmark).
"""

import pathlib
from typing import Any

import pytest
import yaml

from tests.seams.invariants import parse_batch_result

pytestmark = [
    pytest.mark.live,
    pytest.mark.conformance,
    pytest.mark.asyncio(loop_scope="session"),
]

_GEN_DIR = pathlib.Path(__file__).parent / "generated"


def _load(filename: str) -> list[dict]:
    path = _GEN_DIR / filename
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    return data["tests"]


def _sub(obj: Any, ns: str) -> Any:
    """Recursively replace $NS$ with the worker's scene namespace."""
    if isinstance(obj, str):
        return obj.replace("$NS$", ns)
    if isinstance(obj, dict):
        return {k: _sub(v, ns) for k, v in obj.items()}
    if isinstance(obj, list):
        return [_sub(item, ns) for item in obj]
    return obj


SCHEMA_VALID   = _load("schema_valid.yaml")
SCHEMA_INVALID = _load("schema_invalid.yaml")
SEAM_TESTS     = _load("seam_tests.yaml")
BATCH_TESTS    = _load("batch_tests.yaml")


@pytest.mark.parametrize("case", SCHEMA_VALID, ids=[c["id"] for c in SCHEMA_VALID])
async def test_schema_valid(conformance_worker, case: dict) -> None:
    worker, bridge = conformance_worker
    args = _sub(case["args"], worker.scene_ns)
    resp = await bridge.send(case["cmd"], args)
    try:
        # schema_valid verifies the tool is reachable and routes correctly —
        # not that minimal args produce a perfect result. Any well-formed
        # response (ok=true OR ok=false with err) is a pass.
        assert "ok" in resp, f"Malformed response (no 'ok' key): {resp!r:.200}"
        assert "data" in resp or "err" in resp, (
            f"Malformed response (no 'data'/'err'): {resp!r:.200}"
        )
    finally:
        for cleanup in _sub(case.get("cleanup", []), worker.scene_ns):
            await bridge.send(cleanup["cmd"], cleanup["args"])


@pytest.mark.parametrize("case", SCHEMA_INVALID, ids=[c["id"] for c in SCHEMA_INVALID])
async def test_schema_invalid(conformance_worker, case: dict) -> None:
    worker, bridge = conformance_worker
    resp = await bridge.send(case["cmd"], case["args"])
    assert "ok" in resp, (
        f"Malformed response from {case['cmd']} with missing '{case.get('missing_field', '?')}': {resp!r:.200}"
    )


@pytest.mark.parametrize("case", SEAM_TESTS, ids=[c["id"] for c in SEAM_TESTS])
async def test_seam(conformance_worker, case: dict) -> None:
    worker, bridge = conformance_worker
    ns = worker.scene_ns
    cleanup = _sub(case.get("cleanup", []), ns)
    try:
        for step in _sub(case["steps"], ns):
            resp = await bridge.send(step["cmd"], step["args"])
            if step.get("expect_ok") is not None:
                assert resp.get("ok") == step["expect_ok"], (
                    f"Step {step['cmd']} expected ok={step['expect_ok']}: "
                    f"{resp.get('err', resp)!r:.200}"
                )
            if "expect_data_contains" in step:
                assert step["expect_data_contains"] in resp.get("data", ""), (
                    f"Step {step['cmd']}: '{step['expect_data_contains']}' "
                    f"not in data: {resp.get('data', '')!r:.200}"
                )
            if "expect_data_not_contains" in step:
                assert step["expect_data_not_contains"] not in resp.get("data", "")
    finally:
        for c in cleanup:
            await bridge.send(c["cmd"], c["args"])


@pytest.mark.parametrize("case", BATCH_TESTS, ids=[c["id"] for c in BATCH_TESTS])
async def test_batch_generated(conformance_worker, case: dict) -> None:
    worker, bridge = conformance_worker
    resp = await bridge.send(case["cmd"], case["args"])
    if case.get("expect_ok") is not None:
        assert resp.get("ok") == case["expect_ok"]
    data = resp.get("data", "") or resp.get("err", "")
    for key in ("expect_data_contains", "expect_data_also_contains"):
        if case.get(key):
            assert case[key] in data, (
                f"{key} '{case[key]}' not found in: {data!r:.300}"
            )
    if case.get("expect_result_count"):
        result = parse_batch_result(data)
        assert result.n == case["expect_result_count"], (
            f"Expected {case['expect_result_count']} results, "
            f"got ok:{result.ok_count} err:{result.err_count}"
        )
