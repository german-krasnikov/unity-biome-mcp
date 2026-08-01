using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public class ReflectionTestComponent : MonoBehaviour
    {
        [SerializeField] public Light _scalarRef;
        [SerializeField] public Light[] _arrayRef;
        [SerializeField] public List<Light> _listRef;
    }
}
