using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace McpFeedbackFixture
{
    [Serializable]
    public struct RefEntry
    {
        public string label;
        public UnityEngine.Object target;
    }

    [Serializable]
    public class RefNode
    {
        public string name;
        public UnityEngine.Object reference;
    }

    [Serializable]
    public class RefLeaf : RefNode
    {
        public int leafValue;
    }

    public class FixtureReferenceGraph : MonoBehaviour
    {
        // 1. Direct Object reference
        [SerializeField] UnityEngine.Object directRef;

        // 2. Object array
        [SerializeField] UnityEngine.Object[] arrayRefs = new UnityEngine.Object[3];

        // 3. List<Object>
        [SerializeField] List<UnityEngine.Object> listRefs = new();

        // 4. Array of struct with Object fields
        [SerializeField] RefEntry[] structArrayRefs = new RefEntry[2];

        // 5. [SerializeReference] for managed polymorphic refs
        [SerializeReference] List<RefNode> managedRefs = new();

        // 6. Intentionally null/missing reference — for broken-ref testing
        [SerializeField] UnityEngine.Object missingRef;

        // --- UnityEvents with persistent listeners ---
        // 7. void event
        [SerializeField] UnityEvent onVoidEvent = new();
        // 8. int event
        [SerializeField] UnityEvent<int> onIntEvent = new();
        // 9. string event
        [SerializeField] UnityEvent<string> onStringEvent = new();
        // 10. Object event
        [SerializeField] UnityEvent<UnityEngine.Object> onObjectEvent = new();

        // --- Expected counts for test verification ---
        // Total object reference edges (all slots, including nulls).
        // directRef(1) + arrayRefs(3) + listRefs(count) + structArrayRefs(2)
        // + managedRefs non-null nodes + missingRef(1, always null but still a slot)
        public int ExpectedEdgeCount => 1 + arrayRefs.Length + listRefs.Count
            + structArrayRefs.Length + CountManagedEdges() + 1;

        // Total persistent listener count across all UnityEvents
        public int ExpectedListenerCount =>
            onVoidEvent.GetPersistentEventCount()
            + onIntEvent.GetPersistentEventCount()
            + onStringEvent.GetPersistentEventCount()
            + onObjectEvent.GetPersistentEventCount();

        int CountManagedEdges()
        {
            int count = 0;
            if (managedRefs != null)
                foreach (var node in managedRefs)
                    if (node != null) count++;
            return count;
        }
    }
}
