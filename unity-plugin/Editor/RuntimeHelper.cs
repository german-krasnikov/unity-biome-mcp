using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class RuntimeHelper
    {
        private static readonly List<TaskCompletionSource<string>> _activeTcs = new();

        // key: (declaring Type, stripped method name); cleared on domain reload
        private static readonly Dictionary<(Type, string), MethodInfo> _methodCache = new();

        [InitializeOnLoadMethod]
        static void HookReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                lock (_activeTcs)
                {
                    foreach (var t in _activeTcs)
                        t.TrySetResult("err:domain reload — operation aborted");
                    _activeTcs.Clear();
                }
                _methodCache.Clear();
            };
        }

        public static string InvokeMethod(string path, string componentType, string methodName, string args)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var comp = FindComponent(go, componentType);
            if (comp == null)
                throw new ArgumentException(ErrorHelper.ComponentNotFound(componentType, go));

            var methods = comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var method = methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                var names = string.Join(", ", methods.Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})"));
                throw new ArgumentException($"Method '{methodName}' not found. Available: {names}");
            }

            var parameters = method.GetParameters();
            var parsed = ParseArgs(args, parameters);

            try
            {
                var result = method.Invoke(comp, parsed);
                if (result == null) return "void";
                if (result is float rf) return rf.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
                if (result is double rd) return rd.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
                return result.ToString();
            }
            catch (TargetInvocationException e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(e.InnerException ?? e).Throw();
                throw; // unreachable — satisfies compiler
            }
        }

        public static string SetRuntimeProperty(string path, string componentType, string field, string value)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var comp = FindComponent(go, componentType);
            if (comp == null)
                throw new ArgumentException(ErrorHelper.ComponentNotFound(componentType, go));

            var type = comp.GetType();

            var prop = type.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                prop.SetValue(comp, ConvertValue(value, prop.PropertyType));
                return $"{field}={value}";
            }

            var fieldInfo = type.GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(comp, ConvertValue(value, fieldInfo.FieldType));
                return $"{field}={value}";
            }

            throw new ArgumentException($"Field/property '{field}' not found on {componentType}");
        }

        public static void WaitUntil(string path, string componentType, string field,
            string expectedValue, float timeout, bool negate, TaskCompletionSource<string> tcs,
            bool abortOnFail = false)
        {
            lock (_activeTcs) _activeTcs.Add(tcs);
            float startTime = Time.realtimeSinceStartup;
            float lastCheck = -1f;

            void Complete(string result)
            {
                EditorApplication.update -= Tick;
                lock (_activeTcs) _activeTcs.Remove(tcs);
                tcs.TrySetResult(result);
            }

            void Tick()
            {
                if (!EditorApplication.isPlaying)
                {
                    Complete("wait_until: Play Mode stopped before condition met.");
                    return;
                }

                float now = Time.realtimeSinceStartup;
                if (now - lastCheck < 0.1f) return;
                lastCheck = now;

                if (now - startTime >= timeout)
                {
                    if (abortOnFail) EditorApplication.isPlaying = false;
                    Complete($"wait_until: timeout after {timeout}s — {field} never matched '{expectedValue}'");
                    return;
                }

                try
                {
                    var go = ComponentSerializer.FindObject(path);
                    if (go == null)
                    {
                        Complete($"wait_until: object '{path}' destroyed during wait");
                        return;
                    }
                    var comp = FindComponent(go, componentType);
                    if (comp == null)
                    {
                        Complete($"wait_until: component '{componentType}' lost during wait");
                        return;
                    }

                    var current = ReadField(comp, field);
                    bool matches = string.Equals(current, expectedValue, StringComparison.OrdinalIgnoreCase);
                    if (negate) matches = !matches;

                    if (matches)
                    {
                        var condition = negate ? $"{field}!={expectedValue}" : $"{field}={expectedValue}";
                        Complete($"{condition} after {now - startTime:F1}s");
                    }
                }
                catch (Exception e)
                {
                    Complete($"wait_until error: {e.Message}");
                }
            }

            EditorApplication.update += Tick;
        }

        public static void MoveTo(string path, string args, float timeout, TaskCompletionSource<string> tcs,
            PlaytestConfig config = null)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) { tcs.TrySetResult($"Error: '{path}' not found"); return; }

            // Load config once — avoids 2-3 redundant AssetDatabase.FindAssets calls per move
            if (config == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig");
                if (guids.Length > 0)
                    config = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            var moveComp = FindMoveComponent(go, config);
            if (moveComp == null) { tcs.TrySetResult($"Error: no movement component found on '{path}'"); return; }
            var comp = moveComp;

            var parts = args.Split(',');
            if (parts.Length != 3) { tcs.TrySetResult($"Error: expected 3 floats (x,y,z), got {parts.Length}"); return; }

            var floats = ValueParser.ParseFloats(args, 3);
            var target = new Vector3(floats[0], floats[1], floats[2]);

            var moveName = GetConfiguredMoveMethod(config);
            if (string.IsNullOrEmpty(moveName))
                moveName = comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Vector3)
                        && m.GetParameters()[1].ParameterType == typeof(Action<bool>))?.Name;
            if (string.IsNullOrEmpty(moveName)) { tcs.TrySetResult("Error: no move method (Vector3, Action<bool>) found — set moveMethod in PlaytestConfig"); return; }
            var method = comp.GetType().GetMethod(moveName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null) { tcs.TrySetResult($"Error: method '{moveName}' not found"); return; }

            lock (_activeTcs) _activeTcs.Add(tcs);
            float startTime = Time.realtimeSinceStartup;
            bool completed = false;

            Action<bool> callback = success =>
            {
                completed = true;
                float elapsed = Time.realtimeSinceStartup - startTime;
                EditorApplication.delayCall += () =>
                {
                    lock (_activeTcs) _activeTcs.Remove(tcs);
                    tcs.TrySetResult($"MoveTo {(success ? "arrived" : "blocked")} at " +
                        $"({target.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"{target.y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"{target.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}) after " +
                        elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s");
                };
            };

            try { method.Invoke(comp, new object[] { target, callback }); }
            catch (Exception e)
            {
                lock (_activeTcs) _activeTcs.Remove(tcs);
                tcs.TrySetResult($"Error: {e.InnerException?.Message ?? e.Message}");
                return;
            }

            // Timeout fallback
            void TimeoutCheck()
            {
                if (completed || !EditorApplication.isPlaying) { EditorApplication.update -= TimeoutCheck; return; }
                if (Time.realtimeSinceStartup - startTime >= timeout)
                {
                    EditorApplication.update -= TimeoutCheck;
                    lock (_activeTcs) _activeTcs.Remove(tcs);
                    tcs.TrySetResult($"MoveTo timeout after {timeout}s — still moving");
                }
            }
            EditorApplication.update += TimeoutCheck;
        }

        public static void TestStep(string path, string position, string checksBefore, string checksAfter,
            float waitAfter, float timeout, TaskCompletionSource<string> tcs)
        {
            lock (_activeTcs) _activeTcs.Add(tcs);

            // Phase 1: take BEFORE snapshot (synchronous, main thread)
            string beforeSnapshot = string.IsNullOrEmpty(checksBefore) ? "" : GameStateHelper.Snapshot(checksBefore);

            // Phase 2: start movement
            var moveTcs = new TaskCompletionSource<string>();
            MoveTo(path, position, timeout, moveTcs);

            float settleStart = -1f;
            string moveResult = null;
            float startTime = Time.realtimeSinceStartup;

            void Complete(string result)
            {
                EditorApplication.update -= Tick;
                lock (_activeTcs) _activeTcs.Remove(tcs);
                tcs.TrySetResult(result);
            }

            void Tick()
            {
                if (!EditorApplication.isPlaying)
                {
                    Complete(BuildTestStepReport(beforeSnapshot, "stopped", "", "Play Mode stopped"));
                    return;
                }

                if (Time.realtimeSinceStartup - startTime >= timeout + waitAfter + 2f)
                {
                    Complete(BuildTestStepReport(beforeSnapshot, moveResult ?? "timeout", "", "timeout"));
                    return;
                }

                // Phase 3: wait for move completion
                if (moveResult == null)
                {
                    if (!moveTcs.Task.IsCompleted) return;
                    moveResult = moveTcs.Task.Result;
                    settleStart = Time.realtimeSinceStartup;
                    return;
                }

                // Phase 4: settle wait
                if (Time.realtimeSinceStartup - settleStart < waitAfter) return;

                // Phase 5: AFTER snapshot + console check
                string afterSnapshot = string.IsNullOrEmpty(checksAfter) ? "" : GameStateHelper.Snapshot(checksAfter);
                string console = ConsoleCapture.GetLogs(10, "error,warning");
                Complete(BuildTestStepReport(beforeSnapshot, moveResult, afterSnapshot, console));
            }

            EditorApplication.update += Tick;
        }

        private static string BuildTestStepReport(string before, string move, string after, string console)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(before)) sb.AppendLine($"BEFORE:\n{before}");
            sb.AppendLine($"MOVE: {move}");
            if (!string.IsNullOrEmpty(after)) sb.AppendLine($"AFTER:\n{after}");
            sb.AppendLine($"CONSOLE: {(string.IsNullOrEmpty(console) ? "ok" : console)}");
            return sb.ToString().TrimEnd();
        }

        internal static Component FindComponentInternal(GameObject go, string typeName) => FindComponent(go, typeName);
        internal static string ReadFieldInternal(Component comp, string fieldName) => ReadField(comp, fieldName);

        private static Component FindMoveComponent(GameObject go, PlaytestConfig config)
        {
            if (config != null && !string.IsNullOrEmpty(config.moveComponent))
                return FindComponent(go, config.moveComponent);
            // Fallback: find any component with a method matching (Vector3, Action<bool>) signature
            return go.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(m => m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Vector3)
                        && m.GetParameters()[1].ParameterType == typeof(Action<bool>)));
        }

        private static string GetConfiguredMoveMethod(PlaytestConfig config)
        {
            return config != null && !string.IsNullOrEmpty(config.moveMethod) ? config.moveMethod : null;
        }

        private static Component FindComponent(GameObject go, string typeName)
            => ComponentSerializer.FindComponent(go, typeName);

        private static string ReadField(Component comp, string fieldName)
        {
            object current = comp;
            foreach (var part in fieldName.Split('.'))
            {
                if (current == null) throw new ArgumentException($"Null at '{part}' in path '{fieldName}'");
                var t = current.GetType();
                int lparen = part.IndexOf('(');
                if (lparen >= 0 && part.EndsWith(")"))
                {
                    var mName = part.Substring(0, lparen);
                    var argsStr = part.Substring(lparen + 1, part.Length - lparen - 2);
                    int argCount = string.IsNullOrEmpty(argsStr) ? 0 : argsStr.Split(',').Length;
                    var cacheKey = (t, mName + ":" + argCount);
                    if (!_methodCache.TryGetValue(cacheKey, out var mi))
                    {
                        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                        mi = argCount == 0
                            ? t.GetMethod(mName, flags, null, Type.EmptyTypes, null)
                            : t.GetMethods(flags).FirstOrDefault(m => m.Name == mName && m.GetParameters().Length == argCount);
                        _methodCache[cacheKey] = mi; // cache null too — avoids repeat reflection
                    }
                    if (mi == null) throw new ArgumentException($"Method '{mName}({argsStr})' not found on {t.Name}");
                    current = argCount == 0
                        ? mi.Invoke(current, null)
                        : mi.Invoke(current, ParseArgs(argsStr, mi.GetParameters()));
                    continue;
                }
                var prop = t.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null) { current = prop.GetValue(current); continue; }
                var field = t.GetField(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) { current = field.GetValue(current); continue; }
                throw new ArgumentException($"Field/property '{part}' not found on {t.Name}");
            }
            if (current is System.Collections.IList list)
            {
                var items = new StringBuilder("[");
                int max = Math.Min(list.Count, 10);
                for (int i = 0; i < max; i++)
                {
                    if (i > 0) items.Append(", ");
                    items.Append(list[i]?.ToString() ?? "null");
                }
                if (list.Count > 10) items.Append($", ...+{list.Count - 10}");
                items.Append("]");
                return items.ToString();
            }
            if (current is float f) return f.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
            if (current is double d) return d.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
            return current?.ToString();
        }

        private static object[] ParseArgs(string args, ParameterInfo[] parameters)
        {
            if (parameters.Length == 0) return new object[0];
            if (string.IsNullOrEmpty(args))
            {
                if (parameters.Length > 0)
                    throw new ArgumentException($"Expected {parameters.Length} args ({string.Join(", ", parameters.Select(p => p.ParameterType.Name))}), got 0");
                return new object[0];
            }

            var parts = args.Split(',');

            // Smart grouping: Vector3 consumes 3 parts, Vector2 consumes 2, others consume 1
            var result = new object[parameters.Length];
            int partIdx = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                var pType = parameters[i].ParameterType;
                int consume = pType == typeof(Vector3) ? 3 : pType == typeof(Vector2) ? 2 : 1;
                if (partIdx + consume > parts.Length)
                    throw new ArgumentException($"Not enough args for param {i} ({pType.Name}), need {consume} parts from index {partIdx}, have {parts.Length}");
                try
                {
                    var chunk = string.Join(",", parts.Skip(partIdx).Take(consume).Select(s => s.Trim()));
                    result[i] = ConvertValue(chunk, pType);
                }
                catch (Exception e) { throw new ArgumentException($"Arg {i} → {pType.Name}: {e.Message}"); }
                partIdx += consume;
            }
            if (partIdx != parts.Length)
                throw new ArgumentException($"Too many args: expected {partIdx} comma-separated values, got {parts.Length}");
            return result;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(bool))
                return ValueParser.ParseBool(value);
            if (targetType == typeof(Vector3))
            {
                var f = ValueParser.ParseFloats(value, 3);
                return new Vector3(f[0], f[1], f[2]);
            }
            if (targetType == typeof(Vector2))
            {
                var f = ValueParser.ParseFloats(value, 2);
                return new Vector2(f[0], f[1]);
            }
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, ignoreCase: true);
            return Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
