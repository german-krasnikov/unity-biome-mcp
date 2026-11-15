"""Strip default values from component read responses to save tokens."""

from .generated.field_aliases import FIELD_ALIAS_ROWS
from .generated.strip_defaults import FIELD_SPECIFIC_DEFAULT_ROWS, STRIP_DEFAULT_ROWS

# Built from dsl/data/fieldaliases.core-data via FIELD_ALIAS_ROWS.
_FIELD_ALIASES: dict[str, str] = {row.alias: row.canonical for row in FIELD_ALIAS_ROWS}

# Built from dsl/data/stripdefaults-global.core-data via STRIP_DEFAULT_ROWS.
_DEFAULTS: frozenset[str] = frozenset(row.value for row in STRIP_DEFAULT_ROWS)

# Item 22: "1"/"1.0" only stripped for known Unity internal fields that default to 1.
# Prevents false-positive stripping of user fields like "mass: 1" on Rigidbody.
# Built from dsl/data/stripdefaults-field.core-data via FIELD_SPECIFIC_DEFAULT_ROWS.
_field_defaults_tmp: dict[str, set[str]] = {}
for _row in FIELD_SPECIFIC_DEFAULT_ROWS:
    _field_defaults_tmp.setdefault(_row.fieldKey, set()).add(_row.value)
_FIELD_DEFAULTS: dict[str, frozenset[str]] = {k: frozenset(v) for k, v in _field_defaults_tmp.items()}
del _field_defaults_tmp, _row


def project_fields(text: str, fields: str) -> str:
    """F07: keep only lines whose key matches a requested field (exact or dotted-prefix,
    case-insensitive). Always keep headers ([...]), separators (---), error/blank lines.
    Requesting 'm_LocalPosition' keeps 'm_LocalPosition.x/y/z'; 'pos' does NOT match 'position'."""
    wanted = []
    for f in (fields or "").split(","):
        raw = f.strip().lower()
        if not raw:
            continue
        wanted.append(raw)
        alias = _FIELD_ALIASES.get(raw)
        if alias and alias != raw:
            wanted.append(alias)
    if not wanted:
        return text
    out = []
    available: list[str] = []
    matched = False
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("[") or stripped == "---" or stripped.startswith("err:"):
            out.append(line)
            continue
        key = (stripped.split(": ", 1)[0] if ": " in stripped else stripped).strip().lower()
        available.append(key)
        if any(key == w or key.startswith(w + ".") for w in wanted):
            out.append(line)
            matched = True
    result = "\n".join(out)
    if not matched and available:
        note = f"0 fields matched. Available: {', '.join(dict.fromkeys(available))}"
        return (result + "\n" + note).strip()
    return result


def _next_content_is_header_or_eof(lines: list[str], j: int) -> bool:
    """Skip blank lines from j; return True if next non-blank is a header or EOF."""
    while j < len(lines) and not lines[j].strip():
        j += 1
    if j >= len(lines):
        return True
    s = lines[j].strip()
    return s.startswith("[") and s.endswith("]")


def collapse_empty_sections(text: str) -> str:
    """Remove [Header] section lines whose body is empty (next line is another header or EOF)."""
    lines = text.splitlines()
    result = []
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]") and stripped != "---" and _next_content_is_header_or_eof(lines, i + 1):
            i += 1
            continue
        result.append(line)
        i += 1
    return "\n".join(result)


def strip_defaults(text: str) -> str:
    """Remove lines whose value is a known default. Keep headers, separators, errors."""
    if not text:
        return text
    out = []
    for line in text.splitlines():
        stripped = line.strip()
        # Always keep: section headers, separators, error lines, blank lines
        if not stripped or stripped.startswith("[") or stripped == "---" or stripped.startswith("err:"):
            out.append(line)
            continue
        # Check value after colon
        if ": " in stripped:
            key, value = stripped.split(": ", 1)
            value = value.strip()
            if value in _DEFAULTS:
                continue
            field_defaults = _FIELD_DEFAULTS.get(key.strip().lower())
            if field_defaults and value in field_defaults:
                continue
        out.append(line)
    return "\n".join(out)
