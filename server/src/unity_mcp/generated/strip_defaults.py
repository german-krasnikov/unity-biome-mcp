# Generated from dsl/data/stripdefaults.data-contract
# source-sha256=920760e5585ddfaf65ae65a822fd7b7ad79cde58880b8b0dff40d2caa4bc33d0
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class StripDefault:
    value: str


@dataclass(frozen=True, slots=True, kw_only=True)
class FieldSpecificDefault:
    fieldKey: str
    value: str


STRIP_DEFAULT_ROWS: tuple[StripDefault, ...] = (
    StripDefault(value="0"),
    StripDefault(value="0.0"),
    StripDefault(value="false"),
    StripDefault(value="null"),
    StripDefault(value="None"),
    StripDefault(value="\"\""),
    StripDefault(value="(0, 0, 0)"),
    StripDefault(value="(0.0, 0.0, 0.0)"),
    StripDefault(value="(0, 0, 0, 1)"),
    StripDefault(value="(0.0, 0.0, 0.0, 1.0)"),
    StripDefault(value="(1, 1, 1)"),
    StripDefault(value="(1.0, 1.0, 1.0)"),
    StripDefault(value="[]"),
    StripDefault(value="#00000000"),
    StripDefault(value="Untagged"),
    StripDefault(value="Default"),
    StripDefault(value="(0, 0)"),
    StripDefault(value="(0, 0, 0, 0)"),
    StripDefault(value="#FFFFFFFF"),
)


FIELD_SPECIFIC_DEFAULT_ROWS: tuple[FieldSpecificDefault, ...] = (
    FieldSpecificDefault(fieldKey="m_mass", value="1"),
    FieldSpecificDefault(fieldKey="m_mass", value="1.0"),
    FieldSpecificDefault(fieldKey="m_layer", value="0"),
    FieldSpecificDefault(fieldKey="m_isstatic", value="false"),
    FieldSpecificDefault(fieldKey="m_isstatic", value="False"),
)
