// Parse a raw [kind:ref] reference into ChipData (Path, ObjectId, DisplayName).
// Inverse of ChipContextResolver.FormatChipRef.
// Hierarchy: "/Root/Child #-33506" -> Path="/Root/Child", ID=-33506, Display="Child".
// Hierarchy (new): "/Root/Child#123@goid" -> parsed via HierarchyReference.
// Asset:     "Assets/Scripts/Foo.cs" -> Path=same, ID=0, Display="Foo.cs".
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

            // Strip " #id" suffix (hierarchy legacy refs handled above, but keep for safety).
            int hashIdx = rawRef.LastIndexOf(" #");
            var token = hashIdx >= 0 ? rawRef.Substring(hashIdx + 2) : "";
            if (hashIdx >= 0 && TransientObjectId.TryParse(token, out _))
            {
                pathOnly   = rawRef.Substring(0, hashIdx);
                objectId = token;
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
