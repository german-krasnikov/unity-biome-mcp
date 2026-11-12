"""Mechanics of a generated wire record (tools/generated/wire_records.py): the cast of a field and the
reader/writer of `[head|]key=value|key=value`.

It holds no fact about any record; the fields, their order, their types and the lead word are in
dsl/protocol.biome (printed through dsl/constructs/record.construct).
"""
_CAST = {"int": int, "float1": float, "bool": {"true": True, "false": False}.__getitem__, "word": str, "text": str}
_FORM = {"int": str, "float1": lambda v: f"{v:.1f}", "bool": lambda v: "true" if v else "false", "word": str, "text": str}


def parser(cls, fields, head=None):
    """A line -> cls, or None: another lead word, a declared field twice or not a value of its type, a required field missing.
    A key that is not declared is left out (a newer plugin may print more)."""
    kinds = {f[0]: f[1] for f in fields}
    required = [f[0] for f in fields if len(f) == 2]

    def parse(line):
        if not isinstance(line, str):
            return None
        parts = line.split("|")
        if head is not None:
            if parts.pop(0) != head:
                return None
        got = {}
        for part in parts:
            key, eq, value = part.partition("=")
            if not eq:
                return None
            if key in kinds:
                if key in got:
                    return None
                try:
                    got[key] = _CAST[kinds[key]](value)
                except (KeyError, ValueError):
                    return None
        return cls(**got) if all(k in got for k in required) else None
    return parse


def formatter(fields, head=None):
    def fmt(**values):
        parts = [head] if head is not None else []
        parts += [f"{name}={_FORM[kind](values[name])}" for name, kind, *_ in fields if values.get(name) is not None]
        return "|".join(parts)
    return fmt
