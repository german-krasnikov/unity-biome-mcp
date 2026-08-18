
import re
from typing import TYPE_CHECKING, Protocol

from gauntlet.model import (
    Contract,
    EffectDomain,
    Identity,
    ScenarioResult,
    Snapshot,
    ToolResult,
    Verdict,
)
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from gauntlet.receipts import ReceiptJournal


class ScenarioDriver(Protocol):
    async def identity(self) -> Identity: ...

    async def snapshot(self) -> Snapshot: ...

    async def call(self, action: str, arguments: dict[str, object]) -> ToolResult: ...


class ScenarioRunner:
    """Evaluate one public action against an independent transition contract."""

    def __init__(self, journal: ReceiptJournal) -> None:
        self._journal = journal

    async def run(self, contract: Contract, driver: ScenarioDriver) -> ScenarioResult:
        self._journal.append(
            "scenario_started",
            {"contract_id": contract.contract_id, "action": contract.action},
        )

        try:
            actual_identity = await driver.identity()
        except Exception as exc:  # noqa: BLE001 - boundary errors become receipts
            return self._finish(
                contract,
                Verdict.ERROR,
                (f"identity probe failed: {type(exc).__name__}",),
            )

        if (
            contract.required_project is not None
            and actual_identity.project_path != contract.required_project
        ):
            return self._finish(
                contract,
                Verdict.BLOCKED,
                ("identity mismatch: required project differs from connected project",),
            )

        try:
            before = await driver.snapshot()
        except Exception as exc:  # noqa: BLE001 - boundary errors become receipts
            return self._finish(
                contract,
                Verdict.ERROR,
                (f"pre-state probe failed: {type(exc).__name__}",),
            )

        if before.identity != actual_identity:
            return self._finish(
                contract,
                Verdict.BLOCKED,
                ("identity changed before dispatch",),
            )

        arguments = dict(contract.arguments)
        self._journal.append(
            "intent_recorded",
            {
                "contract_id": contract.contract_id,
                "action": contract.action,
                "argument_keys": sorted(arguments),
                "arguments_hash": content_hash(arguments),
            },
        )

        try:
            tool_result = await driver.call(contract.action, arguments)
        except Exception as exc:  # noqa: BLE001 - dispatch outcome is unknown
            return self._finish(
                contract,
                Verdict.ERROR,
                (
                    "public action raised before a result was observed: "
                    f"{type(exc).__name__}",
                ),
            )

        self._journal.append(
            "action_observed",
            {
                "contract_id": contract.contract_id,
                "is_error": tool_result.is_error,
                "code": tool_result.code,
                "text_hash": content_hash(tool_result.text),
                "text_bytes": len(tool_result.text.encode("utf-8")),
            },
        )

        try:
            after = await driver.snapshot()
        except Exception as exc:  # noqa: BLE001 - post-state is required evidence
            return self._finish(
                contract,
                Verdict.ERROR,
                (f"post-state probe failed: {type(exc).__name__}",),
            )

        reasons = self._evaluate(contract, before, after, tool_result)
        verdict = Verdict.FAIL if reasons else Verdict.PASS
        return self._finish(contract, verdict, tuple(reasons))

    def _finish(
        self,
        contract: Contract,
        verdict: Verdict,
        reasons: tuple[str, ...],
    ) -> ScenarioResult:
        self._journal.append(
            "scenario_finished",
            {
                "contract_id": contract.contract_id,
                "verdict": verdict.value,
                "reasons": list(reasons),
            },
        )
        return ScenarioResult(contract.contract_id, verdict, reasons)

    @staticmethod
    def _evaluate(
        contract: Contract,
        before: Snapshot,
        after: Snapshot,
        result: ToolResult,
    ) -> list[str]:
        reasons: list[str] = []

        if result.is_error != contract.expect_error:
            expected = "error" if contract.expect_error else "success"
            reasons.append(f"response envelope expected {expected}, is_error={result.is_error}")

        if not result.is_error:
            reasons.extend(
                f"response envelope reported success with forbidden text pattern {pattern!r}"
                for pattern in contract.forbidden_success_patterns
                if re.search(pattern, result.text, flags=re.IGNORECASE)
            )

        if before.identity != after.identity:
            reasons.append("identity changed across the public action")

        if (
            EffectDomain.PURE_READ in contract.effects
            and before.protected_hash != after.protected_hash
        ):
            reasons.append("pure-read contract changed protected state")

        return reasons
