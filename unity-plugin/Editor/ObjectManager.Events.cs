using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        public static string WireEvent(string path, string component, string eventField,
            string targetPath, string methodName, string argType, string argValue,
            string targetComponentType = null, string parameterTypes = null)
        {
            var (_, comp) = ResolveComponent(path, component);

            // Validate event field via SerializedProperty (more reliable than reflection)
            if (string.IsNullOrEmpty(eventField))
                throw new ArgumentException("event field name must not be empty");
            var soCheck = new SerializedObject(comp);
            var evtCheck = soCheck.FindProperty(eventField);
            if (evtCheck == null)
            {
                var alt = "m_" + char.ToUpper(eventField[0]) + eventField.Substring(1);
                evtCheck = soCheck.FindProperty(alt);
                if (evtCheck != null) eventField = alt;
            }
            if (evtCheck == null)
                throw new ArgumentException($"Field '{eventField}' not found on {component}");
            if (evtCheck.FindPropertyRelative("m_PersistentCalls.m_Calls") == null)
                throw new ArgumentException($"Field '{eventField}' is not a UnityEvent");

            // Find target — scene object or asset
            UnityEngine.Object target;
            var targetGo = ComponentSerializer.FindObject(targetPath);
            if (targetGo != null)
            {
                target = ResolveTargetComponent(targetGo, methodName,
                    targetComponentType, parameterTypes);
            }
            else
            {
                target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
                if (target == null)
                    throw new ArgumentException($"Target not found: {targetPath}");
            }

            Undo.RecordObject(comp, $"Wire {eventField}");

            if (!string.IsNullOrEmpty(argType) && argType != "void" && string.IsNullOrEmpty(argValue))
                throw new ArgumentException($"arg_value required when arg_type is '{argType}'");

            // Add persistent listener via SerializedObject for reliability
            var so = new SerializedObject(comp);
            var evtProp = so.FindProperty(eventField);
            var calls = evtProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            int idx = calls.arraySize;
            calls.InsertArrayElementAtIndex(idx);
            var call = calls.GetArrayElementAtIndex(idx);

            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                $"{target.GetType().AssemblyQualifiedName}";
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly

            // Determine mode from argType
            if (string.IsNullOrEmpty(argType) || argType == "void")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
            }
            else if (argType == "bool")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 6; // Bool
                call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue =
                    ValueParser.ParseBool(argValue);
            }
            else if (argType == "int")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 3; // Int
                call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue =
                    int.Parse(argValue);
            }
            else if (argType == "float")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 4; // Float
                call.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue =
                    float.Parse(argValue, CultureInfo.InvariantCulture);
            }
            else if (argType == "string")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 5; // String
                call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = argValue;
            }
            else if (argType == "object")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 2; // Object
                var argObj = ComponentSerializer.FindObject(argValue);
                UnityEngine.Object resolved = argObj;
                if (resolved == null)
                    resolved = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(argValue);
                if (resolved == null)
                    throw new ArgumentException($"Object arg not found: {argValue}");
                call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = resolved;
                call.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue =
                    "UnityEngine.Object, UnityEngine";
            }
            else
            {
                throw new ArgumentException($"Unsupported arg_type: {argType}. Use: void, bool, int, float, string, object");
            }

            so.ApplyModifiedProperties();

            // Include target type in return for verification (m_TargetAssemblyTypeName readback)
            var targetTypeName = target.GetType().FullName;
            var baseMsg = argType == "void" || string.IsNullOrEmpty(argType)
                ? $"Wired {eventField}[{idx}]: {targetPath}.{methodName}()"
                : $"Wired {eventField}[{idx}]: {targetPath}.{methodName}({argType}={argValue})";
            return $"{baseMsg} target={targetTypeName}";
        }

        // Resolves which component on targetGo owns methodName.
        // Fails with structured error when overloads are ambiguous.
        private static UnityEngine.Object ResolveTargetComponent(
            GameObject targetGo, string methodName,
            string targetComponentType, string parameterTypes)
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            Type[] paramTypes = ParseParameterTypes(parameterTypes);

            UnityEngine.Object resolved = targetGo; // fallback: GO itself (SetActive etc.)
            foreach (var comp2 in targetGo.GetComponents<Component>())
            {
                if (comp2 == null) continue;

                // Narrow to requested component type when specified
                if (!string.IsNullOrEmpty(targetComponentType))
                {
                    var ct = comp2.GetType();
                    if (ct.Name != targetComponentType && ct.FullName != targetComponentType)
                        continue;
                }

                var overloads = comp2.GetType()
                    .GetMethods(flags)
                    .Where(m => m.Name == methodName)
                    .ToArray();

                if (overloads.Length == 0) continue;

                if (overloads.Length == 1)
                {
                    resolved = comp2;
                    break;
                }

                // Multiple overloads — try to disambiguate with parameterTypes
                if (paramTypes != null)
                {
                    var method = comp2.GetType().GetMethod(methodName, flags, null, paramTypes, null);
                    if (method != null)
                    {
                        resolved = comp2;
                        break;
                    }
                }

                // Cannot disambiguate — report all candidate signatures
                var sigs = overloads.Select(m =>
                    $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                throw new ArgumentException(
                    $"Ambiguous method '{methodName}' on {comp2.GetType().Name}: " +
                    $"[{string.Join(", ", sigs)}]. " +
                    "Specify target_component_type and parameter_types to disambiguate.");
            }
            return resolved;
        }

        // Parses "string" or "int,float" into Type[]. Returns null when empty.
        private static Type[] ParseParameterTypes(string parameterTypes)
        {
            if (string.IsNullOrEmpty(parameterTypes)) return null;

            return parameterTypes.Split(',')
                .Select(name => name.Trim())
                .Select(name => ResolveSimpleType(name))
                .ToArray();
        }

        private static Type ResolveSimpleType(string name) => name switch
        {
            "string" => typeof(string),
            "int"    => typeof(int),
            "float"  => typeof(float),
            "double" => typeof(double),
            "bool"   => typeof(bool),
            "long"   => typeof(long),
            "short"  => typeof(short),
            "byte"   => typeof(byte),
            "uint"   => typeof(uint),
            _        => Type.GetType(name)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
                ?? throw new ArgumentException($"Cannot resolve parameter type: '{name}'")
        };

        public static string UnwireEvent(string path, string component, string eventField, string index)
        {
            var (_, comp) = ResolveComponent(path, component);

            var so = new SerializedObject(comp);
            if (string.IsNullOrEmpty(eventField))
                throw new ArgumentException("event field name must not be empty");
            var evtProp = so.FindProperty(eventField);
            if (evtProp == null)
            {
                var alt = "m_" + char.ToUpper(eventField[0]) + eventField.Substring(1);
                if (so.FindProperty(alt) != null) eventField = alt;
                evtProp = so.FindProperty(eventField);
            }
            if (evtProp == null)
                throw new ArgumentException($"Field '{eventField}' not found on {component}");
            var calls = evtProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null)
                throw new ArgumentException($"Field '{eventField}' is not a UnityEvent");

            Undo.RecordObject(comp, $"Unwire {eventField}");

            if (string.IsNullOrEmpty(index))
            {
                int count = calls.arraySize;
                calls.arraySize = 0;
                so.ApplyModifiedProperties();
                return $"Cleared {eventField} ({count} removed)";
            }

            if (!int.TryParse(index, out int idx))
                throw new ArgumentException($"Index must be an integer, got: '{index}'");
            if (idx < 0 || idx >= calls.arraySize)
                throw new ArgumentException($"Index {idx} out of range (0..{calls.arraySize - 1})");
            calls.DeleteArrayElementAtIndex(idx);
            so.ApplyModifiedProperties();
            return $"Removed {eventField}[{idx}], {calls.arraySize} remaining";
        }
    }
}
