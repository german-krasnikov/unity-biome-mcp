"""Generate AgentEvent JSON Schema and validate JSONL fixtures against it.

Usage:
    python scripts/export_agent_schema.py --write   # generate + commit schema
    python scripts/export_agent_schema.py --check   # CI drift detection (read-only)
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).parent.parent
_SCHEMA_PATH = _REPO_ROOT / "protocol" / "chat-relay" / "v2" / "agent-event.schema.json"
_FIXTURES_DIR = _REPO_ROOT / "scripts" / "fixtures" / "agent-events"

sys.path.insert(0, str(_REPO_ROOT / "server" / "src"))


def _generate_schema() -> dict:
    from unity_mcp.agent_event import AgentEvent
    return AgentEvent.model_json_schema()


def _validate_fixtures(schema: dict) -> int:
    """Validate every line of every JSONL fixture. Returns number of errors."""
    import jsonschema
    errors = 0
    for jsonl_path in sorted(_FIXTURES_DIR.glob("*.jsonl")):
        for i, line in enumerate(jsonl_path.read_text(encoding="utf-8").splitlines(), 1):
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            try:
                jsonschema.validate(instance=obj, schema=schema)
            except jsonschema.ValidationError as exc:
                print(f"ERROR {jsonl_path.name}:{i}: {exc.message}", file=sys.stderr)
                errors += 1
    return errors


def _write(schema: dict) -> None:
    _SCHEMA_PATH.parent.mkdir(parents=True, exist_ok=True)
    _SCHEMA_PATH.write_text(json.dumps(schema, indent=2) + "\n", encoding="utf-8")
    print(f"Written: {_SCHEMA_PATH}")
    errors = _validate_fixtures(schema)
    if errors:
        print(f"{errors} fixture validation error(s).", file=sys.stderr)
        sys.exit(1)
    print("Fixtures OK (validated against generated schema).")


def _check(schema: dict) -> None:
    if not _SCHEMA_PATH.exists():
        print(f"ERROR: committed schema not found: {_SCHEMA_PATH}", file=sys.stderr)
        sys.exit(1)
    committed = json.loads(_SCHEMA_PATH.read_text(encoding="utf-8"))
    if schema != committed:
        import difflib
        a = json.dumps(committed, indent=2).splitlines(keepends=True)
        b = json.dumps(schema, indent=2).splitlines(keepends=True)
        diff = "".join(difflib.unified_diff(a, b, fromfile="committed", tofile="generated"))
        print(f"Schema drift detected:\n{diff}", file=sys.stderr)
        sys.exit(1)
    errors = _validate_fixtures(schema)
    if errors:
        sys.exit(1)
    print("Schema OK (no drift, fixtures valid).")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--write", action="store_true", help="Generate and write schema")
    group.add_argument("--check", action="store_true", help="Drift check (CI)")
    args = parser.parse_args()

    schema = _generate_schema()
    if args.write:
        _write(schema)
    else:
        _check(schema)


if __name__ == "__main__":
    main()
