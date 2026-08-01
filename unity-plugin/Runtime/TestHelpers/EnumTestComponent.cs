using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public enum ToolType { None = 0, Hammer = 1, Wrench = 5 }

    [System.Flags]
    public enum PermFlags { None = 0, Read = 1, Write = 2, Execute = 4 }

    public class EnumTestComponent : MonoBehaviour
    {
        public ToolType _toolType;
        public PermFlags _perms;
        public KeyCode _keyCode;
    }
}
