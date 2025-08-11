using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// surely this is something that Unity has already, but I don't know where
class Geometry
{
    public List<Vector3> Vertices { get; private set; }
    public Dictionary<int, List<int>> Triangles { get; private set; }
    public List<Vector2> UVs { get; private set; }
    public int LastMeshIndex { get; private set; } = 0;

    public Geometry()
    {
        Vertices = new List<Vector3>();
        Triangles = new Dictionary<int, List<int>>();
        UVs = new List<Vector2>();
    }


    public void AddVertex(Vector3 point)
    {
        Vertices.Add(point);
    }

    public void AddTriangle(int indexA, int indexB, int indexC, int offset = 0, int meshIndex = -1)
    {
        if (meshIndex == -1)
        {
            meshIndex = LastMeshIndex;
        }
        Triangles.TryGetValue(meshIndex, out var triangleList);
        if (triangleList == null)
        {
            triangleList = new List<int>();
            Triangles[meshIndex] = triangleList;
        }
        triangleList.Add(indexA + offset);
        triangleList.Add(indexB + offset);
        triangleList.Add(indexC + offset);
    }

    public void AddVertexUV(Vector2 uv)
    {
        UVs.Add(uv);
    }

    // public void AddTriangleUV(List<Vector2> uvs){
    //     foreach(var uv in uvs){
    //         UVs.Add(uv);
    //     }
    // }

    public void IncrementMeshIndex()
    {
        LastMeshIndex++;
    }
}
