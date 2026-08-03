using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class ValidateReferencesHelper
    {
        public static string Validate(string path, int depth, bool ignoreOptional, bool verbose = false)
        {
            // ignoreOptional reserved for future use
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var sb = new StringBuilder();
            int ok = 0, errors = 0, missing = 0;
            ValidateRecursive(go, depth, sb, ref ok, ref errors, ref missing, verbose);

            if (sb.Length > 0) sb.AppendLine();
            sb.Append($"{errors} ERROR, {ok} OK");
            if (missing > 0) sb.Append($", {missing} MISSING");
            return sb.ToString().TrimEnd('\n');
        }

        private static void ValidateRecursive(GameObject go, int depth, StringBuilder sb,
            ref int ok, ref int errors, ref int missing, bool verbose)
        {
            ValidateObject(go, sb, ref ok, ref errors, ref missing, verbose);
            if (depth <= 1) return;
            foreach (Transform child in go.transform)
                ValidateRecursive(child.gameObject, depth - 1, sb, ref ok, ref errors, ref missing, verbose);
        }

        private static void ValidateObject(GameObject go, StringBuilder sb,
            ref int ok, ref int errors, ref int missing, bool verbose)
        {
            var goPath = ComponentSerializer.GetPath(go);
            if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.MissingAsset)
            {
                sb.Append("[ERROR] ").Append(goPath).AppendLine(" — missing prefab asset");
                errors++;
                return;
            }
            int localOk = ok, localErrors = errors, localMissing = missing;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    sb.Append("[ERROR] ").Append(goPath).AppendLine(" — missing script (null component)");
                    localErrors++;
                    continue;
                }
                if (comp is Transform) continue;

                var so = new SerializedObject(comp);
                var compType = comp.GetType().Name;
                ReferenceHelper.WalkObjectRefs(so, (p, label) =>
                    CheckRef(p, label, goPath, compType, sb, ref localOk, ref localErrors, ref localMissing, verbose));
            }
            ok = localOk; errors = localErrors; missing = localMissing;
        }

        private static void CheckRef(SerializedProperty prop, string propName, string goPath,
            string compType, StringBuilder sb, ref int ok, ref int errors, ref int missing, bool verbose)
        {
            var hasObjectId = TransientObjectId.HasSerializedReference(prop);
            var value = prop.objectReferenceValue;

            if (hasObjectId && value == null)
            {
                sb.Append("[MISSING] ").Append(goPath)
                  .Append(" [").Append(compType).Append("].").AppendLine(propName);
                missing++;
            }
            else if (value != null)
            {
                if (verbose)
                {
                    sb.Append("[OK] ").Append(goPath)
                      .Append(" [").Append(compType).Append("].").Append(propName)
                      .Append(": ").AppendLine(value.name);
                }
                ok++;
            }
        }
    }
}
