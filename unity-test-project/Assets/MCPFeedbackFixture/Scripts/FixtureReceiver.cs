using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureReceiver : MonoBehaviour
    {
        [SerializeField] FixtureState state;
        [SerializeField] int acceptedCount;
        [SerializeField] int lastAcceptedId;

        public int AcceptedCount => acceptedCount;
        public int LastAcceptedId => lastAcceptedId;

        // Accept a custom value type — tests MCP-INVOKE-037
        public void AcceptId(FixtureId id)
        {
            acceptedCount++;
            lastAcceptedId = (int)id;
            if (state) state.Accept((int)id);
        }

        // Multi-arg method — tests argument parsing (MCP-INVOKE-038)
        public void Record(string first, string second, int integer)
        {
            if (state) state.Record(first, second, integer);
        }
    }
}
