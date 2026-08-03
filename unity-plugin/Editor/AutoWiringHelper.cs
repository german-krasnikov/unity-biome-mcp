using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal readonly struct WireCandidate
    {
        public readonly Component          Component;
        public readonly string             FieldName;
        public readonly string             FieldPath;
        public readonly UnityEngine.Object Target;
        public readonly string             TargetPath;
        public readonly string             Reason;

        public WireCandidate(Component comp, string fieldName, string fieldPath,
                             UnityEngine.Object target, string targetPath, string reason)
        {
            Component  = comp;
            FieldName  = fieldName;
            FieldPath  = fieldPath;
            Target     = target;
            TargetPath = targetPath;
            Reason     = reason;
        }
    }

    internal static class AutoWiringHelper
    {
        private enum MatchState { NoMatch, Ambiguous, Wired }

        /// <summary>Scan all null ObjectReference fields and find matches in scene scope.</summary>
        public static (List<WireCandidate> wired, List<string> skipped) Scan(GameObject go)
        {
            var wired   = new List<WireCandidate>();
            var skipped = new List<string>();
            var scope   = BuildScope(go);

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                ScanComponent(comp, go, scope, wired, skipped);
            }
            return (wired, skipped);
        }

        /// <summary>Apply wired candidates: record undo + assign via SerializedObject.</summary>
        public static void Apply(List<WireCandidate> candidates)
        {
            foreach (var c in candidates)
            {
                Undo.RecordObject(c.Component, $"AutoWire: {c.FieldName}");
                var so   = new SerializedObject(c.Component);
                so.UpdateIfRequiredOrScript();
                var prop = so.FindProperty(c.FieldPath);
                if (prop == null) continue;
                prop.objectReferenceValue = c.Target;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>Format scan result as plain text. dryRun prepends [DRY] prefix per line.</summary>
        public static string Format(List<WireCandidate> wired, List<string> skipped, bool dryRun)
        {
            var sb     = new StringBuilder();
            var prefix = dryRun ? "[DRY] " : "";

            foreach (var c in wired)
            {
                var goPath = ComponentSerializer.GetPath(c.Component.gameObject);
                sb.AppendLine($"{prefix}{goPath}:{c.Component.GetType().Name}.{c.FieldName} → {c.TargetPath}  [{c.Reason}]");
            }

            foreach (var s in skipped)
                sb.AppendLine(s);

            sb.Append($"Wired: {wired.Count} | Ambiguous: {skipped.Count} | No match: 0");
            return sb.ToString();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void ScanComponent(Component comp, GameObject go, List<Transform> scope,
            List<WireCandidate> wired, List<string> skipped)
        {
            var so    = new SerializedObject(comp);
            so.UpdateIfRequiredOrScript();
            var prop  = so.GetIterator();
            bool enter = true;

            while (prop.NextVisible(enter))
            {
                enter = false;
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.objectReferenceValue != null) continue;

                var rawName   = prop.name;
                var fieldName = rawName.StartsWith("m_", StringComparison.Ordinal)
                    ? rawName.Substring(2) : rawName;

                Type fieldType = null;
                try { fieldType = ValueParser.GetSerializedFieldType(prop); }
                catch { /* exotic type: skip type-only bucket */ }

                var (state, target, reason) = Resolve(fieldName, fieldType, scope);

                if (state == MatchState.Wired)
                {
                    var targetGo   = (target is Component c) ? c.gameObject : target as GameObject;
                    var targetPath = targetGo != null
                        ? ComponentSerializer.GetPath(targetGo)
                        : target.name;
                    wired.Add(new WireCandidate(comp, fieldName, prop.propertyPath, target, targetPath, reason));
                }
                else if (state == MatchState.Ambiguous)
                {
                    var goPath = ComponentSerializer.GetPath(go);
                    skipped.Add($"{goPath}:{comp.GetType().Name}.{fieldName} AMBIGUOUS");
                }
                // NoMatch → silently skip
            }
        }

        private static List<Transform> BuildScope(GameObject go)
        {
            var seen  = new HashSet<Transform>();
            var scope = new List<Transform>();

            void Add(Transform t)
            {
                if (t == null || !seen.Add(t)) return;
                scope.Add(t);
            }

            // 1. Children (includes self)
            foreach (var t in go.GetComponentsInChildren<Transform>(includeInactive: true))
                Add(t);

            // 2. Siblings (parent's children; self already added)
            if (go.transform.parent != null)
                foreach (var t in go.transform.parent.GetComponentsInChildren<Transform>(includeInactive: true))
                    Add(t);

            // 3. Scene roots
            foreach (var (_, roots) in SceneContext.Current.Scenes)
                foreach (var root in roots)
                    foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                        Add(t);

            return scope;
        }

        private static (MatchState state, UnityEngine.Object target, string reason) Resolve(
            string fieldName, Type fieldType, List<Transform> scope)
        {
            var exact    = new List<Transform>();
            var contains = new List<Transform>();
            var typeOnly = new List<UnityEngine.Object>();

            foreach (var t in scope)
            {
                var name       = t.name;
                bool isExact   = name.Equals(fieldName, StringComparison.OrdinalIgnoreCase);
                bool isContain = !isExact && name.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isExact)   exact.Add(t);
                if (isContain) contains.Add(t);

                if (!isExact && !isContain && fieldType != null)
                {
                    var comp = t.GetComponent(fieldType);
                    if (comp != null) typeOnly.Add(comp);
                }
            }

            if (exact.Count == 1)    return (MatchState.Wired, ResolveTarget(exact[0], fieldType), "exact");
            if (exact.Count > 1)     return (MatchState.Ambiguous, null, null);
            if (contains.Count == 1) return (MatchState.Wired, ResolveTarget(contains[0], fieldType), "contains");
            if (contains.Count > 1)  return (MatchState.Ambiguous, null, null);
            if (typeOnly.Count == 1) return (MatchState.Wired, typeOnly[0], "type-only");
            if (typeOnly.Count > 1)  return (MatchState.Ambiguous, null, null);

            return (MatchState.NoMatch, null, null);
        }

        // If fieldType is a Component subclass, return that component from t's GO; else return the GO.
        private static UnityEngine.Object ResolveTarget(Transform t, Type fieldType)
        {
            if (fieldType != null && typeof(Component).IsAssignableFrom(fieldType))
            {
                var comp = t.GetComponent(fieldType);
                if (comp != null) return comp;
            }
            return t.gameObject;
        }
    }
}
