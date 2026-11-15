using System.Collections.Generic;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class PlaytestStepValidator
    {
        public static string GetValidationError(VisualStep step)
        {
            if (step == null) return "step is null";
            if (!PlaytestDslExporter.IsSupportedType(step.type))
                return string.IsNullOrEmpty(step.rawLine)
                    ? $"unsupported step type: {step.type}"
                    : null;
            return step.type switch
            {
                StepType.Move or StepType.Teleport =>
                    string.IsNullOrEmpty(step.path) && step.position == Vector3.zero
                        ? "path or position required" : null,
                StepType.Wait =>
                    step.delay <= 0 ? "delay must be > 0" : null,
                StepType.TimeScale =>
                    step.delay < 0 ? "time scale must be >= 0" : null,
                StepType.WaitUntil =>
                    string.IsNullOrEmpty(step.query) ? "query required"
                    : step.timeout <= 0 ? "timeout must be > 0" : null,
                StepType.Assert =>
                    string.IsNullOrEmpty(step.query) ? "query required" : null,
                StepType.Invoke =>
                    string.IsNullOrEmpty(step.path) ? "path required"
                    : string.IsNullOrEmpty(step.component) ? "component required"
                    : string.IsNullOrEmpty(step.method) ? "method required" : null,
                StepType.Set =>
                    string.IsNullOrEmpty(step.path) ? "path required"
                    : string.IsNullOrEmpty(step.component) ? "component required"
                    : string.IsNullOrEmpty(step.method) ? "field required" : null,
                StepType.Click =>
                    string.IsNullOrEmpty(step.path) ? "path required" : null,
                StepType.Capture =>
                    string.IsNullOrEmpty(step.message) ? "label required"
                    : string.IsNullOrEmpty(step.query) ? "query required" : null,
                StepType.Invariant =>
                    string.IsNullOrEmpty(step.query) ? "query required" : null,
                StepType.AssertCaptured =>
                    string.IsNullOrEmpty(step.message) ? "label required" : null,
                StepType.AssertNear =>
                    string.IsNullOrEmpty(step.path)  ? "path A required"
                    : string.IsNullOrEmpty(step.value) ? "path B required" : null,
                _ => null
            };
        }

        public static bool IsScriptValid(List<VisualStep> steps)
        {
            if (steps == null || steps.Count == 0) return true;
            foreach (var s in steps)
                if (GetValidationError(s) != null) return false;
            return true;
        }
    }
}
