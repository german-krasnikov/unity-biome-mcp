using System.Collections;
using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureAsyncState : MonoBehaviour
    {
        [SerializeField] float progress;
        [SerializeField] int startCount;
        [SerializeField] int terminalCount;
        [SerializeField] bool isRunning;

        public float Progress => progress;
        public int StartCount => startCount;
        public int TerminalCount => terminalCount;
        public bool IsRunning => isRunning;

        Coroutine activeCoroutine;

        public void StartOperation(float duration)
        {
            if (isRunning) Cancel();
            startCount++;
            isRunning = true;
            activeCoroutine = StartCoroutine(Run(duration));
        }

        public void Cancel()
        {
            if (activeCoroutine != null) { StopCoroutine(activeCoroutine); activeCoroutine = null; }
            isRunning = false;
            terminalCount++;
        }

        public void Reset()
        {
            if (activeCoroutine != null) { StopCoroutine(activeCoroutine); activeCoroutine = null; }
            progress = 0f;
            startCount = 0;
            terminalCount = 0;
            isRunning = false;
        }

        IEnumerator Run(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= duration)
                {
                    progress = 1f;
                    isRunning = false;
                    terminalCount++;
                    activeCoroutine = null;
                    yield break;
                }
                progress = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
        }
    }
}
