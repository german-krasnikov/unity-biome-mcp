"""Strict reviewed contract catalog for deterministic Gauntlet scenarios."""


import re
from collections.abc import (
    Mapping,
    Set,  # noqa: TC003
)
from dataclasses import dataclass
from enum import StrEnum
from pathlib import Path  # noqa: TC003
from types import MappingProxyType

from gauntlet.json_io import JsonFileError, load_json_object, parse_json_object
from gauntlet.model import Contract, EffectDomain
from gauntlet.receipts import content_hash

_ROOT_KEYS = {
    "schema_version",
    "catalog_version",
    "scope",
    "owner",
    "contracts",
}
_CONTRACT_KEYS = {
    "id",
    "action",
    "effects",
    "retry",
    "arguments",
    "preconditions",
    "expect_error",
    "forbidden_success_patterns",
}
_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")


class CatalogError(ValueError):
    """Raised when a reviewed contract manifest is incomplete or unsafe."""


class CatalogScope(StrEnum):
    BUILTIN = "builtin"
    PLUGIN = "plugin"


class RetryPolicy(StrEnum):
    BLIND_SAFE = "blind_safe"
    RECONCILE = "reconcile"
    NEVER = "never"


@dataclass(frozen=True, slots=True)
class CatalogContract:
    contract_id: str
    action: str
    effects: frozenset[EffectDomain]
    retry: RetryPolicy
    arguments: Mapping[str, object]
    preconditions: Mapping[str, object]
    expect_error: bool
    forbidden_success_patterns: tuple[str, ...]

    def as_contract(self) -> Contract:
        return Contract(
            contract_id=self.contract_id,
            action=self.action,
            effects=self.effects,
            arguments=_thaw_json_object(self.arguments),
            expect_error=self.expect_error,
            forbidden_success_patterns=self.forbidden_success_patterns,
        )


@dataclass(frozen=True, slots=True)
class ContractCatalog:
    catalog_version: str
    scope: CatalogScope
    owner: str | None
    contracts: tuple[CatalogContract, ...]
    catalog_sha: str


def load_contract_catalog(path: Path) -> ContractCatalog:
    try:
        data = load_json_object(path)
    except JsonFileError as exc:
        raise CatalogError(str(exc)) from exc
    return _parse_contract_catalog(data)


def parse_contract_catalog(data: bytes, *, source: str) -> ContractCatalog:
    """Parse catalog bytes captured by the trusted source observer."""
    try:
        value = parse_json_object(data, source=source)
    except JsonFileError as exc:
        raise CatalogError(str(exc)) from exc
    return _parse_contract_catalog(value)


def _parse_contract_catalog(data: dict[str, object]) -> ContractCatalog:
    if set(data) != _ROOT_KEYS:
        raise CatalogError("contract catalog schema mismatch")
    if data["schema_version"] != 2:
        raise CatalogError("unsupported contract catalog schema")

    version = _require_text(data["catalog_version"], "catalog version")
    scope = _parse_scope(data["scope"])
    owner = _parse_owner(scope, data["owner"])
    contracts = _parse_contracts(data["contracts"])
    return ContractCatalog(
        catalog_version=version,
        scope=scope,
        owner=owner,
        contracts=contracts,
        catalog_sha=content_hash(_catalog_payload(version, scope, owner, contracts)),
    )


def validate_action_coverage(
    catalog: ContractCatalog,
    public_actions: Set[str],
) -> None:
    covered = {contract.action for contract in catalog.contracts}
    missing = sorted(public_actions - covered)
    extra = sorted(covered - public_actions)
    if missing:
        raise CatalogError(f"contract catalog is missing public actions: {missing}")
    if extra:
        raise CatalogError(f"contract catalog has extra actions: {extra}")


def _parse_contracts(value: object) -> tuple[CatalogContract, ...]:
    if not isinstance(value, list) or not value:
        raise CatalogError("contract catalog must contain at least one contract")
    contracts: list[CatalogContract] = []
    seen_ids: set[str] = set()
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != _CONTRACT_KEYS:
            raise CatalogError("contract schema mismatch")
        contract = _parse_contract(raw)
        if contract.contract_id in seen_ids:
            raise CatalogError(f"duplicate contract ID: {contract.contract_id}")
        seen_ids.add(contract.contract_id)
        contracts.append(contract)
    contracts.sort(key=lambda contract: contract.contract_id)
    return tuple(contracts)


