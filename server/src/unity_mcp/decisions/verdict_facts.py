"""The facts of the playtest verdict, read from a report, and the calls of the decisions that judge them (plane `server`).

The rules are dsl/decisions/verdict.logic, printed into generated/verdict_logic.py; the facts are verdict.contract. This module is the
mechanical half: it reads a report into those facts the way the Python side always read it -- the PLAYTEST: p/t header on any line (a
middleware warning may precede it, MCP-GUARD-007), the marker words anywhere in the text, the ledger of the JSON receipt -- and nothing
here decides. A fact of the contract has a domain (counts up to a million, an enum), so a number beyond it is folded to a count with
the same answers to the questions the rules ask (is it zero, are the two equal), and a receipt that is no ledger is `unreadable`.
"""
import json
import re
from dataclasses import fields

from .generated import verdict_logic as logic

_HEADER = re.compile(r"PLAYTEST:\s*(\d+)\s*/\s*(\d+)\b")
_MARKERS = {name: re.compile(rf"\b{word}\b") for name, word in (("fail", "FAIL"), ("error", "ERROR"), ("console_err", "CONSOLE_ERR"),
                                                              ("blocked", "BLOCKED"), ("timeout", "TIMEOUT"), ("aborted", "ABORTED"))}
_MAX = 1_000_000  # the domain of a count in verdict.contract


def _count(number: int) -> int:
    return min(number, _MAX)


def _pair(passed: int, total: int) -> tuple[int, int]:
    """(passed, total) inside the domain of the contract; beyond it, 1/1 when they are equal and 0/1 when they are not."""
    return (passed, total) if max(passed, total) <= _MAX else ((1, 1) if passed == total else (0, 1))


def _text_facts(raw: str) -> dict:
    header, passed, total = "none", 0, 0
    for index, line in enumerate(raw.splitlines()):
        match = _HEADER.match(line)
        if match:
            header, (passed, total) = ("first" if index == 0 else "later"), _pair(int(match[1]), int(match[2]))
            break
    return {"text_header": header, "text_passed": passed, "text_total": total,
            **{f"text_has_{name}": marker.search(raw) is not None for name, marker in _MARKERS.items()}}


def _ledger_facts(raw: str) -> dict:
    try:
        receipt = json.loads(raw)
    except ValueError:
        receipt = None
    if not isinstance(receipt, dict):
        return {"ledger_readable": False, "ledger_teardown_ok": False, "ledger_has_failed": False, "ledger_failed": 0,
                "ledger_steps": 0, "ledger_steps_not_ok": 0, "ledger_failed_matches_steps": True}
    outer, steps = receipt.get("outer", {}), receipt.get("steps", [])
    steps = steps if isinstance(steps, list) else []
    # passes asks for `ok is True` of every step; outcome compares `failed` with the steps that are not truthy-ok (B16, F7)
    not_ok = sum(1 for step in steps if not (isinstance(step, dict) and step.get("ok") is True))
    falsy = sum(1 for step in steps if not (isinstance(step, dict) and step.get("ok")))
    return {"ledger_readable": True, "ledger_teardown_ok": isinstance(outer, dict) and outer.get("teardown_ok") is True,
            "ledger_has_failed": "failed" in receipt, "ledger_failed": 0 if receipt.get("failed", 1) == 0 else 1,
            "ledger_steps": _count(len(steps)), "ledger_steps_not_ok": _count(not_ok),
            "ledger_failed_matches_steps": receipt.get("failed", falsy) == falsy}


def _report_facts(raw: str | None, fmt: str | None) -> dict:
    """All the facts of a report. fmt is `json` or `text` as the caller says; any other value is sniffed (`{` opens a ledger)."""
    if fmt not in ("json", "text"):
        fmt = "json" if (raw or "").lstrip().startswith("{") else "text"
    facts = {"report_plane": "server", "report_format": fmt, "report_blank": not raw, "report_zero_of_zero": bool(raw) and "0/0" in raw}
    neutral_ledger = {"ledger_readable": False, "ledger_teardown_ok": False, "ledger_has_failed": False, "ledger_failed": 0, "ledger_steps": 0,
                      "ledger_steps_not_ok": 0, "ledger_failed_matches_steps": True}
    neutral_text = {"text_header": "none", "text_passed": 0, "text_total": 0, **{f"text_has_{name}": False for name in _MARKERS}}
    if not raw:
        return {**facts, **neutral_ledger, **neutral_text}
    return {**facts, **(_ledger_facts(raw) if fmt == "json" else neutral_ledger), **(_text_facts(raw) if fmt == "text" else neutral_text)}


def _input(input_type, facts: dict):
    """The input of one decision: the facts it declares, out of all of them."""
    return input_type(**{field.name: facts[field.name] for field in fields(input_type)})


def is_pass(raw: str | None, fmt: str | None = None) -> bool:
    """The DECISION passes: a non-empty, complete PLAYTEST ratio and no failure signal, or a ledger whose steps are all ok."""
    return logic.passes(_input(logic.PassesInput, _report_facts(raw, fmt)))


def classify(raw: str | None, fmt: str | None = None) -> str:
    """The DECISION outcome: pass, fail, or error (nothing to judge, or a receipt that is unreadable or disagrees with itself)."""
    return logic.outcome(_input(logic.OutcomeInput, _report_facts(raw, fmt)))


def file_state(passed: int, expected: int, raw: str) -> str:
    """The DECISION fileState of one file of a suite: pass, fail, error or no_verdict."""
    passed, expected = _pair(passed, expected)
    return logic.file_state(logic.FileStateInput(file_passed=passed, file_expected=expected, file_error_text="ERROR" in raw))


def suite_labels(states: list[str], outer_failed: bool) -> tuple[str, str]:
    """The DECISIONs suiteInner and suiteOuter: (INNER_PASS | INNER_FAIL | NO_VERDICT, OUTER_PASS | OUTER_FAIL)."""
    inner = logic.suite_inner(logic.SuiteInnerInput(suite_any_fail="fail" in states, suite_any_error="error" in states,
                                                    suite_all_no_verdict=set(states) <= {"no_verdict"}))
    return inner, logic.suite_outer(logic.SuiteOuterInput(suite_outer_failed=outer_failed))
