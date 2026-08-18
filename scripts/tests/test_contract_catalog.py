"""Machine-readable Contract Gauntlet catalog tests."""


import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from gauntlet.contract_catalog import (  # noqa: E402
    CatalogError,
    RetryPolicy,
    load_contract_catalog,
    validate_action_coverage,
)
from gauntlet.model import EffectDomain  # noqa: E402


def _data() -> dict[str, object]:
    return {
        "schema_version": 2,
        "catalog_version": "1.0.0",
        "scope": "builtin",
        "owner": None,
        "contracts": [
            {
                "id": "status-clean-read",
                "action": "mcp_status",
                "effects": ["pure_read"],
                "retry": "blind_safe",
                "arguments": {},
                "preconditions": {"connected": True},
                "expect_error": False,
                "forbidden_success_patterns": ["^error:"],
            },
            {
                "id": "create-object-once",
                "action": "create_object",
                "effects": ["unity_persistent"],
                "retry": "reconcile",
                "arguments": {"name": "gauntlet-owned-object"},
                "preconditions": {"editor_state": "stopped_clean"},
                "expect_error": False,
                "forbidden_success_patterns": [],
            },
        ],
    }


def _write(path: Path, data: dict[str, object]) -> Path:
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


def test_catalog_loads_typed_contracts_and_stable_digest(tmp_path: Path) -> None:
    first = load_contract_catalog(_write(tmp_path / "first.json", _data()))
    second_path = tmp_path / "second.json"
    second_path.write_text(json.dumps(_data(), indent=4), encoding="utf-8")
    second = load_contract_catalog(second_path)
    reordered_data = _data()
    reordered_data["contracts"] = list(reversed(reordered_data["contracts"]))
    reordered = load_contract_catalog(
        _write(tmp_path / "reordered.json", reordered_data)
    )

    assert first.catalog_sha == second.catalog_sha == reordered.catalog_sha
    by_id = {contract.contract_id: contract for contract in first.contracts}
    assert by_id["status-clean-read"].effects == frozenset({EffectDomain.PURE_READ})
    assert by_id["status-clean-read"].retry is RetryPolicy.BLIND_SAFE
    assert by_id["create-object-once"].as_contract().arguments == {
        "name": "gauntlet-owned-object"
    }


def test_catalog_coverage_requires_exact_public_action_set(tmp_path: Path) -> None:
    catalog = load_contract_catalog(_write(tmp_path / "catalog.json", _data()))

    validate_action_coverage(catalog, {"mcp_status", "create_object"})
    with pytest.raises(CatalogError, match="missing"):
        validate_action_coverage(
            catalog,
            {"mcp_status", "create_object", "new_public_tool"},
        )
    with pytest.raises(CatalogError, match="extra"):
        validate_action_coverage(catalog, {"mcp_status"})


def test_catalog_contract_data_is_deeply_immutable_and_thawed_per_execution(tmp_path: Path) -> None:
    data = _data()
    data["contracts"][1]["arguments"] = {
        "nested": {"safe": True},
        "sequence": [{"value": 1}],
    }
    catalog = load_contract_catalog(_write(tmp_path / "catalog.json", data))
    catalog_contract = next(
        contract for contract in catalog.contracts if contract.contract_id == "create-object-once"
    )

    with pytest.raises(TypeError):
        catalog_contract.arguments["added"] = True  # type: ignore[index]
    with pytest.raises(TypeError):
        catalog_contract.arguments["nested"]["safe"] = False  # type: ignore[index]

    first_execution = catalog_contract.as_contract()
    first_execution.arguments["nested"]["safe"] = False  # type: ignore[index]
    first_execution.arguments["sequence"].append({"value": 2})  # type: ignore[union-attr]
    second_execution = catalog_contract.as_contract()

    assert second_execution.arguments == {
        "nested": {"safe": True},
        "sequence": [{"value": 1}],
    }
    assert catalog.catalog_sha == load_contract_catalog(
        _write(tmp_path / "catalog-copy.json", data)
    ).catalog_sha


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda data: data.update({"unknown": True}), "schema"),
        (lambda data: data.update({"contracts": []}), "contract"),
        (lambda data: data["contracts"].append(dict(data["contracts"][0])), "duplicate"),
        (lambda data: data["contracts"][0].update({"effects": []}), "effect"),
        (lambda data: data["contracts"][0].update({"effects": ["guess"]}), "effect"),
        (
            lambda data: data["contracts"][1].update({"retry": "blind_safe"}),
            "blind-safe",
        ),
        (
            lambda data: data["contracts"][0].update(
                {"effects": ["pure_read", "filesystem"]}
            ),
            "pure-read",
        ),
        (
            lambda data: data["contracts"][0].update(
                {"forbidden_success_patterns": ["("]}
            ),
            "regular expression",
        ),
        (lambda data: data.update({"scope": "plugin", "owner": None}), "owner"),
    ],
)
def test_catalog_rejects_ambiguous_or_unsafe_contracts(
    tmp_path: Path,
    mutate: object,
    message: str,
) -> None:
    data = _data()
    assert callable(mutate)
    mutate(data)

    with pytest.raises(CatalogError, match=message):
        load_contract_catalog(_write(tmp_path / "catalog.json", data))
