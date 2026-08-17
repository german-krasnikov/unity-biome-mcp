"""Path resolution + fuzzy matching for Middleware (extracted: F14).

PathResolverMixin is mixed into Middleware; methods bind to the same `self`
(state lives in Middleware.__init__) — behavior identical to the inline version.
"""
import re
import time

from .utils import _levenshtein

_SCENE_HEADER_RE = re.compile(r"^\[(.+)\]$")


def _last_segment(path: str) -> str:
    """Bracket-aware last segment: '/a/[b/c]/d' → 'd', '/Root/[Zone A/B]' → '[Zone A/B]'."""
    depth, last = 0, -1
    for i, ch in enumerate(path):
        if ch == '[':
            depth += 1
        elif ch == ']':
            depth -= 1
        elif ch == '/' and depth == 0:
            last = i
    return path[last + 1:]


def _split_scene_qualified(path: str) -> tuple:
    """'Scene:/foo' -> ('Scene', '/foo'). '/foo' -> ('', '/foo')."""
    if ":" in path and path[0] not in ("$", "&"):
        idx = path.index(":")
        rest = path[idx + 1:]
        if rest.startswith("/"):
            return path[:idx], rest
    return "", path


class PathResolverMixin:
    """Path cache, fuzzy/suffix matching, live search + disambiguation."""

    def update_path_cache(self, cmd: str, result: str) -> None:
        if cmd != "get_hierarchy":
            return
        self.known_paths.clear()
        self.path_to_scene.clear()
        # Parse indented hierarchy into full paths.
        # Each line: optional tree chars + name + " $ref" [+ extras]
        # Indent depth: count leading groups of 3 chars (│  , ├─ , └─ )
        stack: list[str] = []  # names per depth level
        current_scene = ""
        base_depth = -1  # normalized base: first object's depth becomes 0
        for line in result.split("\n"):
            stripped = line.strip()
            if not stripped:
                continue
            # Scene header detection: [SceneName] with no & ref
            m = _SCENE_HEADER_RE.match(stripped)
            if m and "&" not in stripped:
                current_scene = m.group(1)
                stack = []
                base_depth = -1  # reset per scene
                continue
            if "&" not in line:
                continue
            # Count indent depth: each level uses exactly 3 chars (│  / ├─ / └─ / spaces)
            i = 0
            for ch in line:
                if ch in "│├└─ ":
                    i += 1
                else:
                    break
            depth = i // 3
            if base_depth < 0:
                base_depth = depth  # first object sets the baseline
            rel_depth = depth - base_depth  # 0 = root, 1 = child, etc.
            raw = line[i:].strip()
            if not raw:
                continue
            name = raw.split("&")[0].strip()
            if not name:
                continue
            # Trim stack to relative depth, then append current name
            stack = stack[:rel_depth]
            stack.append(name)
            full_path = "/" + "/".join(stack)
            self.known_paths.add(full_path)
            if current_scene:
                self.known_paths.add(f"{current_scene}:{full_path}")
                self.path_to_scene[full_path] = current_scene

    def validate_path(self, path: str) -> str | None:
        if not self.known_paths:
            return None
        if path.startswith("&") or path.startswith("$") or path.startswith("#"):
            return None
        scene, bare = _split_scene_qualified(path)
        check = bare if scene else path
        if check not in self.known_paths:
            sample = ", ".join(sorted(self.known_paths)[:10])
            return f"⚠ PATH WARNING: '{path}' not in last hierarchy. Known: {sample}"
        if scene and self.path_to_scene.get(bare) not in (scene, None, ""):
            actual = self.path_to_scene.get(bare, "?")
            return f"⚠ PATH WARNING: '{bare}' belongs to scene '{actual}', not '{scene}'"
        return None

    def resolve_path(self, path: str) -> str:
        """Fuzzy-match path against known_paths. Returns best match or original."""
        if not self.known_paths:
            return path
        if path in self.known_paths:
            return path
        # Try suffix match: find paths ending with /path
        suffix = "/" + path.lstrip("/")
        matches = [p for p in self.known_paths if p.endswith(suffix) or p == path]
        if len(matches) == 1:
            return matches[0]
        if len(matches) > 1:
            # Pick closest by Levenshtein distance
            return min(matches, key=lambda p: _levenshtein(p, path))
        return path

    def _get_disambig(self):
        """Lazy-init Disambiguator with refreshed snapshots."""
        if not self._disambig_enabled:
            return None

        recent = list(self._recent_focus) if hasattr(self, "_recent_focus") else []
        clean = set(self._clean_paths.keys()) if hasattr(self, "_clean_paths") else set()
        mutations_paths = []
        if hasattr(self, "_last_writes"):
            mutations_paths = [k[0] for k in self._last_writes if k and k[0]]

        if self._disambig is None:
            from collections import deque

            from .clarifier import Disambiguator
            self._disambig = Disambiguator(
                recent_paths=recent,
                clean_paths=clean,
                mutation_log=deque(mutations_paths),
            )
        else:
            # Refresh snapshots cheaply
            self._disambig._recent = recent
            self._disambig._clean = clean
            self._disambig._mutations = mutations_paths

        return self._disambig

    async def resolve_path_live(self, path: str, send_fn) -> tuple[str, str]:
        """Resolve path via Unity search when cache misses.

        Returns (resolved_path, marker) tuple.
        marker is non-empty string when auto-resolved with context clue.
        resolved_path starts with '__DISAMBIG_BLOCK__\\n...' when block needed.
        """
        if not path or path.startswith("&") or path.startswith("$") or path.startswith("#"):
            return path, ""
        if not self.known_paths:
            return path, ""
        cached = self.resolve_path(path)
        if cached != path:
            return cached, ""
        if path in self.known_paths:
            return path, ""  # exact match in cache — no need to search
        # F17: negative path cache — skip TCP if recently confirmed unknown
        now = time.monotonic()
        if path in self._negative_path_cache and now < self._negative_path_cache[path]:
            return path, ""
        leaf = _last_segment(path)
        search_ok = False
        try:
            result = await send_fn("search_scene", {"query": f"name {leaf}"})
            search_ok = True
            candidates = [
                re.sub(r"\s+[#$&].*", "", line).strip()
                for line in result.strip().split("\n")
                if line.strip() and leaf.lower() in line.lower()
            ]
            if len(candidates) == 1:
                return candidates[0], ""
            if len(candidates) > 1:
                disambig = self._get_disambig()
                if disambig is not None:
                    decision = disambig.decide(leaf, candidates)
                    if decision is not None:
                        chosen, marker = decision
                        return chosen, marker
                    # Block — return special marker for wrap_send to detect
                    return f"__DISAMBIG_BLOCK__\n{disambig.format_block(leaf, candidates)}", ""
        except Exception:
            pass
        # Confirmed-not-found → negative cache. Skip on TCP failure (transient, not "absent").
        if search_ok:
            self._negative_path_cache[path] = time.monotonic() + self._NEGATIVE_PATH_TTL
            if len(self._negative_path_cache) > 256:
                cutoff = sorted(self._negative_path_cache.values())[128]
                self._negative_path_cache = {k: v for k, v in self._negative_path_cache.items() if v > cutoff}
        return path, ""

    def find_from_cache(self, name: str | None) -> str | None:
        """Return paths from cache matching name as last segment. None if no hit."""
        if not name or not self.known_paths:
            return None
        matches = [p for p in self.known_paths if ":" not in p and p.split("/")[-1] == name]
        if not matches:
            return None
        return "\n".join(sorted(matches))
