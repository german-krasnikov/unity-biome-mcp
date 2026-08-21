// Value object for hierarchy references: path + transient object ID + GlobalObjectId.
// Format: "/path$HEX" optionally followed by "@GlobalObjectId".
using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public readonly struct HierarchyReference
    {
        public string Path { get; }
        public string ObjectId { get; }
        public GlobalObjectId GlobalObjectId { get; }

        public HierarchyReference(string path, string objectId, GlobalObjectId globalObjectId)
        {
            Path = path;
            ObjectId = objectId ?? "";
            GlobalObjectId = globalObjectId;
        }

        public HierarchyReference(string path, int legacyId, GlobalObjectId globalObjectId)
            : this(path, legacyId == 0 ? "" : legacyId.ToString(CultureInfo.InvariantCulture), globalObjectId)
        {
        }

        public static HierarchyReference Parse(string rawRef)
        {
            if (string.IsNullOrEmpty(rawRef))
                return new HierarchyReference("", "", default);

            var working = rawRef;
            GlobalObjectId globalObjectId = default;

            int atIndex = working.IndexOf('@');
            if (atIndex >= 0)
            {
                var goidString = working.Substring(atIndex + 1);
                working = working.Substring(0, atIndex);
                GlobalObjectId.TryParse(goidString, out globalObjectId);
            }

            string objectId = "";

            // &ref format — e.g. "/Ground &a" (space before &, so & is not part of an object name).
            // The & must NOT be immediately preceded by a letter/digit — that would indicate it's
            // embedded in a name like "Tom&Jerry", not a ref token.
            int ampIndex = working.LastIndexOf('&');
            if (ampIndex >= 0)
            {
                if (ampIndex == 0 || !char.IsLetterOrDigit(working[ampIndex - 1]))
                {
                    var token = working.Substring(ampIndex);
                    if (RefManager.IsRef(token))
                    {
                        objectId = token;
                        working  = working.Substring(0, ampIndex).TrimEnd();
                    }
                }
            }

            // $HEX format — backward compat, e.g. "/Ground$2B678" ($ attached directly, no space).
            if (string.IsNullOrEmpty(objectId))
            {
                int dollarIndex = working.LastIndexOf('$');
                if (dollarIndex >= 0)
                {
                    var token = working.Substring(dollarIndex); // "$2B678" — includes $
                    if (TransientObjectId.TryParse(token, out _))
                    {
                        objectId = token;
                        working  = working.Substring(0, dollarIndex).TrimEnd();
                    }
                }
            }

            return new HierarchyReference(working, objectId, globalObjectId);
        }
    }

    public interface IHierarchyResolver
    {
        GameObject Resolve(HierarchyReference reference);
    }

    internal sealed class HierarchyResolver : IHierarchyResolver
    {
        public GameObject Resolve(HierarchyReference reference)
        {
            // 1. GlobalObjectId (survives reparent/rename).
            if (reference.GlobalObjectId.targetObjectId != 0)
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(reference.GlobalObjectId);
                if (obj is GameObject go) return go;
            }

            // 2. Process-local object ID — &ref (RefManager) or $HEX (TransientObjectId).
            if (!string.IsNullOrEmpty(reference.ObjectId) && reference.ObjectId != "0")
            {
                if (RefManager.IsRef(reference.ObjectId))
                {
                    var go = RefManager.Resolve(reference.ObjectId);
                    if (go != null) return go;
                }
                else
                {
                    var obj = TransientObjectId.Resolve(reference.ObjectId);
                    if (obj is GameObject go) return go;
                }
            }

            // 3. Exact path.
            if (!string.IsNullOrEmpty(reference.Path))
            {
                var go = SceneObjectFinder.FindGameObject(reference.Path);
                if (go != null) return go;

                // 4. Fuzzy name match on the leaf.
                var leaf = reference.Path;
                int slash = leaf.LastIndexOf('/');
                if (slash >= 0 && slash < leaf.Length - 1)
                    leaf = leaf.Substring(slash + 1);

                if (!string.IsNullOrEmpty(leaf))
                {
                    var fuzzy = GameObject.Find(leaf);
                    if (fuzzy != null) return fuzzy;
                }
            }

            return null;
        }
    }
}
