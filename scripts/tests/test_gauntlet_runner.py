
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.model import (  # noqa: E402
    Contract,
    EffectDomain,
    Identity,
    Snapshot,
    ToolResult,
    Verdict,
)
from gauntlet.receipts import ReceiptJournal, verify_journal  # noqa: E402
from gauntlet.runner import ScenarioRunner  # noqa: E402

pytestmark = pytest.mark.asyncio


class FakeDriver:
    def __init__(
        self,
        *,
        identity: Identity,
        before: Snapshot,
        after: Snapshot,
        result: ToolResult,
    ) -> None:
        self._identity = identity
        self._snapshots = iter((before, after))
        self._result = result
        self.calls: list[tuple[str, dict[str, object]]] = []

    async def identity(self) -> Identity:
        return self._identity

    async def snapshot(self) -> Snapshot:
        return next(self._snapshots)

    async def call(self, action: str, arguments: dict[str, object]) -> ToolResult:
        self.calls.append((action, arguments))
        return self._result


def _identity(project: str = "/worker-a") -> Identity:
    return Identity(
        worker_id="worker-a",
        project_path=project,
        port=9500,
        protocol_version="3",
        plugin_version="1.26.0",
        server_version="1.26.0",
        source_sha="abc123",
    )


def _snapshot(identity: Identity, protected_hash: str = "same") -> Snapshot:
    return Snapshot(identity=identity, protected_hash=protected_hash, state={"playing": False})


async def test_runner_blocks_before_dispatch_on_identity_mismatch(tmp_path: Path) -> None:
    actual = _identity("/worker-b")
    driver = FakeDriver(
        identity=actual,
        before=_snapshot(actual),
        after=_snapshot(actual),
        result=ToolResult(is_error=False, text="ok"),
    )
    contract = Contract(
        contract_id="identity-gate",
        action="mcp_status",
        effects=frozenset({EffectDomain.PURE_READ}),
        required_project="/worker-a",
    )

    result = await ScenarioRunner(ReceiptJournal(tmp_path / "r.jsonl", "run-1")).run(
        contract, driver
    )

    assert result.verdict is Verdict.BLOCKED
    assert driver.calls == []
    assert "identity" in " ".join(result.reasons).lower()


async def test_runner_fails_when_read_contract_mutates_protected_state(tmp_path: Path) -> None:
    identity = _identity()
    driver = FakeDriver(
        identity=identity,
        before=_snapshot(identity, "before"),
        after=_snapshot(identity, "after"),
        result=ToolResult(is_error=False, text="ok"),
    )
    contract = Contract(
        contract_id="read-does-not-mutate",
        action="get_hierarchy",
        effects=frozenset({EffectDomain.PURE_READ}),
    )

    result = await ScenarioRunner(ReceiptJournal(tmp_path / "r.jsonl", "run-1")).run(
        contract, driver
    )

    assert result.verdict is Verdict.FAIL
    assert "protected" in " ".join(result.reasons).lower()


async def test_runner_fails_false_success_envelope(tmp_path: Path) -> None:
    identity = _identity()
    driver = FakeDriver(
        identity=identity,
        before=_snapshot(identity),
        after=_snapshot(identity),
        result=ToolResult(is_error=False, text="Cannot instantiate: Transform"),
    )
    contract = Contract(
        contract_id="schema-transform-negative",
        action="get_schema",
        effects=frozenset({EffectDomain.PURE_READ}),
        forbidden_success_patterns=(r"^Cannot instantiate:",),
    )

    result = await ScenarioRunner(ReceiptJournal(tmp_path / "r.jsonl", "run-1")).run(
        contract, driver
    )

    assert result.verdict is Verdict.FAIL
    assert "envelope" in " ".join(result.reasons).lower()


async def test_runner_passes_clean_read_and_records_intent_before_result(tmp_path: Path) -> None:
    identity = _identity()
    driver = FakeDriver(
        identity=identity,
        before=_snapshot(identity),
        after=_snapshot(identity),
        result=ToolResult(is_error=False, text="scene=GridTest"),
    )
    contract = Contract(
        contract_id="status-smoke",
        action="mcp_status",
        effects=frozenset({EffectDomain.PURE_READ}),
    )
    receipt = tmp_path / "r.jsonl"

    result = await ScenarioRunner(ReceiptJournal(receipt, "run-1")).run(contract, driver)

    assert result.verdict is Verdict.PASS
    assert driver.calls == [("mcp_status", {})]
    event_types = [event["event_type"] for event in verify_journal(receipt)]
    assert event_types == [
        "scenario_started",
        "intent_recorded",
        "action_observed",
        "scenario_finished",
    ]


async def test_runner_receipt_does_not_store_raw_arguments_or_response(tmp_path: Path) -> None:
    identity = _identity()
    secret_argument = "credential-that-must-not-enter-artifacts"
    private_response = "private response content that must stay in memory"
    driver = FakeDriver(
        identity=identity,
        before=_snapshot(identity),
        after=_snapshot(identity),
        result=ToolResult(is_error=False, text=private_response),
    )
    contract = Contract(
        contract_id="artifact-redaction",
        action="ask",
        arguments={"api_key": secret_argument, "prompt": "private prompt"},
        effects=frozenset({EffectDomain.EXTERNAL_SERVICE}),
    )
    receipt = tmp_path / "r.jsonl"

    result = await ScenarioRunner(ReceiptJournal(receipt, "run-1")).run(contract, driver)

    assert result.verdict is Verdict.PASS
    raw_receipt = receipt.read_text(encoding="utf-8")
    assert secret_argument not in raw_receipt
    assert "private prompt" not in raw_receipt
    assert private_response not in raw_receipt
    events = verify_journal(receipt)
    intent = next(event for event in events if event["event_type"] == "intent_recorded")
    observed = next(event for event in events if event["event_type"] == "action_observed")
    assert intent["payload"]["argument_keys"] == ["api_key", "prompt"]
    assert len(intent["payload"]["arguments_hash"]) == 64
    assert len(observed["payload"]["text_hash"]) == 64
