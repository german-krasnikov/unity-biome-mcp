using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Playtest
{
    public sealed partial class PlayerPlaytestRunner
    {
        internal static StepResult EvaluateAssert(PlaytestStep step)
        {
            try
            {
                // Bool shorthand: a step can arrive with no operator (e.g.
                // ASSERT /path with no comparison) — treat it as an implicit
                // "== True" (or "== False" when negated), mirroring Core's own
                // bool-shorthand semantics instead of failing Compare().
                if (string.IsNullOrEmpty(step.Op))
                {
                    var negated = step.Query.StartsWith("!", StringComparison.Ordinal);
                    var query = negated ? step.Query.Substring(1) : step.Query;
                    var expected = negated ? "False" : "True";
                    var boolActual = ReadQuery(query);
                    return string.Equals(boolActual, expected, StringComparison.OrdinalIgnoreCase)
                        ? StepResult.Pass(step.RawLine, $"{boolActual} == {expected}")
                        : StepResult.Fail(step.RawLine, $"actual={boolActual}, expected {expected}");
                }

                var actual = ReadQuery(step.Query);
                return PlaytestParser.Compare(actual, step.Op, step.Value)
                    ? StepResult.Pass(step.RawLine, $"{actual} {step.Op} {step.Value}")
                    : StepResult.Fail(step.RawLine, $"actual={actual}, expected {step.Op} {step.Value}");
            }
            catch (Exception e)
            {
                return StepResult.Fail(step.RawLine, e.Message);
            }
        }

        private static StepResult ExecuteSnapshot(PlaytestStep step)
        {
            var queries = step.Queries ?? Array.Empty<string>();
            var parts = new string[queries.Length];
            for (var i = 0; i < queries.Length; i++)
            {
                var query = queries[i].Trim();
                try
                {
                    parts[i] = query + "=" + ReadQuery(query);
                }
                catch (Exception e)
                {
                    return StepResult.Fail(step.RawLine, e.Message);
                }
            }
            return StepResult.Pass(step.RawLine, string.Join(";", parts));
        }

        internal static StepResult ExecuteInvoke(PlaytestStep step)
        {
            try
            {
                var component = FindComponent(FindObject(step.Path), step.Component);
                var argTokens = PlaytestParser.SplitTokens(step.Args ?? "");
                var result = InvokeBestMatch(component, step.Method, argTokens, 0);
                if (result is string message && message.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                    return StepResult.Fail(step.RawLine, message);
                return StepResult.Pass(step.RawLine, FormatValue(result));
            }
            catch (Exception e)
            {
                return StepResult.Fail(step.RawLine, e.Message);
            }
        }

        internal static StepResult ExecuteSet(PlaytestStep step)
        {
            try
            {
                var component = FindComponent(FindObject(step.Path), step.Component);
                SetMember(component, step.Method, step.Args);
                return StepResult.Pass(step.RawLine, $"{step.Method}={step.Args}");
            }
            catch (Exception e)
            {
                return StepResult.Fail(step.RawLine, e.Message);
            }
        }

        private static string ReadQuery(string query)
        {
            var parts = query.Split('|');
            if (parts.Length == 2)
                return ReadGameObjectValue(FindObject(parts[0]), parts[1]);
            if (parts.Length == 3)
                return ReadComponentValue(FindObject(parts[0]), parts[1], parts[2]);
            throw new InvalidOperationException("query syntax: /Path|field or /Path|Component|field");
        }

        private static string ReadGameObjectValue(GameObject go, string field)
        {
            return field switch
            {
                "activeSelf" => go.activeSelf.ToString(),
                "activeInHierarchy" => go.activeInHierarchy.ToString(),
                "name" => go.name,
                "tag" => go.tag,
                "layer" => go.layer.ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException("unknown GameObject field: " + field),
            };
        }

        private static string ReadComponentValue(GameObject go, string componentName, string field)
        {
            var component = FindComponent(go, componentName);
            var type = component.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = type.GetProperty(field, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
                return FormatValue(property.GetValue(component));
            var fieldInfo = type.GetField(field, flags);
            if (fieldInfo != null)
                return FormatValue(fieldInfo.GetValue(component));
            var method = type.GetMethod(field, flags, null, Type.EmptyTypes, null);
            if (method != null)
                return FormatValue(method.Invoke(component, Array.Empty<object>()));
            throw new InvalidOperationException($"member not found: {componentName}.{field}");
        }

        private static GameObject FindObject(string path)
        {
            var names = path.Trim('/').Split('/');
            if (names.Length == 0 || string.IsNullOrEmpty(names[0]))
                throw new InvalidOperationException("empty object path");
            var root = GameObject.Find("/" + names[0]) ?? GameObject.Find(names[0]);
            if (root == null)
                throw new InvalidOperationException("object not found: " + path);
            var current = root.transform;
            for (var i = 1; i < names.Length; i++)
            {
                current = current.Find(names[i]);
                if (current == null)
                    throw new InvalidOperationException("object not found: " + path);
            }
            return current.gameObject;
        }

        private static Component FindComponent(GameObject go, string componentName)
        {
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                var type = component.GetType();
                if (type.Name == componentName || type.FullName == componentName)
                    return component;
            }
            throw new InvalidOperationException("component not found: " + componentName);
        }

        private static object InvokeBestMatch(Component component, string methodName, string[] tokens, int firstArg)
        {
            var type = component.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var method in type.GetMethods(flags))
            {
                var parameters = method.GetParameters();
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal) ||
                    parameters.Length != tokens.Length - firstArg)
                {
                    continue;
                }

                var args = ConvertArguments(tokens, firstArg, parameters);
                return method.Invoke(component, args);
            }
            throw new InvalidOperationException($"method not found: {type.Name}.{methodName}");
        }

        private static object[] ConvertArguments(string[] tokens, int firstArg, ParameterInfo[] parameters)
        {
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                args[i] = ConvertValue(tokens[firstArg + i], parameters[i].ParameterType);
            return args;
        }

        private static void SetMember(Component component, string memberName, string rawValue)
        {
            var type = component.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(component, ConvertValue(rawValue, property.PropertyType));
                return;
            }
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(component, ConvertValue(rawValue, field.FieldType));
                return;
            }
            throw new InvalidOperationException($"member not settable: {type.Name}.{memberName}");
        }

        private static object ConvertValue(string raw, Type targetType)
        {
            if (targetType == typeof(string))
                return raw;
            if (targetType == typeof(bool))
                return bool.Parse(raw);
            if (targetType == typeof(int))
                return int.Parse(raw, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(raw, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(raw, CultureInfo.InvariantCulture);
            if (targetType == typeof(Vector3))
            {
                var f = NumericParsing.ParseFloats(raw, 3);
                return new Vector3(f[0], f[1], f[2]);
            }
            if (targetType.IsEnum)
                return Enum.Parse(targetType, raw, true);
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => "",
                bool b => b.ToString(),
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                Vector3 v => $"{v.x.ToString(CultureInfo.InvariantCulture)},{v.y.ToString(CultureInfo.InvariantCulture)},{v.z.ToString(CultureInfo.InvariantCulture)}",
                _ => value.ToString(),
            };
        }
    }
}
