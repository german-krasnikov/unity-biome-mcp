using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Playtest
{
    public sealed partial class PlayerPlaytestRunner : MonoBehaviour
    {
        // The exact 9 StepTypes the fixtures under Assets/StreamingAssets/Playtests/
        // use (D11). Do NOT widen without a corresponding Execute() case — Move/
        // Section/Setup/Monitor etc. are deliberately out of scope (no fixture needs
        // them; unbounded scope creep).
        private static readonly HashSet<StepType> SupportedStepTypes = new()
        {
            StepType.Assert, StepType.AssertConsoleClean, StepType.Invoke, StepType.Log,
            StepType.Set, StepType.Snapshot, StepType.TimeScale, StepType.WaitUntil, StepType.Wait,
        };

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
            var script = File.ReadAllText(_scriptPath);
            var parsed = PlaytestParser.Parse(script, ResolveInclude);
            var steps = parsed.Steps;

            // Pre-scan gate: never a partial run against a script containing a step
            // type this Player runner cannot execute. Record every offender first,
            // then stop before any step actually runs.
            var hasUnsupported = false;
            foreach (var step in steps)
            {
                if (SupportedStepTypes.Contains(step.Type))
                    continue;
                _results.Add(StepResult.Fail(step.RawLine, $"unsupported step type in Player: {step.Type}"));
                hasUnsupported = true;
            }

            if (!hasUnsupported)
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    var before = _results.Count;
                    yield return Execute(step);
                    if (_results.Count == before)
                        _results.Add(StepResult.Fail(step.RawLine, "step produced no result"));
                }
            }

            var failed = 0;
            for (var i = 0; i < _results.Count; i++)
                if (!_results[i].Passed)
                    failed++;

            Time.timeScale = 1f;
            Application.logMessageReceived -= OnLog;
            var duration = (DateTime.UtcNow - started).TotalSeconds;
            WriteReceipts(failed, duration);
            if (_exitWhenDone)
                Application.Quit(failed == 0 ? 0 : 1);
        }

        private IEnumerator Execute(PlaytestStep step)
        {
            switch (step.Type)
            {
                case StepType.Log:
                    _results.Add(StepResult.Pass(step.RawLine, step.Message));
                    yield break;
                case StepType.Wait:
                    yield return Wait(step);
                    yield break;
                case StepType.TimeScale:
                    _results.Add(ExecuteTimescale(step));
                    yield break;
                case StepType.Invoke:
                    _results.Add(ExecuteInvoke(step));
                    yield break;
                case StepType.Set:
                    _results.Add(ExecuteSet(step));
                    yield break;
                case StepType.Snapshot:
                    _results.Add(ExecuteSnapshot(step));
                    yield break;
                case StepType.AssertConsoleClean:
                    _results.Add(_consoleErrors.Count == 0
                        ? StepResult.Pass(step.RawLine, "console clean")
                        : StepResult.Fail(step.RawLine, string.Join("\\n", _consoleErrors)));
                    yield break;
                case StepType.Assert:
                    _results.Add(EvaluateAssert(step));
                    yield break;
                case StepType.WaitUntil:
                    yield return WaitUntil(step);
                    yield break;
            }
        }

        private IEnumerator Wait(PlaytestStep step)
        {
            var end = Time.realtimeSinceStartup + step.Delay;
            while (Time.realtimeSinceStartup < end)
                yield return null;
            _results.Add(StepResult.Pass(step.RawLine, "waited"));
        }

        private static StepResult ExecuteTimescale(PlaytestStep step)
        {
            Time.timeScale = step.Delay;
            return StepResult.Pass(step.RawLine, $"timeScale={step.Delay.ToString(CultureInfo.InvariantCulture)}");
        }

        private IEnumerator WaitUntil(PlaytestStep step)
        {
            var end = Time.realtimeSinceStartup + step.Timeout;
            var last = StepResult.Fail(step.RawLine, "not evaluated");
            while (Time.realtimeSinceStartup < end)
            {
                last = EvaluateAssert(step);
                if (last.Passed)
                {
                    _results.Add(StepResult.Pass(step.RawLine, "condition met"));
                    yield break;
                }
                yield return null;
            }
            _results.Add(StepResult.Fail(step.RawLine, "timeout: " + last.Message));
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

        // D10: injectable IncludeResolver for the Player build — reads PlaytestDefs
        // from Application.streamingAssetsPath instead of the Editor-only
        // "Assets/PlaytestDefs/" path used by PlaytestParser's default resolver.
        // Plain System.IO.File works on every CI Player target (Linux/macOS/Windows);
        // Android/WebGL would need UnityWebRequest, but no CI target needs that yet.
        private static string ResolveInclude(string filename) =>
            File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "PlaytestDefs", filename));
    }
}
