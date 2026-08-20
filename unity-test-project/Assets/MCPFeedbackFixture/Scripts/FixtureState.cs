using System.Collections;
using UnityEngine;

namespace McpFeedbackFixture
{
    public class FixtureState : MonoBehaviour
    {
        [SerializeField] int value = 100;
        [SerializeField] bool completed;
        [SerializeField] int mutationCounter;
        [SerializeField] string lastMethod = "";
        [SerializeField] string lastFirstArgument = "";
        [SerializeField] string lastSecondArgument = "";
        [SerializeField] int lastIntegerArgument;
        [SerializeField] int acceptedCount;
        [SerializeField] int lastAcceptedId;

        public int Value => value;
        public bool Completed => completed;
        public int MutationCounter => mutationCounter;
        public string LastMethod => lastMethod;
        public string LastFirstArgument => lastFirstArgument;
        public string LastSecondArgument => lastSecondArgument;
        public int LastIntegerArgument => lastIntegerArgument;
        public int AcceptedCount => acceptedCount;
        public int LastAcceptedId => lastAcceptedId;

        public void ResetState()
        {
            value = 100;
            completed = false;
            mutationCounter = 0;
            lastMethod = "";
            lastFirstArgument = "";
            lastSecondArgument = "";
            lastIntegerArgument = 0;
            acceptedCount = 0;
            lastAcceptedId = 0;
        }

        public void Increment()
        {
            value++;
            mutationCounter++;
            lastMethod = nameof(Increment);
        }

        public void Record(string first, string second, int integer)
        {
            lastFirstArgument = first;
            lastSecondArgument = second;
            lastIntegerArgument = integer;
            mutationCounter++;
            lastMethod = nameof(Record);
        }

        public void Accept(int id)
        {
            acceptedCount++;
            lastAcceptedId = id;
            mutationCounter++;
            lastMethod = nameof(Accept);
        }

        public void CompleteAfterSeconds(float seconds)
        {
            StartCoroutine(CompleteCoroutine(seconds));
        }

        IEnumerator CompleteCoroutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            completed = true;
            lastMethod = nameof(CompleteAfterSeconds);
        }
    }
}
