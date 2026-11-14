using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        public static string FindObjects(string name, string tag, string layer, string component)
        {
            var results = new StringBuilder();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded || scene.name == "DontDestroyOnLoad") continue;
                foreach (var root in scene.GetRootGameObjects())
                    TraverseAndFilter(root.transform, name, tag, layer, component, results);
            }
            return results.ToString().TrimEnd('\n');
        }

        private static void TraverseAndFilter(Transform t, string name, string tag, string layer, string component, StringBuilder results)
        {
            var go = t.gameObject;
            bool match = true;

            if (!string.IsNullOrEmpty(name) && !go.name.Contains(name)) match = false;
            if (!string.IsNullOrEmpty(tag) && go.tag != tag) match = false;
            if (!string.IsNullOrEmpty(layer) && LayerMask.LayerToName(go.layer) != layer) match = false;
            if (!string.IsNullOrEmpty(component) && go.GetComponent(ComponentSerializer.StripNamespace(component)) == null) match = false;

            if (match) results.AppendLine(ComponentSerializer.GetPath(go));

            foreach (Transform child in t)
                TraverseAndFilter(child, name, tag, layer, component, results);
        }

        internal static Type FindType(string typeName)
        {
            // Fast path: common Unity types.
            // Skip abstract/open-generic — AddComponent rejects them.
            var quick = Type.GetType("UnityEngine." + typeName + ", UnityEngine")
                     ?? Type.GetType(typeName + ", Assembly-CSharp");
            if (quick != null && !quick.IsAbstract && !quick.IsGenericTypeDefinition) return quick;

            // Full scan by full name — SafeGetTypes preserves partial loads.
            // Skip abstract/open-generic here too for consistency.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = SafeGetTypes(assembly)
                    .FirstOrDefault(t => !t.IsAbstract && !t.IsGenericTypeDefinition &&
                                         (t.FullName == typeName ||
                                          t.FullName == "UnityEngine." + typeName));
                if (type != null) return type;
            }

            // Short-name scan (no dot = unqualified)
            if (!typeName.Contains('.'))
            {
                // Fast path: TypeCache avoids full reflection load.
                // Filter abstract/open-generic: AddComponent rejects them with an
                // uncontrolled InvalidOperationException; return null so hint-logic runs.
                var cached = UnityEditor.TypeCache.GetTypesDerivedFrom<Component>()
                    .Where(t => t.Name == typeName && !t.IsAbstract && !t.IsGenericTypeDefinition)
                    .ToList();
                if (cached.Count == 1) return cached[0];
                if (cached.Count > 1)
                    throw new ArgumentException(
                        $"Ambiguous: '{typeName}' = {cached[0].FullName} and {cached[1].FullName}. Use full namespace.");

                // Full scan fallback
                Type found = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in SafeGetTypes(asm))
                    {
                        if (t.Name == typeName && typeof(Component).IsAssignableFrom(t)
                            && !t.IsAbstract && !t.IsGenericTypeDefinition)
                        {
                            if (found != null)
                                throw new ArgumentException(
                                    $"Ambiguous: '{typeName}' = {found.FullName} and {t.FullName}. Use full namespace.");
                            found = t;
                        }
                    }
                }
                if (found != null) return found;
            }

            return null;
        }

        internal static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            { return ex.Types.Where(t => t != null); }
        }
    }
}
