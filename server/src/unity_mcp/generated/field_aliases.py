# Generated from dsl/data/fieldaliases.data-contract, dsl/data/fieldaliases.core-data
# source-sha256=241363a34bd3e81c7f7b854ed2392651b9cd16c7eaa1ceff5795a7855ccb72fa
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class FieldAlias:
    alias: str
    canonical: str


FIELD_ALIAS_ROWS: tuple[FieldAlias, ...] = (
    FieldAlias(alias="position", canonical="m_localposition"),
    FieldAlias(alias="localposition", canonical="m_localposition"),
    FieldAlias(alias="rotation", canonical="m_localrotation"),
    FieldAlias(alias="localrotation", canonical="m_localrotation"),
    FieldAlias(alias="scale", canonical="m_localscale"),
    FieldAlias(alias="localscale", canonical="m_localscale"),
    FieldAlias(alias="mass", canonical="m_mass"),
    FieldAlias(alias="enabled", canonical="m_enabled"),
    FieldAlias(alias="active", canonical="m_isactive"),
    FieldAlias(alias="name", canonical="m_name"),
    FieldAlias(alias="tag", canonical="m_tagstring"),
    FieldAlias(alias="layer", canonical="m_layer"),
)
