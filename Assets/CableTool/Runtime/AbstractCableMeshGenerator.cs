using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public abstract class AbstractCableMeshGenerator : MonoBehaviour
{
    abstract public void GenerateMesh(List<Vector3> points);
}
