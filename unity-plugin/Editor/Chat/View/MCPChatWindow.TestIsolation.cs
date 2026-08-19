#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private static TestIsolationScope _activeTestIsolation;

        internal static bool HasActiveTestIsolation => _activeTestIsolation != null;

        internal static bool IsTestIsolationOwnedBy(string ownerId) =>
            _activeTestIsolation != null &&
            string.Equals(_activeTestIsolation.OwnerId, ownerId, StringComparison.Ordinal);

        internal static IDisposable BeginTestIsolation(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                throw new ArgumentException(
                    "A test-isolation owner id is required.", nameof(ownerId));
            if (_activeTestIsolation != null &&
                !IsTestIsolationOwnedBy(ownerId))
                throw new InvalidOperationException(
                    "MCPChatWindow test isolation is owned by another test.");

            var scope = new TestIsolationScope(_activeTestIsolation, ownerId);
            _activeTestIsolation = scope;
            return scope;
        }

        internal static bool RepairOrphanedTestIsolation(string currentOwnerId)
        {
            if (string.IsNullOrEmpty(currentOwnerId))
                throw new ArgumentException(
                    "A test-isolation owner id is required.", nameof(currentOwnerId));
            if (_activeTestIsolation == null ||
                IsTestIsolationOwnedBy(currentOwnerId))
                return false;

            var errors = new List<Exception>();
            while (_activeTestIsolation != null)
            {
                var orphanedScope = _activeTestIsolation;
                try { orphanedScope.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }

            if (errors.Count > 0)
                throw new AggregateException(
                    "MCPChatWindow orphaned test isolation required repair.", errors);
            return true;
        }

        private sealed class TestIsolationScope : IDisposable
        {
            private readonly TestIsolationScope _previous;
            private readonly HashSet<MCPChatWindow> _baselineWindows;
            private readonly Func<string, IChatBackend> _backendFactory;
            private readonly Func<string, string> _colorResolver;
            private readonly Action<ChipData> _addToContext;
            private readonly Action<string, string> _regionCommitted;
            private readonly Action<string, string> _annotationCommitted;
            private readonly Action<string> _screenshotCaptured;
            private readonly Action<string, string> _annotationReady;
            private readonly Action _copyFlash;
            private readonly ChipData[] _pendingChips;
            private readonly SessionStringSnapshot _transcript;
            private readonly SessionStringSnapshot _backendSession;
            private bool _disposed;

            internal TestIsolationScope(TestIsolationScope previous, string ownerId)
            {
                _previous = previous;
                OwnerId = ownerId;
                _baselineWindows = new HashSet<MCPChatWindow>(CurrentWindows());
                _backendFactory = BackendFactoryForTest;
                _colorResolver = ChipPillFactory.ColorResolver;
                _addToContext = ChipPillFactory.AddToContextAction;
                _regionCommitted = RegionTool.SceneRegionTool.OnRegionCommitted;
                _annotationCommitted = RegionTool.SceneAnnotationTool.OnAnnotationCommitted;
                _screenshotCaptured = ScreenshotToolbarButton.OnScreenshotCaptured;
                _annotationReady = Annotation.AnnotationEditorWindow.OnAnnotationReady;
                _copyFlash = CopyFlash.ShowAction;
                _pendingChips = ChipPillFactory.PendingChips.ToArray();
                _transcript = SessionStringSnapshot.Capture(PrefKeys.ChatTranscript);
                _backendSession = SessionStringSnapshot.Capture(
                    PrefKeys.ChatBackendSessionId);
            }

            internal string OwnerId { get; }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_activeTestIsolation, this))
                    throw new InvalidOperationException(
                        "MCPChatWindow test-isolation scopes must be disposed in reverse order.");

                var errors = new List<Exception>();
                var currentWindows = Array.Empty<MCPChatWindow>();
                TryRestore(() => currentWindows = CurrentWindows().ToArray(), errors);
                foreach (var window in currentWindows.Where(window =>
                             !_baselineWindows.Contains(window)))
                {
                    TryRestore(() =>
                    {
                        if (window == null) return;
                        try { window.Close(); }
                        catch (Exception) { /* m_Parent null — window was never shown */ }
                        if (window != null)
                            UnityEngine.Object.DestroyImmediate(window);
                    }, errors);
                }

                foreach (var baseline in _baselineWindows)
                {
                    if (baseline == null)
                        errors.Add(new InvalidOperationException(
                            "A test destroyed an MCPChatWindow that existed before the test."));
                }

                TryRestore(() => BackendFactoryForTest = _backendFactory, errors);
                TryRestore(() => ChipPillFactory.ColorResolver = _colorResolver, errors);
                TryRestore(() => ChipPillFactory.AddToContextAction = _addToContext, errors);
                TryRestore(() => RegionTool.SceneRegionTool.OnRegionCommitted =
                    _regionCommitted, errors);
                TryRestore(() => RegionTool.SceneAnnotationTool.OnAnnotationCommitted =
                    _annotationCommitted, errors);
                TryRestore(() => ScreenshotToolbarButton.OnScreenshotCaptured =
                    _screenshotCaptured, errors);
                TryRestore(() => Annotation.AnnotationEditorWindow.OnAnnotationReady =
                    _annotationReady, errors);
                TryRestore(() => CopyFlash.ShowAction = _copyFlash, errors);
                TryRestore(() =>
                {
                    ChipPillFactory.PendingChips.Clear();
                    foreach (var chip in _pendingChips)
                        ChipPillFactory.PendingChips.Enqueue(chip);
                }, errors);
                TryRestore(_transcript.Restore, errors);
                TryRestore(_backendSession.Restore, errors);

                _activeTestIsolation = _previous;
                _disposed = true;
                if (errors.Count > 0)
                    throw new AggregateException(
                        "MCPChatWindow test isolation could not restore exact state.", errors);
            }

            private static IEnumerable<MCPChatWindow> CurrentWindows() =>
                Resources.FindObjectsOfTypeAll<MCPChatWindow>()
                    .Where(window => window != null);

            private static void TryRestore(Action action, ICollection<Exception> errors)
            {
                try { action(); }
                catch (Exception error) { errors.Add(error); }
            }
        }

        private readonly struct SessionStringSnapshot
        {
            private readonly string _key;
            private readonly bool _exists;
            private readonly string _value;

            private SessionStringSnapshot(string key, bool exists, string value)
            {
                _key = key;
                _exists = exists;
                _value = value;
            }

            internal static SessionStringSnapshot Capture(string key)
            {
                var sentinel = "UnityMCP.SessionState.Absent." + Guid.NewGuid().ToString("N");
                var value = SessionState.GetString(key, sentinel);
                return new SessionStringSnapshot(key, value != sentinel,
                    value == sentinel ? "" : value);
            }

            internal void Restore()
            {
                if (_exists) SessionState.SetString(_key, _value);
                else SessionState.EraseString(_key);
            }
        }
    }
}
#endif
