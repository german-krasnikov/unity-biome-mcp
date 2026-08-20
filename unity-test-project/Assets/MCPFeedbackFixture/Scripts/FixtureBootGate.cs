using System.Collections;
using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureBootGate : MonoBehaviour
    {
        [SerializeField] int readyDelayFrames;
        [SerializeField] bool ready;

        public int ReadyDelayFrames => readyDelayFrames;
        public bool Ready => ready;

        void Start()
        {
            if (readyDelayFrames == 0)
            {
                ready = true;
                return;
            }
            StartCoroutine(WaitFrames());
        }

        IEnumerator WaitFrames()
        {
            for (int i = 0; i < readyDelayFrames; i++)
                yield return null;
            ready = true;
        }
    }
}
