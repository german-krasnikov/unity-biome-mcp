using System;
using UnityEngine;

[Serializable] public struct SimpleHashId { public string _id; public int _hash; }
[Serializable] public struct ThreeFieldStruct { public string name; public int value; public float ratio; }

public class TestStructScript : MonoBehaviour
{
    public SimpleHashId itemId;
    public ThreeFieldStruct threeField;
}
