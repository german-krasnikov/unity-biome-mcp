"""CI gate: verify agent-event.schema.json has not been modified after T14 freeze.

Usage: python scripts/check_relay_schema.py
Exit nonzero on hash mismatch. First run: writes the .schema.freeze file.
"""
import hashlib
import sys
from pathlib import Path

SCHEMA = Path(__file__).parent.parent / "protocol" / "chat-relay" / "v2" / "agent-event.schema.json"
FREEZE = Path(__file__).parent.parent / "protocol" / "chat-relay" / "v2" / ".schema.freeze"


def check() -> None:
    if not SCHEMA.exists():
        print(f"ERROR: schema file not found: {SCHEMA}")
        sys.exit(1)

    schema_hash = hashlib.sha256(SCHEMA.read_bytes()).hexdigest()

    if FREEZE.exists():
        pinned = FREEZE.read_text(encoding="utf-8").strip()
        if schema_hash != pinned:
            print(
                f"FREEZE VIOLATION: schema changed!\n"
                f"  current : {schema_hash[:16]}\n"
                f"  pinned  : {pinned[:16]}\n"
                f"Update {FREEZE.name} intentionally after reviewing the schema diff."
            )
            sys.exit(1)
        print(f"schema freeze OK  ({schema_hash[:16]})")
    else:
        FREEZE.write_text(schema_hash + "\n", encoding="utf-8")
        print(f"freeze pinned: {schema_hash[:16]}")


if __name__ == "__main__":
    check()
