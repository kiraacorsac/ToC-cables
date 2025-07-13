using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;


public enum CableMeshEnding
{
    None,
    OrJunction,
    AndJunction,
    SimpleJunction,
    ConvexJunction,
    ConcaveJunction
}

[Serializable]
public class PointOverride
{
    public int index;
    public CableMeshEnding ending;
}

public class CreationCableMeshGenerator : AbstractCableMeshGenerator
{

    public bool upsideDown = false;
    public float width = 1f;
    public float height = 1f;

    public CableMeshEnding startJunction = CableMeshEnding.None;
    public CableMeshEnding endJunction = CableMeshEnding.None;

    [SerializeField]
    public List<PointOverride> pointOverrides = new List<PointOverride>();


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

        GenerateSamePlaneSimpleJunctionMesh(points, geometry);
        GenerateSegmentMesh(points, geometry);
        GenerateConcavePlaneChangeJunctionMesh(points, geometry);
        GenerateConvexPlaneChangeJunctionMesh(points, geometry);

        // GenerateLastJunctionMesh(points, geometry);

        geometry.IncrementMeshIndex();


        // Debug.Log($"Generated {geometry.Vertices.Count} vertices and {geometry.Triangles.Values.Sum(list => list.Count)} triangles in {geometry.LastMeshIndex} meshes for cable '{name}'.");
        mesh.SetVertices(geometry.Vertices);

        foreach (var triangle in geometry.Triangles)
        {
            mesh.SetTriangles(triangle.Value, triangle.Key);
        }
        // mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void GenerateSamePlaneSimpleJunctionMesh(List<Cable.CablePoint> points, Geometry geometry)
    {
        if (points.Count < 3)
        {
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            if (points[i - 1].normal == points[i].normal)
            {
                Vector3 forward = (points[i].position - points[i - 1].position).normalized;
                int vertexOffset = geometry.Vertices.Count;

                foreach (var vertex in GetSamePlaneSimpleJunctionVertices(points[i].position, points[i].normal, forward))
                {
                    geometry.AddVertex(vertex);
                }

                foreach (var triangle in GetSamePlaneSimpleJunctionTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }
        }
    }

    private void GenerateConcavePlaneChangeJunctionMesh(List<Cable.CablePoint> points, Geometry geometry)
    {
        if (points.Count < 3)
        {
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {

            Vector3 forward = (points[i].position - points[i - 1].position).normalized;

            // Debug.Log($"Checking point {i} at position {points[i].position} with normal {points[i].normal} and forward {forward}, dot {Vector3.Dot(forward, points[i].normal)}");
            if (points[i - 1].normal != points[i].normal && Vector3.Dot(forward, points[i].normal) < -0.75f)
            {
                // Debug.Log($"Generating concave plane change junction for point {i} at position {points[i].position} with normal {points[i].normal} and dot {Vector3.Dot(forward, points[i].normal)}");

                int vertexOffset = geometry.Vertices.Count;

                // take previous point's normal as up
                foreach (var vertex in GetConcavePlaneChangeJunctionVertices(points[i].position, points[i - 1].normal, forward))
                {
                    geometry.AddVertex(vertex);
                }

                foreach (var triangle in GetConcavePlaneChangeJunctionTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }
        }
    }

    private void GenerateConvexPlaneChangeJunctionMesh(List<Cable.CablePoint> points, Geometry geometry)
    {
        if (points.Count < 3)
        {
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {

            Vector3 forward = (points[i].position - points[i - 1].position).normalized;

            if (points[i - 1].normal != points[i].normal && Vector3.Dot(forward, points[i].normal) > 0.75f)
            {
                Debug.Log($"Generating convex plane change junction for point {i} at position {points[i].position} with normal {points[i].normal} and dot {Vector3.Dot(forward, points[i].normal)}");

                int vertexOffset = geometry.Vertices.Count;

                // take previous point's normal as up
                foreach (var vertex in GetConvexPlaneChangeJunctionVertices(points[i].position, points[i - 1].normal, forward))
                {
                    geometry.AddVertex(vertex);
                }

                foreach (var triangle in GetConvexPlaneChangeJunctionTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }
        }
    }

    private void GenerateSegmentMesh(List<Cable.CablePoint> points, Geometry geometry)
    {
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

            foreach (var vertex in GetSegmentCrosscutVertices(points[i].position, points[i].normal, forward))
            {
                geometry.AddVertex(vertex);
            }
            if (i < points.Count - 1)
            {
                // take the previous point's normal as the "up" vector, so we don't get twisting
                foreach (var vertex in GetSegmentCrosscutVertices(points[i + 1].position, points[i].normal, forward))
                {
                    geometry.AddVertex(vertex);
                }
                foreach (var triangle in GetSegmentTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }
        }
    }

    private List<Vector3> GetSegmentCrosscutVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var vertices = new List<Vector3>
        {
            // see concept blend file for the source of magic numbers
            point + 0.052868f * width * right,
            point + 0.031255f * width * right + 0.027186f * height * localUp,
            point + 0.031255f * width * -right + 0.027186f * height * localUp,
            point + 0.052868f * width * -right
        };

        return vertices;
    }

    private List<Vector3Int> GetSegmentTriangles()
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

    # region Same Plane Simple Junction

    private List<Vector3> GetSamePlaneSimpleJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        forward = forward.normalized;
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var vertices = new List<Vector3>
        {
            // see concept blend file for the source of magic numbers
            //bottom
            point + 0.071789f * width * right + 0.071789f * forward * width,
            point + 0.071789f * width * right + 0.071789f * -forward * width,
            point + 0.071789f * -width * right + 0.071789f * -forward * width,
            point + 0.071789f * -width * right + 0.071789f * forward * width,
            //top
            point + 0.041164f * width * right + 0.041164f * forward * width + 0.038168f * height * localUp,
            point + 0.041164f * width * right + 0.041164f * -forward * width + 0.038168f * height * localUp,
            point + 0.041164f * -width * right + 0.041164f * -forward * width + 0.038168f * height * localUp,
            point + 0.041164f * -width * right + 0.041164f * forward * width + 0.038168f * height * localUp
        };
        return vertices;
    }

    private List<Vector3Int> GetSamePlaneSimpleJunctionTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 1, 5),
            new Vector3Int(0, 5, 4),
            new Vector3Int(1, 2, 6),
            new Vector3Int(1, 6, 5),
            new Vector3Int(2, 3, 7),
            new Vector3Int(2, 7, 6),
            new Vector3Int(3, 0, 4),
            new Vector3Int(3, 4, 7),
            new Vector3Int(4, 5, 6),
            new Vector3Int(4, 6, 7),
        };
    }
    #endregion

