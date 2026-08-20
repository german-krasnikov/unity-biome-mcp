using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureFramePulse : MonoBehaviour
    {
        [SerializeField] int frameCount;

        public int FrameCount => frameCount;

        Renderer cachedRenderer;

        void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        void Update()
        {
            frameCount++;
            if (cachedRenderer == null) return;
            cachedRenderer.material.color = (frameCount % 2 == 0) ? Color.red : Color.blue;
        }
    }
}
