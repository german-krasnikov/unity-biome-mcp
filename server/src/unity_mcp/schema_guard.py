"""SchemaGuard: pre-flight validator that blocks known typos before TCP send."""

from .schema_cache import SchemaCache
from .utils import _levenshtein


class SchemaGuard:
    """Validate set_property / manage_component add / wire_event before sending."""

    LEV_BLOCK = 2       # lev ≤ 2 → block
    LEV_WARN_MAX = 5    # lev ∈ [3,5] → pass silently (v1)
    SKIP_PROP_PREFIXES = ("m_",)

    def __init__(self, mw, cache: SchemaCache) -> None:
        self._mw = mw
        self._cache = cache

    async def validate(self, cmd: str, args: dict, send_fn) -> str | None:
        """Returns 4-line block envelope on bad input, None on pass.

        Catches all exceptions internally (fail-open).
        """
        from .metrics import METRICS
        METRICS.inc("validate.checked")
        try:
            if cmd == "set_property":
                return await self._validate_set_property(args, send_fn)
            if cmd == "manage_component" and args.get("action") == "add":
                return await self._validate_manage_add(args, send_fn)
            if cmd == "wire_event":
                return await self._validate_wire_event(args, send_fn)
            return None
        except Exception:
            METRICS.inc("validate.error")
            return None

    # ── set_property ─────────────────────────────────────────────────────────

    async def _validate_set_property(self, args: dict, send_fn) -> str | None:
        path = args.get("path", "")
        component = args.get("component", "")
        prop = args.get("prop", "")

        if not component or not prop:
            return None
        if path.startswith("$"):
            return None
        if any(prop.startswith(pfx) for pfx in self.SKIP_PROP_PREFIXES):
            return None
        if "/" in str(args.get("value", "")):
            return None  # ObjectReference — skip

        # Component check
        comps = self._mw.get_components_for_path(path)
        if comps is not None and component not in comps:
            best, lev = self._best_match(component, comps)
            if lev <= self.LEV_BLOCK:
                known = ", ".join(sorted(comps)[:5])
                return self._block_envelope("component", component, best, lev, path, known)
            return None  # lev > 2: unknown type, pass through

        # Prop check
        props = await self._fetch_props(component, send_fn)
        if not props:
            return None  # type not in schema DB — don't block
        if prop not in props:
            best, lev = self._best_match(prop, props)
            if lev <= self.LEV_BLOCK:
                known = ", ".join(sorted(props)[:5])
                return self._block_envelope("prop", prop, best, lev, f"{path}.{component}", known)
        return None

    # ── manage_component add ─────────────────────────────────────────────────

    async def _validate_manage_add(self, args: dict, send_fn) -> str | None:
        type_name = args.get("type", "")
        if not type_name:
            return None

        props = await self._fetch_props(type_name, send_fn)
        if props is not None and len(props) == 0:
            # Type not found — suggest from known types
            known_types = self._known_types()
            best, lev = self._best_match(type_name, known_types) if known_types else ("", 999)
            fix = f"'{best}' (lev={lev})" if best else "execute_code to AddComponent dynamically"
            known = ", ".join(sorted(known_types)[:5]) if known_types else "none cached yet"
            return (
                f"[INVALID: type '{type_name}' not found]\n"
                f"[FIX: try {fix} or 'execute_code' to AddComponent dynamically]\n"
                f"[KNOWN: {known}]\n"
                f"[BYPASS: pass _no_validate=true to skip]"
            )
        return None

    # ── wire_event ───────────────────────────────────────────────────────────

    async def _validate_wire_event(self, args: dict, send_fn) -> str | None:
        target_path = args.get("target_path", "")
        target_comp = args.get("target_component", "")
        if not target_path or not target_comp:
            return None

        comps = self._mw.get_components_for_path(target_path)
        if comps is not None and target_comp not in comps:
            best, lev = self._best_match(target_comp, comps)
            if lev <= self.LEV_BLOCK:
                known = ", ".join(sorted(comps)[:5])
                return self._block_envelope("component", target_comp, best, lev, target_path, known)
        return None

    # ── helpers ──────────────────────────────────────────────────────────────

    async def _fetch_props(self, component: str, send_fn) -> frozenset | None:
        """Return props frozenset from cache or via get_schema. None on unknown state."""
        from .metrics import METRICS
        cached = self._cache.get(component)
        if cached is not None:
            METRICS.inc("validate.cache_hit")
            return cached
        schema_text = await send_fn("get_schema", {"type": component})
        schema_str = schema_text.get("data", "") if isinstance(schema_text, dict) else str(schema_text)
        props = SchemaCache.parse(schema_str)
        self._cache.put(component, props)
        METRICS.inc("validate.cache_miss")
        return props

    def _best_match(self, name: str, candidates) -> tuple[str, int]:
        """Return (best_candidate, lev_distance) from iterable."""
        if not candidates:
            return "", 999
        best, lev = min(((c, _levenshtein(name, c)) for c in candidates), key=lambda x: x[1])
        return best, lev

    def _known_types(self) -> set:
        """Flatten all component names from the component cache."""
        return self._mw.get_known_component_types()

    @staticmethod
    def _block_envelope(kind: str, bad: str, best: str, lev: int, where: str, known: str) -> str:
        return (
            f"[INVALID: {kind} '{bad}' on {where}]\n"
            f"[FIX: '{best}' (lev={lev}) | manage_component add type={bad}]\n"
            f"[KNOWN: {known}]\n"
            f"[BYPASS: pass _no_validate=true to skip]"
        )
