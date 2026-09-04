"""C14: thin, frozen DTO for selecting a subset of Biome's test corpus.

Scope cut (plan R-08): no C# mirror, no Python<->C# key-parity test.
Waves C-E have no C# consumer of this shape -- `TestSelectionFilter` gains
its C# mirror only at the first real C# consumer (Wave F "first external
extension"), landing together with the parity test per
`.claude/skills/api-design-standards.md`.

All 8 fields are free-form `list[str]`/`bool` leaves -- no enum types for
risks/surfaces (that would be a 9th place needing updates). Values are
validated against `Tests/taxonomy-map.json`'s dimension vocabulary later,
by C18's static lint -- not by this dataclass.
"""
from dataclasses import dataclass, field

_FIELD_NAMES = (
    "layers",
    "modes",
    "environments",
    "speeds",
    "include_tags",
    "exclude_tags",
    "exclude_capabilities",
    "allow_empty",
)


@dataclass(frozen=True)
class TestSelectionFilter:
    __test__ = False  # prevent pytest from treating this as a test class

    layers: list[str] = field(default_factory=list)
    modes: list[str] = field(default_factory=list)
    environments: list[str] = field(default_factory=list)
    speeds: list[str] = field(default_factory=list)
    include_tags: list[str] = field(default_factory=list)
    exclude_tags: list[str] = field(default_factory=list)
    exclude_capabilities: list[str] = field(default_factory=list)
    allow_empty: bool = False

    def to_dict(self) -> dict[str, list[str] | bool]:
        return {name: getattr(self, name) for name in _FIELD_NAMES}

    @classmethod
    def from_dict(cls, data: dict) -> TestSelectionFilter:
        unknown = sorted(set(data) - set(_FIELD_NAMES))
        if unknown:
            raise ValueError(f"Unknown TestSelectionFilter key(s): {unknown}")
        return cls(**data)
