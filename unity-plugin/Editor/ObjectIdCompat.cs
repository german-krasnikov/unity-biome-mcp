// unity-plugin/Editor/ObjectIdCompat.cs
// Platform isolation for Unity 6000.4+ EntityId API migration.
// ALL #if guards live here. Callers see clean static methods.
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Compat bridge: instance-ID-based APIs deprecated in Unity 6000.4 (error in 6000.5).
    /// Old path: GetInstanceID / InstanceIDToObject / objectReferenceInstanceIDValue.
    /// New path: GetEntityId / EntityIdToObject / objectReferenceEntityIdValue.
    /// </summary>
    internal static class ObjectIdCompat
    {
#if UNITY_6000_4_OR_NEWER
        internal static ulong GetRawId(Object obj)
            => obj == null ? 0UL : EntityId.ToULong(obj.GetEntityId());

        internal static Object ResolveObject(ulong rawId)
            => rawId == 0UL ? null : EditorUtility.EntityIdToObject(EntityId.FromULong(rawId));

        internal static bool HasSerializedReference(SerializedProperty property)
            => property.objectReferenceEntityIdValue != EntityId.None;
#else
        internal static ulong GetRawId(Object obj)
            => obj == null ? 0UL : unchecked((ulong)(long)obj.GetInstanceID());

        internal static Object ResolveObject(ulong rawId)
            => rawId == 0UL ? null
               : EditorUtility.InstanceIDToObject(unchecked((int)rawId));

        internal static bool HasSerializedReference(SerializedProperty property)
            => property.objectReferenceInstanceIDValue != 0;
#endif
    }
}
