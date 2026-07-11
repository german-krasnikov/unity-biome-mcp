"""Pure functions for alias resolution in middleware pipeline.

All functions are stateless and have zero external dependencies (stdlib only).
Alias cache format: {name: 'path|comp|field'} — keys WITHOUT '$' prefix.
"""
import re

_ALIAS_HEADER = "--- ALIASES ---"
_ALIAS_FOOTER = "---"
# Matches whole-value sigil: "$name" where name is ASCII identifier
_SIGIL_RE = re.compile(r"^\$([A-Za-z_][A-Za-z0-9_]*)$")
# Keys with comma-separated values that are each resolved independently
_COMMA_KEYS = frozenset({"paths", "queries", "checks_before", "checks_after"})


def parse_aliases_from_hierarchy(text: str) -> "dict[str, str] | None":
    """Parse '--- ALIASES ---' block from get_hierarchy response.

    Returns dict of {name: pipe_path} or None if no block found.
    Partitions on first '=' so values may contain '='.
    Returns None (not empty dict) when no block → caller preserves cache.
    Returns {} when block found but empty.
    """
    if not text or _ALIAS_HEADER not in text:
        return None
    start = text.index(_ALIAS_HEADER) + len(_ALIAS_HEADER)
    result: dict[str, str] = {}
    for line in text[start:].split("\n"):
        stripped = line.strip()
        if stripped == _ALIAS_FOOTER:
            break
        if not stripped or "=" not in stripped:
            continue
        name, _, value = stripped.partition("=")
        name = name.strip().lstrip("$")
        if not name:
            continue
        result[name] = value.strip()
    return result


def resolve_aliases_in_args(args: dict, cache: "dict[str, str]") -> dict:
    """Replace $name in arg values with cached aliases.

    Whole-value match only: "$hp" resolves; "/prefix/$hp" does NOT (use a full-path VAL).
    For compound paths like '/Player/Child', define: VAL $playerChild /Player/Child
    Per-key extraction from pipe-format values:
      path / paths → segment[0]
      component    → segment[1]
      field / prop → segment[2]
      all others   → full pipe value (e.g. query, queries)
    Comma-separated: 'paths' and 'queries' split on comma, each resolved, rejoined.
    Returns new dict (never mutates input).
    """
    if not cache or not args:
        return args
    result: dict = {}
    for key, val in args.items():
        if not isinstance(val, str):
            result[key] = val
            continue
        if key in _COMMA_KEYS:
            tokens = [_resolve_one(t.strip(), key, cache) for t in val.split(",")]
            result[key] = ",".join(tokens)
        else:
            result[key] = _resolve_one(val, key, cache)
    return result


def strip_alias_block(text: str) -> str:
    """Remove '--- ALIASES ---...---' block from hierarchy text.

    Safety net: if C# still prepends the block, strip it here so the LLM
    never sees it (aliases are already in cache after parse_aliases_from_hierarchy).
    Returns text unchanged when no block found or footer is missing.
    """
    if not text or _ALIAS_HEADER not in text:
        return text
    start = text.index(_ALIAS_HEADER)
    end = text.find("\n" + _ALIAS_FOOTER, start)
    if end == -1:
        return text
    result = text[:start].rstrip("\n") + text[end + len(_ALIAS_FOOTER) + 1:]
    return result.strip("\n") if result else result


def parse_aliases_from_get_aliases(text: str) -> "dict[str, str]":
    """Parse bare name=value lines from get_aliases response.

    get_aliases returns lines without the --- ALIASES --- header/footer.
    Returns empty dict on 'no aliases' or empty text.
    """
    result: dict[str, str] = {}
    if not text or text.strip() == "no aliases":
        return result
    for line in text.split("\n"):
        stripped = line.strip()
        if not stripped or "=" not in stripped:
            continue
        name, _, value = stripped.partition("=")
        name = name.strip().lstrip("$")
        if not name:
            continue
        result[name] = value.strip()
    return result


def _resolve_one(val: str, key: str, cache: "dict[str, str]") -> str:
    """Resolve a single potential $name token against the alias cache."""
    m = _SIGIL_RE.match(val)
    if not m:
        return val
    name = m.group(1)
    if name not in cache:
        return val  # unknown — pass through unchanged
    full = cache[name]
    parts = full.split("|")
    if key in ("path", "paths"):
        return parts[0]
    if key == "component":
        return parts[1] if len(parts) > 1 else full
    if key in ("field", "prop"):
        return parts[2] if len(parts) > 2 else full
    return full  # query, queries, or any other key → full pipe value


# --- Post-call hooks (registered at import time) ---

from .middleware_hooks import register_post  # noqa: E402


@register_post("get_hierarchy")
def _hook_alias_from_hierarchy(cmd: str, args: dict, result: str, mw) -> str:
    parsed = parse_aliases_from_hierarchy(result)
    if parsed is not None:
        mw._alias_cache = parsed
        result = strip_alias_block(result)
    return result


@register_post("get_aliases")
def _hook_alias_from_get_aliases(cmd: str, args: dict, result: str, mw) -> str:
    mw._alias_cache = parse_aliases_from_get_aliases(result)
    return result
