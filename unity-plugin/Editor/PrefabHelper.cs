using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Execute(action, argsJson) is generated into Generated/ToolDispatch.g.cs from the DISPATCH/ACTION
    // lines of dsl/tools.biome's prefab TOOL block (dsl-bottlenecks-2026-11 S3).
    internal static partial class PrefabHelper
    {
        private static string Save(string args)
        {
            var path = PrefabArgs.Path(args)
                ?? throw new ArgumentException("path is required");
            var assetPath = PrefabArgs.AssetPath(args)
                ?? throw new ArgumentException("asset_path is required");
            var mode = PrefabArgs.Mode(args) ?? "overwrite";

            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            if (mode == "new" && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
                throw new InvalidOperationException($"Prefab already exists: {assetPath}");

            AssetHelper.EnsureDirectory(assetPath);
            UndoGroupStack.StageAsset(assetPath);
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            var srcPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? "";
            return $"ok: {assetPath}\nsource_prefab: {srcPath}\nis_variant: false\ninstance_path: {path}";
        }

        private static string CreateVariant(string args)
        {
            var basePath = PrefabArgs.BasePath(args)
                ?? throw new ArgumentException("base_path is required");
            var variantPath = PrefabArgs.VariantPath(args)
                ?? throw new ArgumentException("variant_path is required");

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath)
                ?? throw new InvalidOperationException($"Prefab not found: {basePath}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                AssetHelper.EnsureDirectory(variantPath);
                UndoGroupStack.StageAsset(variantPath);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                AssetDatabase.SaveAssets();
                return $"ok: {variantPath}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static string Instantiate(string args)
        {
            var assetPath = PrefabArgs.AssetPath(args)
                ?? throw new ArgumentException("asset_path is required");
            var parentPath = PrefabArgs.Parent(args);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)
                ?? throw new InvalidOperationException($"Prefab not found: {assetPath}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = ComponentSerializer.FindObject(parentPath)
                    ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(parentPath));
                Undo.SetTransformParent(instance.transform, parent.transform, "Reparent Prefab Instance");
            }

            var path = ComponentSerializer.GetPath(instance);
            return $"ok: {path}\nasset: {assetPath}";
        }

        private static string Apply(string args)
        {
            var go = RequirePrefabInstance(args);
            Undo.RegisterFullObjectHierarchyUndo(go, "Apply Prefab");
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            return $"Applied: {go.name}";
        }

        private static string Revert(string args)
        {
            var scope = PrefabArgs.Scope(args) ?? "object";
            var go = RequirePrefabInstance(args);
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(go) ?? go;
            if (scope == "children")
            {
                foreach (Transform child in root.transform)
                    PrefabUtility.RevertObjectOverride(child, InteractionMode.AutomatedAction);
                return $"Reverted children of: {root.name}";
            }
            PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
            return $"Reverted: {root.name}";
        }

        private static string GetOverrides(string args)
        {
            var path = PrefabArgs.Path(args)
                ?? throw new ArgumentException("path is required");
            var format = PrefabArgs.Format(args) ?? "text";
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return "no overrides (not a prefab instance)";

            var mods    = PrefabUtility.GetPropertyModifications(go);
            var added   = PrefabUtility.GetAddedComponents(go);
            var removed = PrefabUtility.GetRemovedComponents(go);

            if (format == "structured")
            {
                var srcPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? "";
                return $"source_prefab: {srcPrefab}\ninstance_path: {path}\nchanged_properties: {(mods?.Length ?? 0)}\nadded_components: {added.Count}\nremoved_components: {removed.Count}";
            }

            var sb = new StringBuilder();
            if (mods != null)
                foreach (var m in mods)
                    sb.AppendLine($"  {m.propertyPath}: {m.value}");

            foreach (var a in added)
                sb.AppendLine($"  +component: {a.instanceComponent.GetType().Name}");

            foreach (var r in removed)
                sb.AppendLine($"  -component: {r.assetComponent.GetType().Name}");

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "no overrides";
        }

        private static string Unpack(string args)
        {
            var path = PrefabArgs.Path(args)
                ?? throw new ArgumentException("path is required");
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var recursive = PrefabArgs.Recursive(args);
            var mode = recursive ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot;
            Undo.RegisterFullObjectHierarchyUndo(go, "Unpack Prefab");
            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(go);
            return $"Unpacked: {go.name}";
        }

        private static GameObject RequirePrefabInstance(string args)
        {
            var path = PrefabArgs.Path(args)
                ?? throw new ArgumentException("path is required");
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                throw new InvalidOperationException($"{go.name} is not a prefab instance");
            return go;
        }

        private static string Edit(string args)
        {
            var assetPath  = PrefabArgs.AssetPath(args)
                ?? throw new ArgumentException("asset_path is required (e.g. Assets/Prefabs/Foo.prefab)");
            var childPath  = PrefabArgs.ChildPath(args)
                ?? PrefabArgs.Path(args);
            var component  = PrefabArgs.Component(args);
            var prop       = PrefabArgs.Prop(args);
            var value      = PrefabArgs.Value(args);
            var addComp    = PrefabArgs.AddComponent(args);
            var removeComp = PrefabArgs.RemoveComponent(args);

            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            if (contents == null)
                throw new ArgumentException($"Prefab not found: {assetPath}");
            try
            {
                var target = contents;
                if (!string.IsNullOrWhiteSpace(childPath))
                {
                    childPath = childPath.TrimStart('/');
                    var childTransform = contents.transform.Find(childPath);
                    if (childTransform == null)
                        throw new ArgumentException($"child not found: {childPath}");
                    target = childTransform.gameObject;
                }

                if (!string.IsNullOrEmpty(addComp))
                {
                    var t = ObjectManager.FindType(addComp)
                        ?? throw new ArgumentException($"Component type not found: {addComp}");
                    if (target.GetComponent(t) == null)
                        target.AddComponent(t);
                }
                if (!string.IsNullOrEmpty(removeComp))
                {
                    var t = ObjectManager.FindType(removeComp);
                    var c = t != null ? target.GetComponent(t) : null;
                    if (c != null) UnityEngine.Object.DestroyImmediate(c);
                }
                if (!string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(component))
                {
                    var comp = ComponentSerializer.FindComponent(target, component)
                        ?? throw new ArgumentException(
                            ErrorHelper.ComponentNotFound(component, target));
                    var so = new UnityEditor.SerializedObject(comp);
                    var normProp = InputNormalizer.NormalizeProperty(prop, so);
                    var sp = so.FindProperty(normProp)
                        ?? throw new ArgumentException(
                            ErrorHelper.PropertyNotFound(prop, component, assetPath));
                    ValueParser.SetPropertyValue(sp, InputNormalizer.NormalizeValue(value));
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                UndoGroupStack.StageAsset(assetPath);
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                AssetDatabase.SaveAssets();
                return $"ok: {assetPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
