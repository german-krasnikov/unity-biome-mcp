using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureMoveAdapter : MonoBehaviour
    {
        [SerializeField] FixtureMover mover;
        [SerializeField] FixtureState state;

        // Unique method name — no ambiguity (used by MOVE profile)
        public void MoveAsync(Vector3 target)
        {
            if (mover) mover.StartMoveToPoint(target, success => {
                if (state) state.Record(nameof(MoveAsync), target.ToString(), success ? 1 : 0);
            });
        }

        // Unique method name — no ambiguity (used by TELEPORT profile)
        public void Teleport(Vector3 target)
        {
            transform.position = target;
            var physicsRoot = transform.Find("PhysicsRoot");
            if (physicsRoot) physicsRoot.position = target;
            if (state) state.Record(nameof(Teleport), target.ToString(), 1);
        }
    }
}