    # region Concave Plane Change Junction

    private List<Vector3> GetConcavePlaneChangeJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        forward = forward.normalized;
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;

        Debug.Log($"Generating concave plane change junction at point {point} (normal: {normal}, forward {forward}, up: {up}, right: {right}, localUp: {localUp})");

        if (upsideDown)
        {
            localUp = -localUp;
        }

        var vertices = new List<Vector3>
        {
            // see concept blend file for the source of magic numbers
            //front
            point + 0.056414f * width * right + -0.033217f * forward + 0.000000f * height * localUp,
            point + 0.033691f * width * right + -0.033217f * forward + 0.029491f * height * localUp,
            point + 0.033691f * width * -right + -0.033217f * forward + 0.029491f * height * localUp,
            point + 0.056414f * width * -right + -0.033217f * forward + 0.000000f * height * localUp,

            //middle
            point + 0.056414f * width * right + 0.000000f * forward + 0.000000f * height * localUp,
            point + 0.033691f * width * right + -0.030643f * forward + 0.030596f * height * localUp,
            point + 0.033691f * width * -right + -0.030643f * forward + 0.030596f * height * localUp,
            point + 0.056414f * width * -right + 0.000000f * forward + 0.000000f * height * localUp,

            //back
            point + 0.056414f * width * right + 0.000000f * forward + 0.033207f * height * localUp,
            point + 0.033691f * width * right + -0.029549f * forward + 0.033207f * height * localUp,
            point + 0.033691f * width * -right + -0.029549f * forward + 0.033207f * height * localUp,
            point + 0.056414f * width * -right + 0.000000f * forward + 0.033207f * height * localUp
        };
        return vertices;
    }

    private List<Vector3Int> GetConcavePlaneChangeJunctionTriangles()
    {
        return new List<Vector3Int>
        {
            // front
            new Vector3Int(3, 0, 1),
            new Vector3Int(1, 2, 3),

            // middle front
            new Vector3Int(0, 5, 1),
            new Vector3Int(0, 4, 5),
            new Vector3Int(1, 5, 6),
            new Vector3Int(1, 6, 2),
            new Vector3Int(2, 6, 7),
            new Vector3Int(2, 7, 3),

            // middle back
            new Vector3Int(4, 8, 9),
            new Vector3Int(4, 9, 5),
            new Vector3Int(5, 9, 10),
            new Vector3Int(5, 10, 6),
            new Vector3Int(6, 10, 11),
            new Vector3Int(6, 11, 7),

            // back
            new Vector3Int(9, 8, 11),
            new Vector3Int(9, 11, 10),
        };
    }

    #endregion

    #region Convex Plane Change Junction

    private List<Vector3> GetConvexPlaneChangeJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        forward = forward.normalized;
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;



        if (upsideDown)
        {
            localUp = -localUp;
        }
        localUp = -localUp;

        // forward = -forward;

        var vertices = new List<Vector3>
        {
            // see concept blend file for the source of magic numbers
            //front
            point + 0.052868f * width * right + -0.004057f * forward  + 0.0f * height * localUp,
            point + 0.031255f * width * right + -0.004057f  * forward + -0.027186f* height * localUp,
            point + 0.031255f * width * -right + -0.004057f * forward + -0.027186f * height * localUp,
            point + 0.052868f * width * -right + -0.004057f * forward + 0.0f * height * localUp,

            //middle bottom
            point + 0.052868f * width * right + 0.0f * forward + 0.0f * height * localUp,
            point + 0.031255f * width * right + 0.0f * forward + -0.027186f * height * localUp,
            point + 0.031255f * width * -right + 0.0f * forward + -0.027186f * height * localUp,
            point + 0.052868f * width * -right + 0.0f * forward + 0.0f * height * localUp,

            //middle top
            point + 0.031255f * width * right + 0.027186f * forward + 0.0f * height * localUp,
            point + 0.031255f * width * -right + 0.027186f * forward + 0.0f * height * localUp,

            //top
            point + 0.052868f * width * right + 0.0f * forward + 0.004057f * height * localUp,
            point + 0.031255f * width * right + 0.027186f * forward + 0.004057f * height * localUp,
            point + 0.031255f * width * -right + 0.027186f * forward + 0.004057f * height * localUp,
            point + 0.052868f * width * -right + 0.0f * forward + 0.004057f * height * localUp
        };
        return vertices;
    }

    private List<Vector3Int> GetConvexPlaneChangeJunctionTriangles()
    {
        return new List<Vector3Int>
        {
            // front
            new Vector3Int(1,0,4),
            new Vector3Int(1,4,5),
            new Vector3Int(2,1,5),
            new Vector3Int(2,5,6),
            new Vector3Int(3,2,6),
            new Vector3Int(3,6,7),
            // middle
            new Vector3Int(4,8,5),
            new Vector3Int(5,8,6),
            new Vector3Int(6,8,9),
            new Vector3Int(6,9,7),
            // top
            new Vector3Int(4,10,11),
            new Vector3Int(4,11,8),
            new Vector3Int(8,11,9),
            new Vector3Int(9,11,12),
            new Vector3Int(9,12,7),
            new Vector3Int(7,12,13)
        };
    }
    #endregion
    // this is stupid let's do it in normals in texture
    // private List<Vector3> GetCrosscutVertices(Vector3 point, Vector3 normal, Vector3 forward)
    // {
    //     Vector3 up = normal.normalized;
    //     Vector3 right = Vector3.Cross(forward, up).normalized;
    //     Vector3 localUp = -Vector3.Cross(forward, right).normalized;
    //     if (upsideDown)
    //     {
    //         localUp = -localUp;
    //     }

    //     var face = new List<Vector3>
    //     {
    //         // see concept blend file for the source of magic numbers
    //         point + 0.052868f * width * right,
    //         point + 0.031255f * width * right + 0.027186f * height * localUp,
    //         point + 0.028069f * width * right + 0.027186f * height * localUp,
    //         point + 0.025880f * width * right + 0.024925f * height * localUp,

    //         // mirrored vertices
    //         point + 0.025880f * width * -right + 0.024925f * height * localUp,
    //         point + 0.028069f * width * -right + 0.027186f * height * localUp,
    //         point + 0.031255f * width * -right + 0.027186f * height * localUp,
    //         point + 0.052868f * width * -right
    //     };

    //     return face;
    // }

    // private List<Vector3Int> GetCrosscutTriangles()
    // {
    //     return new List<Vector3Int>
    //     {
    //         new Vector3Int(0, 8, 1),
    //         new Vector3Int(1, 8, 9),
    //         new Vector3Int(1, 9, 2),
    //         new Vector3Int(2, 9, 10),
    //         new Vector3Int(2, 10, 3),
    //         new Vector3Int(3, 10, 11),
    //         new Vector3Int(3, 11, 4),
    //         new Vector3Int(4, 11, 12),
    //         new Vector3Int(4, 12, 5),
    //         new Vector3Int(5, 12, 13),
    //         new Vector3Int(5, 13, 6),
    //         new Vector3Int(6, 13, 14),
    //         new Vector3Int(6, 14, 7),
    //         new Vector3Int(7, 14, 15),
    //     };
    // }


    private void AddDebugMeshToGeometry(Geometry geometry, Vector3 point, Vector3 forward)
    {
        var triangleOffset = geometry.Vertices.Count;
        var debugFace = GetDebugFaceVertices(point, forward);
        foreach (var vertex in debugFace)
        {
            geometry.AddVertex(vertex);
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
