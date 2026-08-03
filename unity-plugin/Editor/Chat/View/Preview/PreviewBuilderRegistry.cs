// Registry of IPreviewBuilder instances. Lower priority = checked first.
// Adding a new media type = one file implementing IPreviewBuilder + one Register() call.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class PreviewBuilderRegistry
    {
        sealed class Entry
        {
            public readonly IPreviewBuilder Builder;
            public readonly int Priority;
            public Entry(IPreviewBuilder builder, int priority)
            {
                Builder  = builder;
                Priority = priority;
            }
        }

        static readonly List<Entry> _entries = new();
        static int _version;

        public static int Version => _version;

        /// <summary>Register a builder. Lower priority is checked first.</summary>
        public static void Register(IPreviewBuilder builder, int priority = 0)
        {
            if (builder == null) return;
            _entries.Add(new Entry(builder, priority));
            _entries.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _version++;
        }

        /// <summary>Remove a previously registered builder. Returns true if found.</summary>
        public static bool Unregister(IPreviewBuilder builder)
        {
            if (builder == null) return false;
            var removed = _entries.RemoveAll(e => ReferenceEquals(e.Builder, builder));
            if (removed > 0) { _version++; return true; }
            return false;
        }

        /// <summary>Find the first builder that CanBuild the request.</summary>
        public static IPreviewBuilder Resolve(string kindKey, string path)
        {
            foreach (var e in _entries)
                if (e.Builder.CanBuild(kindKey, path))
                    return e.Builder;
            return null;
        }

        /// <summary>Build a preview, or return null if no builder handles the request.</summary>
        public static VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var builder = Resolve(request.KindKey, request.Path);
            return builder?.Build(request, context);
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Preserves builder identity, priority ordering and the exact observable version.
        /// </summary>
        internal static IDisposable PreserveStateForTests()
            => new TestIsolationScope(new List<Entry>(_entries), _version);

        private sealed class TestIsolationScope : IDisposable
        {
            private List<Entry> _entriesSnapshot;
            private readonly int _versionSnapshot;

            internal TestIsolationScope(List<Entry> entriesSnapshot, int versionSnapshot)
            {
                _entriesSnapshot = entriesSnapshot;
                _versionSnapshot = versionSnapshot;
            }

            public void Dispose()
            {
                if (_entriesSnapshot == null) return;

                _entries.Clear();
                _entries.AddRange(_entriesSnapshot);
                _version = _versionSnapshot;
                _entriesSnapshot = null;
            }
        }
#endif

        /// <summary>TEST-ONLY: clear all registrations.</summary>
        public static void Reset()
        {
            _entries.Clear();
            _version++;
        }
    }
}
