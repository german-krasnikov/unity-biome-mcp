// Generated from dsl/data/stripdefaults.data-contract
// source-sha256=920760e5585ddfaf65ae65a822fd7b7ad79cde58880b8b0dff40d2caa4bc33d0
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class StripDefault
    {
        public readonly string Value;
        public StripDefault(string value)
        {
            this.Value = value;
        }
    }

    public sealed class FieldSpecificDefault
    {
        public readonly string FieldKey;
        public readonly string Value;
        public FieldSpecificDefault(string fieldKey, string value)
        {
            this.FieldKey = fieldKey;
            this.Value = value;
        }
    }

    public static class StripDefaultRows
    {
        public static readonly StripDefault[] All =
        {
            new StripDefault(value: "0"),
            new StripDefault(value: "0.0"),
            new StripDefault(value: "false"),
            new StripDefault(value: "null"),
            new StripDefault(value: "None"),
            new StripDefault(value: "\"\""),
            new StripDefault(value: "(0, 0, 0)"),
            new StripDefault(value: "(0.0, 0.0, 0.0)"),
            new StripDefault(value: "(0, 0, 0, 1)"),
            new StripDefault(value: "(0.0, 0.0, 0.0, 1.0)"),
            new StripDefault(value: "(1, 1, 1)"),
            new StripDefault(value: "(1.0, 1.0, 1.0)"),
            new StripDefault(value: "[]"),
            new StripDefault(value: "#00000000"),
            new StripDefault(value: "Untagged"),
            new StripDefault(value: "Default"),
            new StripDefault(value: "(0, 0)"),
            new StripDefault(value: "(0, 0, 0, 0)"),
            new StripDefault(value: "#FFFFFFFF"),
        };
    }

    public static class FieldSpecificDefaultRows
    {
        public static readonly FieldSpecificDefault[] All =
        {
            new FieldSpecificDefault(fieldKey: "m_mass", value: "1"),
            new FieldSpecificDefault(fieldKey: "m_mass", value: "1.0"),
            new FieldSpecificDefault(fieldKey: "m_layer", value: "0"),
            new FieldSpecificDefault(fieldKey: "m_isstatic", value: "false"),
            new FieldSpecificDefault(fieldKey: "m_isstatic", value: "False"),
        };
    }
}
