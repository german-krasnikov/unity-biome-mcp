"""SchemaCache: lazy LRU cache for component property schemas."""
from collections import OrderedDict


class SchemaCache:
    """LRU cache mapping component type → frozenset of property names."""

    def __init__(self, max_size: int = 256) -> None:
        self._max = max_size
        self._data: OrderedDict[str, frozenset] = OrderedDict()

    def get(self, type_name: str) -> frozenset | None:
        """Return cached props or None if not cached."""
        if type_name not in self._data:
            return None
        self._data.move_to_end(type_name)
        return self._data[type_name]

    def put(self, type_name: str, props: frozenset) -> None:
        """Cache props for type. Empty frozenset means 'type not found'."""
        if type_name in self._data:
            self._data.move_to_end(type_name)
        else:
            if len(self._data) >= self._max:
                self._data.popitem(last=False)
        self._data[type_name] = props

    def invalidate_all(self) -> None:
        self._data.clear()

    @staticmethod
    def parse(schema_text: str) -> frozenset:
        """Parse 'Schema: T\\n  prop: Type\\n...' → frozenset of prop names.

        Returns empty frozenset on 'Type not found' or unparseable text.
        """
        if not schema_text or "Type not found" in schema_text or "Cannot instantiate" in schema_text:
            return frozenset()
        props: list[str] = []
        for line in schema_text.splitlines():
            stripped = line.strip()
            # Skip header line 'Schema: TypeName'
            if stripped.startswith("Schema:"):
                continue
            if ":" in stripped and not stripped.startswith("#"):
                prop_name = stripped.split(":")[0].strip()
                if prop_name:
                    props.append(prop_name)
        return frozenset(props)