def _parse_contract(data: dict[str, object]) -> CatalogContract:
    contract_id = _require_id(data["id"], "contract ID")
    action = _require_id(data["action"], "public action")
    effects = _parse_effects(data["effects"])
    retry = _parse_retry(data["retry"])
    if retry is RetryPolicy.BLIND_SAFE and effects != frozenset({EffectDomain.PURE_READ}):
        raise CatalogError("blind-safe retry is permitted only for pure-read contracts")

    arguments = _require_object(data["arguments"], "arguments")
    preconditions = _require_object(data["preconditions"], "preconditions")
    expect_error = data["expect_error"]
    if not isinstance(expect_error, bool):
        raise CatalogError("expect_error must be boolean")
    patterns = _parse_patterns(data["forbidden_success_patterns"])
    return CatalogContract(
        contract_id=contract_id,
        action=action,
        effects=effects,
        retry=retry,
        arguments=arguments,
        preconditions=preconditions,
        expect_error=expect_error,
        forbidden_success_patterns=patterns,
    )


def _parse_effects(value: object) -> frozenset[EffectDomain]:
    if not isinstance(value, list) or not value:
        raise CatalogError("contract effects must be a non-empty list")
    try:
        effects = frozenset(EffectDomain(item) for item in value)
    except (TypeError, ValueError) as exc:
        raise CatalogError("contract contains an unknown effect domain") from exc
    if len(effects) != len(value):
        raise CatalogError("contract effects contain a duplicate")
    if EffectDomain.PURE_READ in effects and len(effects) != 1:
        raise CatalogError("pure-read cannot be combined with mutating effects")
    return effects


def _parse_patterns(value: object) -> tuple[str, ...]:
    if not isinstance(value, list):
        raise CatalogError("forbidden success patterns must be a list")
    patterns: list[str] = []
    for pattern in value:
        if not isinstance(pattern, str) or not pattern:
            raise CatalogError("forbidden success pattern must be non-empty")
        try:
            re.compile(pattern)
        except re.error as exc:
            raise CatalogError("forbidden pattern is not a valid regular expression") from exc
        patterns.append(pattern)
    if len(set(patterns)) != len(patterns):
        raise CatalogError("forbidden success patterns contain a duplicate")
    return tuple(patterns)


def _parse_scope(value: object) -> CatalogScope:
    try:
        return CatalogScope(value)
    except (TypeError, ValueError) as exc:
        raise CatalogError("contract catalog scope is invalid") from exc


def _parse_owner(scope: CatalogScope, value: object) -> str | None:
    if scope is CatalogScope.BUILTIN:
        if value is not None:
            raise CatalogError("builtin catalog owner must be null")
        return None
    return _require_id(value, "plugin owner")


def _parse_retry(value: object) -> RetryPolicy:
    try:
        return RetryPolicy(value)
    except (TypeError, ValueError) as exc:
        raise CatalogError("contract retry policy is invalid") from exc


def _require_object(value: object, label: str) -> Mapping[str, object]:
    if not isinstance(value, dict) or any(not isinstance(key, str) for key in value):
        raise CatalogError(f"{label} must be a JSON object")
    return MappingProxyType({key: _freeze_json(item) for key, item in value.items()})


def _freeze_json(value: object) -> object:
    if isinstance(value, dict):
        return MappingProxyType({key: _freeze_json(item) for key, item in value.items()})
    if isinstance(value, list):
        return tuple(_freeze_json(item) for item in value)
    return value


def _thaw_json(value: object) -> object:
    if isinstance(value, Mapping):
        return {key: _thaw_json(item) for key, item in value.items()}
    if isinstance(value, tuple):
        return [_thaw_json(item) for item in value]
    return value


def _thaw_json_object(value: Mapping[str, object]) -> dict[str, object]:
    return {key: _thaw_json(item) for key, item in value.items()}


def _require_text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise CatalogError(f"{label} must be a non-empty string")
    return value


def _require_id(value: object, label: str) -> str:
    text = _require_text(value, label)
    if not _ID_PATTERN.fullmatch(text):
        raise CatalogError(f"{label} contains unsupported characters")
    return text


def _catalog_payload(
    version: str,
    scope: CatalogScope,
    owner: str | None,
    contracts: tuple[CatalogContract, ...],
) -> dict[str, object]:
    return {
        "schema_version": 2,
        "catalog_version": version,
        "scope": scope.value,
        "owner": owner,
        "contracts": [
            {
                "id": contract.contract_id,
                "action": contract.action,
                "effects": sorted(effect.value for effect in contract.effects),
                "retry": contract.retry.value,
                "arguments": _thaw_json_object(contract.arguments),
                "preconditions": _thaw_json_object(contract.preconditions),
                "expect_error": contract.expect_error,
                "forbidden_success_patterns": sorted(contract.forbidden_success_patterns),
            }
            for contract in contracts
        ],
    }
