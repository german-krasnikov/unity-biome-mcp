using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace UnityMCP.Playtest
{
    public sealed partial class PlayerPlaytestRunner
    {
        private static StepResult EvaluateAssert(string step)
        {
            var expression = step.Substring("ASSERT ".Length).Trim();
            foreach (var op in new[] { " contains ", " == ", " != ", " >= ", " <= ", " > ", " < " })
            {
                var index = expression.IndexOf(op, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    continue;
                var query = expression.Substring(0, index).Trim();
                var expected = expression.Substring(index + op.Length).Trim();
                try
                {
                    var actual = ReadQuery(query);
                    return Compare(actual, op.Trim(), expected)
                        ? StepResult.Pass(step, $"{actual} {op.Trim()} {expected}")
                        : StepResult.Fail(step, $"actual={actual}, expected {op.Trim()} {expected}");
                }
                catch (Exception e)
                {
                    return StepResult.Fail(step, e.Message);
                }
            }
            return StepResult.Fail(step, "missing comparison operator");
        }

        private static StepResult ExecuteSnapshot(string step)
        {
            var queries = step.Substring("SNAPSHOT ".Length).Split(',');
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
                    return StepResult.Fail(step, e.Message);
                }
            }
            return StepResult.Pass(step, string.Join(";", parts));
        }

        private static StepResult ExecuteInvoke(string step)
        {
            var tokens = SplitWords(step);
            if (tokens.Length < 4)
                return StepResult.Fail(step, "INVOKE syntax: INVOKE /path Component Method [args]");

            try
            {
                var component = FindComponent(FindObject(tokens[1]), tokens[2]);
                var result = InvokeBestMatch(component, tokens[3], tokens, 4);
                if (result is string message && message.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                    return StepResult.Fail(step, message);
                return StepResult.Pass(step, FormatValue(result));
            }
            catch (Exception e)
            {
                return StepResult.Fail(step, e.Message);
            }
        }

        private static StepResult ExecuteSet(string step)
        {
            var tokens = SplitWords(step);
            if (tokens.Length < 5)
                return StepResult.Fail(step, "SET syntax: SET /path Component field value");

            try
            {
                var component = FindComponent(FindObject(tokens[1]), tokens[2]);
                var value = string.Join(" ", tokens, 4, tokens.Length - 4);
                SetMember(component, tokens[3], value);
                return StepResult.Pass(step, $"{tokens[3]}={value}");
            }
            catch (Exception e)
            {
                return StepResult.Fail(step, e.Message);
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
            if (targetType.IsEnum)
                return Enum.Parse(targetType, raw, true);
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }

        private static string[] SplitWords(string step)
        {
            return step.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool Compare(string actual, string op, string expected)
        {
            if (float.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
                float.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var e))
            {
                return op switch
                {
                    "==" => Math.Abs(a - e) < 0.001f,
                    "!=" => Math.Abs(a - e) >= 0.001f,
                    ">" => a > e,
                    ">=" => a >= e,
                    "<" => a < e,
                    "<=" => a <= e,
                    _ => false,
                };
            }
            return op switch
            {
                "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "contains" => actual?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0,
                _ => false,
            };
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
