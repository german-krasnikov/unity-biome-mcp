using UnityEngine;

// Test fixture for Cycle 6d (#29): MonoBehaviour with no [SerializeField] fields
// produces 0 visible serialized properties. Lives in Assets/ (runtime asmdef)
// so AddComponent<EmptyMono>() works reliably in NUnit EditMode tests.
public class EmptyMono : MonoBehaviour
{
    private int unused;
}
