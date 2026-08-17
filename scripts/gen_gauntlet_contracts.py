"""Generate gauntlet routing + mutation contracts from _SPECS and ROUND_TRIP_CASES.

Usage:
    python scripts/gen_gauntlet_contracts.py               # write contracts.json
    python scripts/gen_gauntlet_contracts.py --dry-run     # print counts only
    python scripts/gen_gauntlet_contracts.py --diff        # exit 2 if output differs
    python scripts/gen_gauntlet_contracts.py --output PATH # write to custom path
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import sys
from typing import Any

_ROOT = pathlib.Path(__file__).parent.parent
_DEFAULT_OUT = _ROOT / "scripts" / "gauntlet" / "contracts.json"

# sys.path bootstrap: tool_specs needs server/src; tests.seams needs server/
_SERVER = _ROOT / "server"
# Insert in priority order: server/src > server > scripts.
# server must precede scripts because tests.seams.* lives in server/tests,
# and scripts/tests is a different package (test helpers, not seams).
sys.path.insert(0, str(_ROOT / "scripts")) # gauntlet.*
sys.path.insert(0, str(_SERVER))           # tests.seams.* wins over scripts/tests
sys.path.insert(0, str(_SERVER / "src"))   # unity_mcp.* (highest priority)


# ---------------------------------------------------------------------------
# Effect domain mapping
# ---------------------------------------------------------------------------

_WRITE_EFFECT: dict[str, str] = {
    "CORE":       "unity_persistent",
    "SCENE":      "unity_persistent",
    "UGUI":       "unity_persistent",
    "UITOOLKIT":  "unity_persistent",
    "COMPONENTS": "unity_persistent",
    "MEDIA":      "unity_persistent",
    "ASSETS":     "filesystem",
    "TESTS":      "observer_state",
    "VERIFY":     "observer_state",
    "RUNTIME":    "runtime_state",
    "SYSTEM":     "observer_state",
}

_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")


def _parse_args_str(s: str) -> dict[str, str]:
    """Parse 'key=val key2=val2' into dict. Empty string → {}."""
    s = s.strip()
    if not s:
        return {}
    result = {}
    for token in s.split():
        k, _, v = token.partition("=")
        if k:
            result[k] = v
    return result


def _effects_for(name: str, spec: Any) -> list[str]:
    """Map (category, mutability) to EffectDomain list."""
    if spec.mutability == "read":
        return ["pure_read"]
    return [_WRITE_EFFECT.get(spec.category, "unity_persistent")]


def _retry_for(effects: list[str]) -> str:
    """Derive retry policy from effects. blind_safe only for pure_read."""
    return "blind_safe" if effects == ["pure_read"] else "reconcile"


def _sanitize_id(raw_id: str) -> str:
    """'create_object→hierarchy' → 'rt.create-object.hierarchy'"""
    s = raw_id.replace("→", ".").replace("_", "-")
    # Ensure only valid chars
    s = re.sub(r"[^a-z0-9._-]", "-", s.lower())
    return f"rt.{s}"


def _generate_routing_contracts(
    specs: dict[str, Any],
    minimal_args: dict[str, str],
) -> list[dict]:
    """One contract per batch-callable _SPECS entry (prefix: route.)."""
    result = []
    for name, spec in sorted(specs.items()):
        if spec.direct_only or spec.runtime_only or spec.category == "_INTERNAL":
            continue
        effects = _effects_for(name, spec)
        contract = {
            "id": f"route.{name}",
            "action": name,
            "effects": effects,
            "retry": _retry_for(effects),
            "arguments": _parse_args_str(minimal_args.get(name, "")),
            "preconditions": {"connected": True},
            "expect_error": False,
            "forbidden_success_patterns": [],
        }
        result.append(contract)
    return result


def _generate_round_trip_contracts(cases: list[Any]) -> list[dict]:
    """One contract per ROUND_TRIP_CASES entry (prefix: rt.)."""
    result = []
    for param in cases:
        assert len(param.values) >= 5, f"ROUND_TRIP_CASES entry {param.id} has unexpected shape"
        mutate_cmd, mutate_args_t = param.values[0], param.values[1]
        raw_id = param.id
        # Substitute {ns} template with stable literal
        args = {k: v.replace("{ns}", "__seam-ns") for k, v in mutate_args_t.items()}
        contract = {
            "id": _sanitize_id(raw_id),
            "action": mutate_cmd,
            "effects": ["unity_persistent"],
            "retry": "reconcile",
            "arguments": args,
            "preconditions": {"connected": True},
            "expect_error": False,
            "forbidden_success_patterns": [],
        }
        result.append(contract)
    return result


def _load_base(path: pathlib.Path) -> tuple[dict, list[dict]]:
    """Load existing contracts.json; extract header + hand-written contracts."""
    data = json.loads(path.read_text(encoding="utf-8"))
    header = {k: v for k, v in data.items() if k != "contracts"}
    # Hand-written = not prefixed with route. or rt.
    hand_written = [
        c for c in data.get("contracts", [])
        if not c["id"].startswith("route.") and not c["id"].startswith("rt.")
    ]
    return header, hand_written


def _merge(hand_written: list[dict], generated: list[dict]) -> list[dict]:
    """Combine lists, raise on duplicate IDs."""
    all_contracts = hand_written + generated
    seen: set[str] = set()
    for c in all_contracts:
        if c["id"] in seen:
            raise ValueError(f"duplicate contract ID: {c['id']!r}")
        seen.add(c["id"])
    return all_contracts


def _bump_version(version: str) -> str:
    parts = version.split(".")
    if len(parts) == 3:
        parts[2] = str(int(parts[2]) + 1)
        return ".".join(parts)
    return version


def _sha256(text: str) -> str:
    return hashlib.sha256(text.encode()).hexdigest()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate gauntlet contracts from _SPECS")
    parser.add_argument("--dry-run", action="store_true", help="Print counts only, no writes")
    parser.add_argument("--diff", action="store_true", help="Exit 2 if output differs from committed")
    parser.add_argument("--output", default=str(_DEFAULT_OUT), help="Output path")
    args = parser.parse_args(argv)

    output_path = pathlib.Path(args.output)

    # Load source data
    from tests.seams.test_batch_surface import _MINIMAL_ARGS  # noqa: PLC0415
    from tests.seams.test_round_trips import ROUND_TRIP_CASES  # noqa: PLC0415

    from unity_mcp.tools.tool_specs import _SPECS  # noqa: PLC0415

    routing_contracts  = _generate_routing_contracts(_SPECS, _MINIMAL_ARGS)
    rt_contracts       = _generate_round_trip_contracts(ROUND_TRIP_CASES)
    header, hand_written = _load_base(output_path)

    all_contracts = _merge(hand_written, routing_contracts + rt_contracts)
    # Sort by id
    all_contracts.sort(key=lambda c: c["id"])

    new_version = _bump_version(header.get("catalog_version", "1.0.0"))

    output_data = {
        "schema_version": header.get("schema_version", 2),
        "catalog_version": new_version,
        "scope": header.get("scope", "builtin"),
        "owner": header.get("owner"),
        "contracts": all_contracts,
    }
    new_text = json.dumps(output_data, indent=2)

    if args.dry_run:
        print(f"Routing contracts: {len(routing_contracts)}")
        print(f"Round-trip contracts: {len(rt_contracts)}")
        print(f"Hand-written contracts: {len(hand_written)}")
        print(f"Total: {len(all_contracts)}")
        return 0

    if args.diff:
        if output_path.exists():
            existing = output_path.read_text(encoding="utf-8")
            if _sha256(existing.strip()) != _sha256(new_text.strip()):
                print(f"DRIFT: {output_path} differs from expected output")
                return 2
        else:
            print(f"MISSING: {output_path} not found")
            return 2
        return 0

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(new_text + "\n", encoding="utf-8")
    print(f"Wrote {output_path} ({len(all_contracts)} contracts: "
          f"{len(routing_contracts)} route + {len(rt_contracts)} rt + {len(hand_written)} hand-written)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
