using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class CreationCableMeshGenerator : AbstractCableMeshGenerator
{

    public bool upsideDown = false;
    public float scale = 1f;
    public float width = 1f;
    public float height = 1f;


    override public void GenerateMesh(List<Cable.CablePoint> points)
    {
        Mesh mesh = new Mesh();


        if (points.Count < 2)
        {
            Debug.LogError("Not enough points to generate cable mesh for . At least 2 points are required.");
            GetComponent<MeshFilter>().mesh = null;
            return;
        }


        Geometry geometry = new Geometry();


        for (int i = 0; i < points.Count; i++)
        {

            Vector3 forward;
            if (i < points.Count - 1)
            {
                forward = (points[i + 1].position - points[i].position).normalized;
            }
            else
            {
                forward = (points[i].position - points[i - 1].position).normalized;
            }
            int vertexOffset = geometry.Vertices.Count;

            foreach (var vertex in GetCrosscutVertices(points[i].position, points[i].normal, forward))
            {
                geometry.AddVertex(vertex);
            }
            if (i < points.Count - 1)
            {
                // take the previous point's normal as the "up" vector, so we don't get twisting
                foreach (var vertex in GetCrosscutVertices(points[i + 1].position, points[i].normal, forward))
                {
                    geometry.AddVertex(vertex);
                }
                foreach (var triangle in GetCrosscutTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }


        }
        geometry.IncrementMeshIndex();


        Debug.Log($"Generated {geometry.Vertices.Count} vertices and {geometry.Triangles.Values.Sum(list => list.Count)} triangles in {geometry.LastMeshIndex} meshes for cable '{name}'.");
        mesh.SetVertices(geometry.Vertices);

        foreach (var triangle in geometry.Triangles)
        {
            mesh.SetTriangles(triangle.Value, triangle.Key);
        }
        // mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }


    private List<Vector3> GetCrosscutVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var face = new List<Vector3>
        {
            // see concept blend file for the source of magic numbers
            point + 0.052868f * width * right,
            point + 0.031255f * width * right + 0.027186f * height * localUp,
            // TODO: the middle part :>
            point + 0.031255f * width * -right + 0.027186f * height * localUp,
            point + 0.052868f * width * -right
        };

        return face;
    }

    private List<Vector3Int> GetCrosscutTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 4, 1),
            new Vector3Int(1, 4, 5),
            new Vector3Int(1, 5, 2),
            new Vector3Int(2, 5, 6),
            new Vector3Int(2, 6, 3),
            new Vector3Int(3, 6, 7),
        };
    }


    private void AddDebugMeshToGeometry(Geometry geometry, Vector3 point, Vector3 forward)
    {
        var triangleOffset = geometry.Vertices.Count;
        var debugFace = GetDebugFaceVertices(point, forward);
        foreach (var vertex in debugFace)
        {
            geometry.AddVertex(vertex * scale);
        }
        var debugTriangles = GetDebugFaceTriangles();
        foreach (var triangle in debugTriangles)
        {
            geometry.AddTriangle(triangle.x, triangle.y, triangle.z, triangleOffset);
        }
    }

    private List<Vector3> GetDebugFaceVertices(Vector3 point, Vector3 forward)
    {
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var face = new List<Vector3>
        {
            point + localUp * height,
            point + right * width + localUp * height,
            point + right * width,
            point
        };
        return face;
    }

    private List<Vector3Int> GetDebugFaceTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 1, 2),
            new Vector3Int(0, 2, 3)
        };
    }
}
