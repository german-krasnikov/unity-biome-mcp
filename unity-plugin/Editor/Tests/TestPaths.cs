using System;
using System.IO;
using UnityEditor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    public static class TestPaths
    {
        public const string Root = UnityMcpTestAssetOwnership.Root;

        public static string ForFixture(string className)
        {
            if (string.IsNullOrWhiteSpace(className)
                || className.Contains("/")
                || className.Contains("\\")
                || className.Contains(".."))
                throw new ArgumentException("Fixture names must be a single safe path segment.", nameof(className));

            return $"{Root}/{className}";
        }

        // Segment-walk: creates nested folders without auto-suffix bug
        public static string EnsureFolder(string assetPath)
        {
            if (string.Equals(assetPath, Root, StringComparison.Ordinal))
                return UnityMcpTestAssetOwnership.EnsureOwnedRoot();

            RequireOwnedPath(assetPath);
            UnityMcpTestAssetOwnership.EnsureOwnedRoot();
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(next))
                        throw new IOException($"Could not create test-owned folder '{next}'.");
                }
                current = next;
            }
            return assetPath;
        }

        // Backwards-compat alias (no-arg) used by MultiSceneTestBase
        public static void EnsureRoot() => UnityMcpTestAssetOwnership.EnsureOwnedRoot();

        // Backwards-compat no-arg overload for existing callers
        public static void EnsureFolder() => EnsureRoot();

        public static bool IsOwnedPath(string assetPath)
        {
            return UnityMcpTestAssetOwnership.IsOwnedPath(assetPath);
        }

        public static void RequireOwnedPath(string assetPath)
        {
            UnityMcpTestAssetOwnership.RequireOwnedPath(assetPath);
        }

        public static void DeleteOwnedAsset(string assetPath)
        {
            UnityMcpTestAssetOwnership.DeleteOwnedAsset(assetPath);
        }

        // Backwards-compat alias for MultiSceneTestBase
        public const string TempFolder = Root;
    }
}
