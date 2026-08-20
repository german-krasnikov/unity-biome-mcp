using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureMover : MonoBehaviour
    {
        [SerializeField] FixtureState state;

        // Overload 1: no callback
        public void StartMoveToPoint(Vector3 target)
        {
            transform.position = target;
            if (state) state.Record(nameof(StartMoveToPoint), "Vector3", 1);
        }

        // Overload 2: with callback — deliberately ambiguous
        public void StartMoveToPoint(Vector3 target, System.Action<bool> onComplete)
        {
            transform.position = target;
            if (state) state.Record(nameof(StartMoveToPoint), "Vector3,Action", 2);
            onComplete?.Invoke(true);
        }
    }
}
