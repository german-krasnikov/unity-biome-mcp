"""Strip default values from component read responses to save tokens."""

_FIELD_ALIASES = {
    "position": "m_localposition", "localposition": "m_localposition",
    "rotation": "m_localrotation", "localrotation": "m_localrotation",
    "scale": "m_localscale", "localscale": "m_localscale",
    "mass": "m_mass", "enabled": "m_enabled", "active": "m_isactive",
    "name": "m_name", "tag": "m_tagstring", "layer": "m_layer",
}

_DEFAULTS = frozenset({
    "0", "0.0", "false", "null", "None", '""',
    "(0, 0, 0)", "(0.0, 0.0, 0.0)",
    "(0, 0, 0, 1)", "(0.0, 0.0, 0.0, 1.0)",
    "(1, 1, 1)", "(1.0, 1.0, 1.0)",
    "[]", "#00000000",
    # F08: additional common Unity defaults. "Default"/"Untagged" are context-dependent
    # (layer vs tag field); use _no_strip=True escape hatch if a real value collides.
    "Untagged", "Default",
    "(0, 0)", "(0, 0, 0, 0)", "#FFFFFFFF",
})

# Item 22: "1"/"1.0" only stripped for known Unity internal fields that default to 1.
# Prevents false-positive stripping of user fields like "mass: 1" on Rigidbody.
_FIELD_DEFAULTS: dict[str, frozenset[str]] = {
    "m_mass": frozenset({"1", "1.0"}),
    "m_layer": frozenset({"0"}),
    "m_isstatic": frozenset({"false", "False"}),
}


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


def collapse_empty_sections(text: str) -> str:
    """Remove [Header] section lines whose body is empty (next line is another header or EOF)."""
    lines = text.splitlines()
    result = []
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]") and stripped != "---":
            # Look ahead: skip blank lines, check if next non-blank is another header or EOF
            j = i + 1
            while j < len(lines) and not lines[j].strip():
                j += 1
            next_stripped = lines[j].strip() if j < len(lines) else ""
            is_empty = (j >= len(lines) or
                        (next_stripped.startswith("[") and next_stripped.endswith("]")))
            if is_empty:
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
