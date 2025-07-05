using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AbstractCableMeshGenerator))]
public class Cable : MonoBehaviour
{
    public List<Vector3> points = new();
    public void AddPoint(Vector3 point)
    {
        points.Add(transform.InverseTransformPoint(point));
        GenerateMesh();
    }

    public void GenerateMesh()
    {
        AbstractCableMeshGenerator meshGenerator = GetComponent<AbstractCableMeshGenerator>();
        meshGenerator.GenerateMesh(points);
    }
}
