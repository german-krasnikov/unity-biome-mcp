// Parse a raw [kind:ref] reference into ChipData (Path, ObjectId, DisplayName).
// Inverse of ChipContextResolver.FormatChipRef.
// Hierarchy: "/Root/Child$3039@goid" -> parsed via HierarchyReference.
// Asset:     "Assets/Scripts/Foo.cs" -> Path=same, ID=empty, Display="Foo.cs".
namespace UnityMCP.Editor.Chat
{
    internal static class RefParser
    {
        internal static ChipData Parse(string kindKey, string rawRef)
        {
            if (kindKey == ChipKindKeys.Hierarchy)
            {
                var href = HierarchyReference.Parse(rawRef);
                var path = href.Path;
                var leaf = path;
                int lastSlash = path.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < path.Length - 1)
                    leaf = path.Substring(lastSlash + 1);
                return new ChipData(kindKey, path, leaf, href.ObjectId);
            }

            var pathOnly = rawRef;
            string objectId = "";

            // Strip " $HEX" suffix (defensive for non-hierarchy chips).
            int dollarIdx = rawRef.LastIndexOf(" $");
            if (dollarIdx >= 0)
            {
                var dollarToken = rawRef.Substring(dollarIdx + 1); // "$2B678"
                if (TransientObjectId.TryParse(dollarToken, out _))
                {
                    pathOnly = rawRef.Substring(0, dollarIdx);
                    objectId = dollarToken;
                }
            }

            // Leaf name: after last '/'
            var leafName = pathOnly;
            int lastSlash2 = pathOnly.LastIndexOf('/');
            if (lastSlash2 >= 0 && lastSlash2 < pathOnly.Length - 1)
                leafName = pathOnly.Substring(lastSlash2 + 1);

            return new ChipData(kindKey, pathOnly, leafName, objectId);
        }
    }
}
