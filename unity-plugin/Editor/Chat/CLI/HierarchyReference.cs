// Value object for hierarchy references: path + transient object ID + GlobalObjectId.
// Supports both legacy "path #id" and new "path#id@goid" formats.
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
            int hashIndex = working.LastIndexOf(" #");
            if (hashIndex >= 0)
            {
                var token = working.Substring(hashIndex + 2);
                if (TransientObjectId.TryParse(token, out _))
                {
                    objectId = token;
                    working = working.Substring(0, hashIndex).TrimEnd();
                }
            }
            else
            {
                hashIndex = working.LastIndexOf('#');
                var token = hashIndex >= 0 ? working.Substring(hashIndex + 1) : "";
                if (hashIndex >= 0 && TransientObjectId.TryParse(token, out _))
                {
                    objectId = token;
                    working = working.Substring(0, hashIndex).TrimEnd();
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

            // 2. Process-local object ID.
            if (!string.IsNullOrEmpty(reference.ObjectId) && reference.ObjectId != "0")
            {
                var obj = TransientObjectId.Resolve(reference.ObjectId);
                if (obj is GameObject go) return go;
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
