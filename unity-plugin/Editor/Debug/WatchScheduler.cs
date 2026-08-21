using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class WatchScheduler
    {
        private const int MaxErrors = 5;

        static WatchScheduler()
        {
            WatchRegistry.Load();
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += WatchRegistry.Save;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static void Stop() => EditorApplication.update -= Tick;

#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: re-runs what [InitializeOnLoad] does — restores watches
        /// and re-subscribes Tick. Avoids double-subscription via remove-then-add.</summary>
        internal static void ReRegisterForTest()
        {
            WatchRegistry.Load();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
#endif

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (WatchRegistry.All.Count == 0) return;

            float now = Time.realtimeSinceStartup;
            foreach (var (id, watch) in WatchRegistry.All.ToArray())
            {
                if (now - watch.LastSampleTime < watch.IntervalMs / 1000f) continue;
                watch.LastSampleTime = now;

                try
                {
                    var value = WatchEvaluator.ReadValue(watch.Path, watch.Component, watch.Field);
                    string valueStr = value?.ToString() ?? "null";
                    string prevStr = watch.LastValue?.ToString() ?? "null";

                    if (prevStr != valueStr)
                    {
                        watch.ChangeCount++;
                        WatchRegistry.AddLogEntry(
                            $"[{DateTime.Now:HH:mm:ss}] {id} {watch.Field} changed: {prevStr} → {valueStr}");
                        watch.LastValue = value;
                    }

                    if (!watch.Triggered && WatchCondition.Evaluate(watch.Condition, value))
                    {
                        watch.Triggered = true;
                        WatchRegistry.AddLogEntry(
                            $"[{DateTime.Now:HH:mm:ss}] {id} TRIGGERED: {watch.Field}={valueStr}");
                        if (watch.Action == "pause")
                            EditorApplication.isPaused = true;
                    }

                    watch.ErrorCount = 0;
                }
                catch (Exception e)
                {
                    watch.ErrorCount++;
                    watch.LastSampleTime = now + 5f;  // backoff: skip 5s before next attempt
                    WatchRegistry.AddLogEntry(
                        $"[{DateTime.Now:HH:mm:ss}] {id} ERROR: {e.Message}");
                    if (watch.ErrorCount >= MaxErrors)
                    {
                        WatchRegistry.Remove(id);
                        WatchRegistry.AddLogEntry(
                            $"[{DateTime.Now:HH:mm:ss}] {id} auto-removed after {MaxErrors} errors");
                    }
                }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                foreach (var entry in WatchRegistry.All.Values)
                {
                    entry.LastValue = null;
                    entry.Triggered = false;
                    entry.LastSampleTime = 0f;
                    entry.ChangeCount = 0;
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Reset runtime state, keep Triggered/ChangeCount for post-play inspection
                foreach (var entry in WatchRegistry.All.Values)
                {
                    entry.LastValue = null;
                    entry.LastSampleTime = 0f;
                }
            }
        }
    }
}
