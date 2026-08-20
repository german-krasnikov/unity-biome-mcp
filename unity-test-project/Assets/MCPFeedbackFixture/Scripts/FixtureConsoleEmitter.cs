using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureConsoleEmitter : MonoBehaviour
    {
        public void EmitInfo() =>
            Debug.Log("FixtureConsoleEmitter:Info");

        public void EmitWarning() =>
            Debug.LogWarning("FixtureConsoleEmitter:Warning");

        public void EmitError() =>
            Debug.LogError("FixtureConsoleEmitter:Error");

        public void EmitBurst(int count)
        {
            for (int i = 0; i < count; i++)
                Debug.Log($"FixtureConsoleEmitter:Info:{i}");
        }
    }
}
