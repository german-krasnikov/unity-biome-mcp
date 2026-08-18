"""Lazy tool-schema registry — captures full schemas, formats plain-text summaries.

Module-level singleton `_registry` is used by server.py to capture + resolve.
"""

STUB_SCHEMA: dict = {"type": "object", "properties": {}, "additionalProperties": False}


class SchemaRegistry:
    def __init__(self) -> None:
        self._data: dict[str, dict] = {}  # name -> {inputSchema, description, annotations?}

    def capture(
        self,
        name: str,
        input_schema: dict,
        description: str,
        annotations: dict | None = None,
    ) -> None:
        entry: dict = {"inputSchema": input_schema, "description": description}
        if annotations:
            entry["annotations"] = annotations
        self._data[name] = entry

    def get_full(self, name: str) -> dict | None:
        return self._data.get(name)

    def known_names(self) -> list[str]:
        return list(self._data.keys())

    def format_text(self, names: list[str]) -> str:
        """Return plain-text schema block for each known name (unknown silently skipped)."""
        parts: list[str] = []
        for name in names:
            entry = self._data.get(name)
            if entry is None:
                continue
            lines = [f"== {name} ==", entry["description"]]
            schema = entry.get("inputSchema", {})
            props = schema.get("properties", {})
            required = set(schema.get("required", []))
            if props:
                param_parts: list[str] = []
                for pname, pdef in props.items():
                    ptype = pdef.get("type", "any")
                    star = "*" if pname in required else ""
                    enum = pdef.get("enum")
                    if enum:
                        param_parts.append(f"{pname}{star}:{ptype}({','.join(str(e) for e in enum)})")
                    else:
                        param_parts.append(f"{pname}{star}:{ptype}")
                lines.append("Params: " + " ".join(param_parts))
            parts.append("\n".join(lines))
        return "\n\n".join(parts)


# Module-level singleton
_registry = SchemaRegistry()
