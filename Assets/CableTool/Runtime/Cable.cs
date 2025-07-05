using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AbstractCableMeshGenerator))]
public class Cable : MonoBehaviour
{
    //point and its corresponding normal/local up vector
    [Serializable]
    public struct CablePoint
    {
        public Vector3 position;
        public Vector3 normal;
    }

    [SerializeField]
    public List<CablePoint> Points = new();

    public void GenerateMesh()
    {
        AbstractCableMeshGenerator meshGenerator = GetComponent<AbstractCableMeshGenerator>();
        meshGenerator.GenerateMesh(Points);
    }
}
