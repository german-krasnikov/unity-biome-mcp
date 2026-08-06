"""Read/cache methods for Middleware (mixin)."""

from .middleware_types import is_write
from .tools.editor_state import (
    parse_editor_field as _parse_editor_field,
    is_play_mode as _is_play_mode,
    is_paused as _is_paused,
)


class MiddlewareReadsMixin:
    """Read tracking, component cache, console hints, editor state. Attrs in Middleware.__init__."""

    # ── Feature 3: Taint Tracking ─────────────────────────────────────────

    def record_read(self, cmd: str, args: dict, result: str) -> None:
        if cmd in ("get_component", "inspect", "get_object_detail"):
            path = args.get("path", "")
            if path:
                if path in self._clean_paths:
                    self._clean_paths.move_to_end(path)
                else:
                    self._clean_paths[path] = None
                    if len(self._clean_paths) > self._MAX_PATHS:
                        self._clean_paths.popitem(last=False)

    # ── Feature 2: Confidence Decay ───────────────────────────────────────

    def update_confidence(self, cmd: str, args: dict, result: str) -> str:
        if is_write(cmd, args):
            self.confidence = max(0.0, self.confidence - 0.08)
        else:
            self.confidence = min(1.0, self.confidence + 0.15)
        if self.confidence >= 0.5:
            return result
        suffix = f"\n[confidence: {self.confidence:.2f}]"
        if self.confidence < 0.3:
            suffix += " [LOW CONFIDENCE: re-read state before writing]"
        return result + suffix

    # ── Item 3: Component Cache ───────────────────────────────────────────────

    def _lru_add_component(self, path: str, component: str) -> None:
        if path in self._component_cache:
            self._component_cache.move_to_end(path)
        else:
            self._component_cache[path] = set()
            if len(self._component_cache) > self._MAX_COMPONENTS:
                self._component_cache.popitem(last=False)
        self._component_cache[path].add(component)

    def cache_components(self, cmd: str, args: dict, result: str) -> None:
        """Update component cache from read results and mutations."""
        if cmd == "manage_component" and "path" in args and "type" in args:
            path = args["path"]
            comp = args["type"]
            action = args.get("action", "")
            if action == "add" and not result.startswith("err"):
                self._lru_add_component(path, comp)
            elif action in ("remove", "delete") and path in self._component_cache:
                self._component_cache[path].discard(comp)
            return
        if cmd == "delete_object" and "path" in args:
            self._component_cache.pop(args["path"], None)
            return
        if cmd == "get_component" and "path" in args and "type" in args:
            self._lru_add_component(args["path"], args["type"])
        elif cmd == "inspect" and "[" in result:
            current_path: str | None = None
            for line in result.split("\n"):
                stripped = line.strip()
                if stripped.startswith("---") and stripped.endswith("---"):
                    inner = stripped.strip("-").strip()
                    current_path = inner.split()[0] if inner else None
                    if current_path and current_path not in self._component_cache:
                        self._component_cache[current_path] = set()
                        if len(self._component_cache) > self._MAX_COMPONENTS:
                            self._component_cache.popitem(last=False)
                elif stripped.startswith("[") and stripped.endswith("]") and current_path:
                    if current_path in self._component_cache:
                        self._component_cache[current_path].add(stripped[1:-1])

    def check_component_exists(self, path: str, component: str) -> str | None:
        """Return warning if component definitely not on object (from cache). None = ok/unknown."""
        if path not in self._component_cache:
            return None
        known = self._component_cache[path]
        if not known:
            return None
        if component in known:
            return None
        lower_known = {c.lower(): c for c in known}
        if component.lower() in lower_known:
            return None  # case diff — InputNormalizer will fix
        return f"Component '{component}' not found on '{path}'. Known: {', '.join(sorted(known))}"

    # ── Item 5: Console Error Categorization ─────────────────────────────────

    def categorize_console_errors(self, result: str) -> str:
        """Parse console errors in result, append categorized hints."""
        if "NullReferenceException" in result:
            return result + "\n[HINT: NullRef — likely broken reference. Use validate_references to check.]"
        if "MissingComponentException" in result:
            return result + "\n[HINT: Missing component — was it removed? Use get_components_list to check.]"
        if "FormatException" in result or "Input string was not in a correct format" in result:
            return result + "\n[HINT: Format error — check value format. Use get_schema for field types.]"
        return result

    # ── Feature: Play Mode Auto-Routing ──────────────────────────────────────

    def track_editor_state(self, cmd: str, result: str) -> None:
        """Update is_playing from editor(action=state) response."""
        if cmd in ("recompile", "scene") and self.schema_cache is not None:
            self.schema_cache.invalidate_all()
        if cmd != "editor":
            return
        if _parse_editor_field(result, "playing") is not None:
            self._play_state_known = True
            self.is_playing = _is_play_mode(result) or _is_paused(result)

    def reroute_cmd(self, cmd: str, args: dict) -> tuple[str, dict]:
        """Rewrite set_property↔set_runtime_property based on play mode."""
        if cmd == "set_property" and self.is_playing and "prop" in args:
            new_args = {**args, "field": args["prop"]}
            del new_args["prop"]
            return "set_runtime_property", new_args
        return cmd, args

    # ── Item 1: Focus tracking + PrefetchCache seeding ────────────────────────

    def _track_focus(self, cmd: str, args: dict, result: str) -> None:
        """Update _recent_focus deque from args paths."""
        if cmd in {"find_objects", "get_component", "inspect", "get_object_detail",
                   "set_property", "set_active"}:
            path = args.get("path") or args.get("target")
            if path:
                if path in self._recent_focus:
                    self._recent_focus.remove(path)
                self._recent_focus.append(path)

    def _seed_preimage(self, cmd: str, args: dict, result: str) -> None:
        """After WRITE returns with reflect snapshot, seed PrefetchCache."""
        if self._prefetch_cache is None or cmd != "set_property":
            return
        if not args.get("path") or not args.get("component"):
            return
        try:
            from .reflect import _parse_snapshot
        except ImportError:
            return
        snap = _parse_snapshot(result)
        if not snap:
            return
        body = f"[{args['component']}]\n" + "\n".join(f"{k}: {v}" for k, v in snap.items())
        self._prefetch_cache.put_synthetic(
            "get_component",
            {"path": args["path"], "type": args["component"]},
            body,
            source="reflect-snapshot",
        )

    # ── Item 2: HierarchyDiff ──────────────────────────────────────────────────

    def _maybe_diff_hierarchy(self, full: str) -> str:
        """Return unified diff if economical (<50% of full), else full text."""
        if self._last_hierarchy_full is None:
            self._last_hierarchy_full = full
            self._hierarchy_call_id = 1
            return full

        import difflib
        prev_lines = self._last_hierarchy_full.splitlines()
        full_lines = full.splitlines()
        diff_lines = list(difflib.unified_diff(prev_lines, full_lines, n=0))

        # Skip header lines ('---' and '+++')
        body_lines = diff_lines[2:] if len(diff_lines) > 2 else []
        diff_body = "".join(body_lines)

        if not diff_body:
            return f"[DIFF since #{self._hierarchy_call_id}: NO_CHANGE]"

        # Use diff only if changed lines (+ and -) < 50% of full line count
        changed_lines = [l for l in body_lines if l.startswith(("+", "-"))]
        if len(changed_lines) > len(full_lines) * 0.5:
            self._last_hierarchy_full = full
            self._hierarchy_call_id += 1
            return full

        # Diff economical — update state and return diff
        prev_id = self._hierarchy_call_id
        self._last_hierarchy_full = full
        self._hierarchy_call_id += 1
        return f"[DIFF since #{prev_id}]\n{diff_body}"
