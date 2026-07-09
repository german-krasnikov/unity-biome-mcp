using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        // Resolves path → go and component name → Component in one call.
        // Throws ArgumentException if either is missing.
        internal static (GameObject go, Component comp) ResolveComponent(string path, string component)
        {
            component = ComponentSerializer.StripNamespace(component);
            var go = ComponentSerializer.FindObjectOrThrow(path);
            component = InputNormalizer.NormalizeComponent(component, go);
            var comp = go.GetComponent(component);
            if (comp == null) throw new ArgumentException(ErrorHelper.ComponentNotFound(component, go));
            return (go, comp);
        }

        public static string CreateObject(string name, string parent, string components, string primitive = null, string prefabPath = null, string scene = null)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                    throw new ArgumentException($"Prefab not found: {prefabPath} (exists={System.IO.File.Exists(prefabPath)})");
                var prefabType = PrefabUtility.GetPrefabAssetType(prefabAsset);
                if (prefabType == PrefabAssetType.NotAPrefab)
                    throw new ArgumentException($"Not a prefab: {prefabPath} (type={prefabAsset.GetType().Name})");
                go = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (go == null)
                    throw new InvalidOperationException($"Failed to instantiate: {prefabPath} (prefabType={prefabType})");
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }
            else if (!string.IsNullOrEmpty(primitive) && Enum.TryParse<PrimitiveType>(primitive, true, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }
            else
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }

            if (!string.IsNullOrEmpty(scene))
            {
                var targetScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene);
                if (!targetScene.IsValid())
                    throw new ArgumentException($"Scene not found: {scene}");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, targetScene);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = ComponentSerializer.FindObjectOrThrow(parent);
                Undo.SetTransformParent(go.transform, parentGo.transform, false, $"Reparent {name}");
                EditorUtility.SetDirty(go);
                if (!EditorApplication.isPlaying)
                    EditorSceneManager.MarkSceneDirty(go.scene);
            }

            if (!string.IsNullOrEmpty(components))
            {
                var types = components.Split(',');
                foreach (var typeName in types)
                {
                    var type = FindType(typeName.Trim());
                    if (type == null)
                        throw new ArgumentException($"Component type not found: {typeName}");
                    Undo.AddComponent(go, type);
                }
            }

            return ComponentSerializer.GetPath(go);
        }

        public static string SetActive(string path, bool active)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);
            Undo.RecordObject(go, $"SetActive {path}");
            go.SetActive(active);
            EditorUtility.SetDirty(go);
            return $"{ComponentSerializer.GetPath(go)} active={active}";
        }

        public static string SetMaterial(string path, string color, string shader)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                throw new ArgumentException(ErrorHelper.ComponentNotFound("Renderer", go));

            var shaderObj = Shader.Find(shader ?? "Universal Render Pipeline/Lit");
            if (shaderObj == null)
                shaderObj = Shader.Find("Standard");
            if (shaderObj == null)
                throw new ArgumentException($"Shader not found: {shader}");

            var mat = new Material(shaderObj);
            Undo.RegisterCreatedObjectUndo(mat, "Create Material");
            if (!string.IsNullOrEmpty(color))
            {
                if (!ColorUtility.TryParseHtmlString(color, out var c))
                    throw new ArgumentException($"Invalid color: {color}");
                mat.color = c;
            }
            Undo.RecordObject(renderer, "Set Material");
            renderer.sharedMaterial = mat;
            return $"shader={renderer.sharedMaterial.shader.name} color=#{ColorUtility.ToHtmlStringRGBA(renderer.sharedMaterial.color)}";
        }

        public static string SetParent(string path, string newParent, bool worldPositionStays = true)
        {
            var go = ComponentSerializer.FindObject(path, strict: true);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(newParent))
            {
                var parentGo = ComponentSerializer.FindObject(newParent, strict: true);
                if (parentGo == null)
                    throw new ArgumentException(ErrorHelper.ObjectNotFound(newParent));
                parentTransform = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, parentTransform, worldPositionStays, $"Set parent {path}");
            EditorUtility.SetDirty(go);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
            return ComponentSerializer.GetPath(go);
        }

        public static string RenameObject(string path, string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("rename_object: name cannot be empty");
            var go = ComponentSerializer.FindObjectOrThrow(path);
            Undo.RecordObject(go, $"Rename {go.name} → {name}");
            go.name = name;
            EditorUtility.SetDirty(go);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
            return ComponentSerializer.GetPath(go);
        }

        public static string SetSiblingIndex(string path, int index)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);
            Undo.RecordObject(go.transform, "Set Sibling Index");
            go.transform.SetSiblingIndex(index);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
            return $"ok: {ComponentSerializer.GetPath(go)} index={go.transform.GetSiblingIndex()}";
        }

        public static void DeleteObject(string path, bool force = false)
        {
            var go = ComponentSerializer.FindObject(path, strict: true);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
            if (!force && go.transform.childCount > 0)
                throw new ArgumentException(
                    $"'{path}' has {go.transform.childCount} children. Pass force=true to delete with all descendants.");
            Undo.DestroyObjectImmediate(go);
        }

        public static void DeleteObject(int instanceId, bool force = false)
        {
            var go = ComponentSerializer.FindObjectById(instanceId);
            if (go == null)
                throw new ArgumentException($"Object not found: #{instanceId}");
            if (!force && go.transform.childCount > 0)
                throw new ArgumentException(
                    $"'#{instanceId}' has {go.transform.childCount} children. Pass force=true to delete with all descendants.");
            Undo.DestroyObjectImmediate(go);
        }

        public static void ManageComponent(string path, string type, string action)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);

            if (action == "add")
            {
                var componentType = FindType(type);
                if (componentType == null)
                {
                    var candidates = ErrorHelper.ClosestComponentTypes(type);
                    var hint = candidates.Count > 0
                        ? $" Did you mean: {string.Join(", ", candidates)}?"
                        : "";
                    throw new ArgumentException($"Component type not found: {type}.{hint}");
                }
                if (go.GetComponent(componentType) != null)
                    throw new ArgumentException(
                        $"'{type}' already exists on '{go.name}'. " +
                        "Use action=remove first, or set_property to modify.");
                Undo.AddComponent(go, componentType);
            }
            else if (action == "remove")
            {
                var componentType = FindType(type);
                var component = componentType != null
                    ? go.GetComponent(componentType)
                    : go.GetComponent(ComponentSerializer.StripNamespace(type));
                if (component == null)
                    throw new ArgumentException(ErrorHelper.ComponentNotFound(type, go));
                Undo.DestroyObjectImmediate(component);
            }
            else
            {
                throw new ArgumentException($"Invalid action: {action}");
            }
        }

    }
}
