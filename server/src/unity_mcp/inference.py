"""Argument inference: smart defaults from session context.

Enable with UNITY_MCP_INFERENCE=1.
"""
from collections import Counter, deque
from dataclasses import dataclass, field

PRIMITIVES = ["Cube", "Sphere", "Cylinder", "Capsule", "Plane", "Quad"]


def infer_primitive(name: str) -> str | None:
    name_lower = name.lower()
    for p in PRIMITIVES:
        if name_lower.endswith(p.lower()):
            return p
    return None


def infer_parent(ctx: "SessionContext") -> str | None:
    if len(ctx.recent_creates) < 3:
        return None
    parents = [c[2] for c in ctx.recent_creates if c[2]]
    if len(parents) < 3:
        return None
    most_common, count = Counter(parents).most_common(1)[0]
    return most_common if count >= 3 else None


@dataclass
class SessionContext:
    last_path: str | None = None
    last_component: str | None = None
    recent_creates: deque = field(default_factory=lambda: deque(maxlen=5))

    def record(self, cmd: str, args: dict, result: str) -> None:
        if "path" in args:
            self.last_path = args["path"]
        if cmd == "get_component" and "type" in args:
            self.last_component = args["type"]
        if cmd == "create_object":
            name = args.get("name", "")
            parent = args.get("parent", "")
            full_path = f"{parent}/{name}" if parent else f"/{name}"
            self.recent_creates.append((name, full_path, parent))


class Inferrer:
    def infer(self, cmd: str, args: dict, ctx: SessionContext) -> tuple[dict, list[str]]:
        if cmd != "create_object":
            return args, []
        new_args = dict(args)
        tags: list[str] = []
        if not args.get("primitive") and not args.get("prefab_path"):
            p = infer_primitive(args.get("name", ""))
            if p:
                new_args["primitive"] = p
                tags.append(f"primitive={p}")
        if not args.get("parent"):
            parent = infer_parent(ctx)
            if parent:
                new_args["parent"] = parent
                tags.append(f"parent={parent}")
        return new_args, tags
