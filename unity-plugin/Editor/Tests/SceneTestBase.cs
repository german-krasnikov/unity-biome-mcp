using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    public abstract class SceneTestBase : UnityMcpTestBase
    {
        // Kept public for compatibility with focused cleanup tests and callers.
        public void CleanDirtyScene()
        {
            ResetManagedTestScene();
        }
    }
}
