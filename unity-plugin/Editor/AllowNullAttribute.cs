// G51: AllowNull — marks an ObjectReference field as intentionally nullable.
// Fields with this attribute are excluded from validate_references MISSING detection.
using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Apply to a serialized UnityEngine.Object field to indicate it is intentionally
    /// null. validate_references will not report it as MISSING even if it has a
    /// dangling object reference ID.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class AllowNullAttribute : Attribute { }
}
