using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnityMCP.Playtest
{
    public sealed partial class PlayerPlaytestRunner : MonoBehaviour
    {
        private readonly List<StepResult> _results = new();
        private readonly List<string> _consoleErrors = new();
        private string _scriptPath;
        private string _jsonPath;
        private string _junitPath;
        private bool _exitWhenDone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            var script = GetArg(args, "-unityMcpPlaytest");
            if (string.IsNullOrEmpty(script))
                return;

            var go = new GameObject("UnityMCP_PlayerPlaytestRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<PlayerPlaytestRunner>();
            runner.Configure(
                script,
                GetArg(args, "-unityMcpPlaytestJson"),
                GetArg(args, "-unityMcpPlaytestJunit"),
                HasArg(args, "-unityMcpPlaytestExit"));
        }

        public void Configure(string scriptPath, string jsonPath, string junitPath, bool exitWhenDone)
        {
            _scriptPath = scriptPath;
            _jsonPath = jsonPath;
            _junitPath = junitPath;
            _exitWhenDone = exitWhenDone;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var started = DateTime.UtcNow;
            var failed = 0;
            var script = File.ReadAllText(_scriptPath);
            var steps = ParseSteps(script);
            if (steps != null)
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    var rawStep = steps[i];
                    var before = _results.Count;
                    yield return Execute(rawStep);
                    if (_results.Count == before)
                    {
                        failed++;
                        _results.Add(StepResult.Fail(rawStep, "step produced no result"));
                    }
                    else if (!_results[_results.Count - 1].Passed)
                    {
                        failed++;
                    }
                }
            }

            Application.logMessageReceived -= OnLog;
            var duration = (DateTime.UtcNow - started).TotalSeconds;
            WriteReceipts(failed, duration);
            if (_exitWhenDone)
                Application.Quit(failed == 0 ? 0 : 1);
        }

        private IEnumerator Execute(string rawStep)
        {
            var step = rawStep.Trim();
            if (step.StartsWith("LOG ", StringComparison.OrdinalIgnoreCase))
            {
                _results.Add(StepResult.Pass(step, step.Substring(4).Trim()));
                yield break;
            }
            if (step.StartsWith("WAIT ", StringComparison.OrdinalIgnoreCase))
            {
                yield return Wait(step);
                yield break;
            }
            if (step.Equals("ASSERT_CONSOLE_CLEAN", StringComparison.OrdinalIgnoreCase))
            {
                _results.Add(_consoleErrors.Count == 0
                    ? StepResult.Pass(step, "console clean")
                    : StepResult.Fail(step, string.Join("\\n", _consoleErrors)));
                yield break;
            }
            if (step.StartsWith("ASSERT ", StringComparison.OrdinalIgnoreCase))
            {
                _results.Add(EvaluateAssert(step));
                yield break;
            }
            if (step.StartsWith("WAIT_UNTIL ", StringComparison.OrdinalIgnoreCase))
            {
                yield return WaitUntil(step);
                yield break;
            }
            _results.Add(StepResult.Fail(step, "unsupported player PlayTest command"));
        }

        private IEnumerator Wait(string step)
        {
            if (!float.TryParse(step.Substring(5).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var delay))
            {
                _results.Add(StepResult.Fail(step, "invalid WAIT delay"));
                yield break;
            }
            var end = Time.realtimeSinceStartup + delay;
            while (Time.realtimeSinceStartup < end)
                yield return null;
            _results.Add(StepResult.Pass(step, "waited"));
        }

        private IEnumerator WaitUntil(string step)
        {
            var timeout = 5f;
            var expr = step.Substring("WAIT_UNTIL ".Length).Trim();
            var timeoutIndex = expr.LastIndexOf(" TIMEOUT ", StringComparison.OrdinalIgnoreCase);
            if (timeoutIndex >= 0)
            {
                var rawTimeout = expr.Substring(timeoutIndex + " TIMEOUT ".Length).Trim();
                expr = expr.Substring(0, timeoutIndex).Trim();
                float.TryParse(rawTimeout, NumberStyles.Float, CultureInfo.InvariantCulture, out timeout);
            }

            var end = Time.realtimeSinceStartup + timeout;
            StepResult last = StepResult.Fail(step, "not evaluated");
            while (Time.realtimeSinceStartup < end)
            {
                last = EvaluateAssert("ASSERT " + expr);
                if (last.Passed)
                {
                    _results.Add(StepResult.Pass(step, "condition met"));
                    yield break;
                }
                yield return null;
            }
            _results.Add(StepResult.Fail(step, "timeout: " + last.Message));
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _consoleErrors.Add(condition);
        }

        private static List<string> ParseSteps(string script)
        {
            var steps = new List<string>();
            foreach (var rawLine in script.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                steps.Add(line.TrimEnd('\r'));
            }
            return steps;
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];
            return null;
        }

        private static bool HasArg(string[] args, string name)
        {
            foreach (var arg in args)
                if (arg == name)
                    return true;
            return false;
        }
    }
}
