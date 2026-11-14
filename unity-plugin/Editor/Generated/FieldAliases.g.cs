// Generated from dsl/data/fieldaliases.data-contract, dsl/data/fieldaliases.core-data
// source-sha256=241363a34bd3e81c7f7b854ed2392651b9cd16c7eaa1ceff5795a7855ccb72fa
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class FieldAlias
    {
        public readonly string Alias;
        public readonly string Canonical;
        public FieldAlias(string alias, string canonical)
        {
            this.Alias = alias;
            this.Canonical = canonical;
        }
    }

    public static class FieldAliasRows
    {
        public static readonly FieldAlias[] All =
        {
            new FieldAlias(alias: "position", canonical: "m_localposition"),
            new FieldAlias(alias: "localposition", canonical: "m_localposition"),
            new FieldAlias(alias: "rotation", canonical: "m_localrotation"),
            new FieldAlias(alias: "localrotation", canonical: "m_localrotation"),
            new FieldAlias(alias: "scale", canonical: "m_localscale"),
            new FieldAlias(alias: "localscale", canonical: "m_localscale"),
            new FieldAlias(alias: "mass", canonical: "m_mass"),
            new FieldAlias(alias: "enabled", canonical: "m_enabled"),
            new FieldAlias(alias: "active", canonical: "m_isactive"),
            new FieldAlias(alias: "name", canonical: "m_name"),
            new FieldAlias(alias: "tag", canonical: "m_tagstring"),
            new FieldAlias(alias: "layer", canonical: "m_layer"),
        };
    }
}
