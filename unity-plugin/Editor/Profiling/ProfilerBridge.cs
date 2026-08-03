using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Wraps ProfilerRecorder + FrameTimingManager into a single CollectFrame() call.
    /// Lazy: recorder is created only on first call, disposed when no longer needed.
    /// _frameProvider is replaced in tests to avoid real Unity API dependency.
    /// </summary>
    internal static class ProfilerBridge
    {
        internal readonly struct BatchCountSample
        {
            internal bool IsAvailable { get; }
            internal int Value { get; }
            internal string UnavailableReason { get; }

            private BatchCountSample(bool isAvailable, int value, string unavailableReason)
            {
                IsAvailable = isAvailable;
                Value = value;
                UnavailableReason = unavailableReason;
            }

            internal static BatchCountSample Available(long value)
            {
                var clamped = value < 0 ? 0 : value > int.MaxValue ? int.MaxValue : (int)value;
                return new BatchCountSample(true, clamped, null);
            }

            internal static BatchCountSample Unavailable(string reason) =>
                new BatchCountSample(false, -1, string.IsNullOrEmpty(reason) ? "unknown" : reason);
        }

        internal const string BatchCounterName = "Batches Count";
        private const double BatchProbeRetrySeconds = 1.0;

        private static ProfilerRecorder _cpuRecorder;
        private static ProfilerRecorder _batchRecorder;
        private static bool _batchInitializationAttempted;
        private static double _nextBatchProbeTime;
        private static string _batchInitializationFailure;
        private static int _prevGcCount;
        // Cached to avoid per-call heap allocation
        private static readonly FrameTiming[] _timings = new FrameTiming[1];

        // Injected in tests to replace real Unity API
        internal static Func<FrameSample> _frameProvider = CollectFrameReal;
        internal static Func<BatchCountSample> _batchProvider = CollectBatchCountReal;

        internal static FrameSample CollectFrame() => _frameProvider();
        internal static BatchCountSample CollectBatchCount() => _batchProvider();

        private static void EnsureCpuInitialized()
        {
            if (_cpuRecorder.Valid) return;
            _cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _prevGcCount = GC.CollectionCount(0);
        }

        private static void EnsureBatchInitialized()
        {
            if (_batchRecorder.Valid) return;
            var now = EditorApplication.timeSinceStartup;
            if (_batchInitializationAttempted && now < _nextBatchProbeTime) return;
            _batchInitializationAttempted = true;
            _nextBatchProbeTime = now + BatchProbeRetrySeconds;
            try
            {
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (!IsRenderBatchCounter(description.Name, description.Category.Name))
                        continue;

                    _batchRecorder = new ProfilerRecorder(
                        handle,
                        1,
                        ProfilerRecorderOptions.Default |
                        ProfilerRecorderOptions.StartImmediately);
                    break;
                }

                if (!_batchRecorder.Valid)
                    _batchInitializationFailure = "render-counter-not-supported";
                else
                    _batchInitializationFailure = null;
            }
            catch (Exception)
            {
                _batchRecorder = default;
                _batchInitializationFailure = "initialization-failed";
            }
        }

        internal static bool IsRenderBatchCounter(string counterName, string categoryName) =>
            string.Equals(counterName, BatchCounterName, StringComparison.Ordinal) &&
            string.Equals(categoryName, ProfilerCategory.Render.Name, StringComparison.Ordinal);

        internal static void Shutdown()
        {
            if (_cpuRecorder.Valid) _cpuRecorder.Dispose();
            if (_batchRecorder.Valid) _batchRecorder.Dispose();
            _cpuRecorder = default;
            _batchRecorder = default;
            _batchInitializationAttempted = false;
            _nextBatchProbeTime = 0;
            _batchInitializationFailure = null;
        }

        private static BatchCountSample CollectBatchCountReal()
        {
            EnsureBatchInitialized();
            if (!_batchRecorder.Valid)
                return BatchCountSample.Unavailable(
                    _batchInitializationFailure ?? "render-counter-not-supported");

            try
            {
                return _batchRecorder.Count > 0
                    ? BatchCountSample.Available(_batchRecorder.LastValue)
                    : BatchCountSample.Unavailable("no-frame-sample");
            }
            catch (Exception)
            {
                return BatchCountSample.Unavailable("read-failed");
            }
        }

        private static FrameSample CollectFrameReal()
        {
            EnsureCpuInitialized();
            var batches = CollectBatchCountReal();
            float dt = Time.unscaledDeltaTime;
            float cpuMs = _cpuRecorder.Valid ? _cpuRecorder.LastValue / 1_000_000f : 0f;

            float gpuMs = -1f;
            FrameTimingManager.CaptureFrameTimings();
            uint timingCount = FrameTimingManager.GetLatestTimings(1, _timings);
            if (timingCount > 0 && _timings[0].gpuFrameTime > 0)
                gpuMs = (float)_timings[0].gpuFrameTime;

            int gcNow = GC.CollectionCount(0);
            int gcDelta = Math.Max(0, gcNow - _prevGcCount);
            _prevGcCount = gcNow;

            return new FrameSample
            {
                DeltaTime = dt,
                CpuMs = cpuMs,
                GpuMs = gpuMs,
                GcGen0Count = gcDelta,
                MonoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                DrawCalls = UnityStats.drawCalls,
                // -1 is the FrameSample-level unknown sentinel; command output keeps the reason.
                Batches = batches.IsAvailable ? batches.Value : -1,
                Triangles = UnityStats.triangles,
                SetPassCalls = UnityStats.setPassCalls,
            };
        }
    }
}
