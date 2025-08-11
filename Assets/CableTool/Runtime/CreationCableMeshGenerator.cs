using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;


public enum CableJunctionType
{
    Default,
    None,
    OrJunction,
    AndJunction,
    SimpleJunction,
    ConvexJunction,
    ConcaveJunction
}

public static class CableJunctionTypeExtensions
{
    public static CableJunctionType FromCableExtensionType(this CableJunctionType junctionType, CableExtensionType extensionType)
    {
        return extensionType switch
        {
            CableExtensionType.None => CableJunctionType.None,
            CableExtensionType.Or => CableJunctionType.OrJunction,
            CableExtensionType.And => CableJunctionType.AndJunction,
            CableExtensionType.Passthrough => CableJunctionType.SimpleJunction,
            _ => CableJunctionType.None
        };
    }
}

[Serializable]
public class PointOverride
{
    public int index;
    public CableJunctionType junctionType;
}

public class CreationCableMeshGenerator : AbstractCableMeshGenerator
{

    public bool upsideDown = false;
    public float width = 1f;
    public float height = 1f;

    public CableJunctionType startJunction = CableJunctionType.Default;

    [SerializeField]
    public List<PointOverride> pointOverrides = new List<PointOverride>();

    // This can be optimized if it becomes a performance issue
    // we expect size of pointOverrides to be negligible in most cases
    // private CableJunctionType getPointOverride(int index){
    //     foreach (var override in pointOverrides){
    //         if(override.index == index){
    //             return override.junctionType;
    //         }
    //     }
    //     return null;
    // }

    override public void GenerateMesh(List<Cable.CablePoint> points, bool active, CableExtensionType extensionType)
    {
        Mesh mesh = new Mesh();


        if (points.Count < 2)
        {
            Debug.LogError("Not enough points to generate cable mesh. At least 2 points are required.");
            GetComponent<MeshFilter>().mesh = null;
            return;
        }


        Geometry geometry = new Geometry();

        GenerateConcavePlaneChangeJunctionMesh(points, active, geometry);
        GenerateConvexPlaneChangeJunctionMesh(points, active, geometry);
        GenerateSamePlaneSimpleJunctionMesh(points, active, geometry);

        CableJunctionType junction = startJunction;
        if (junction == CableJunctionType.Default){
            junction = startJunction.FromCableExtensionType(extensionType);
        }
        GenerateStartJunctionMesh(points, active, junction, geometry);

        // GenerateLastJunctionMesh(points, geometry);

        foreach (var vert in geometry.Vertices)
        {
            geometry.AddVertexUV(new Vector2(0f, 0f));
        }   
        GenerateSegmentMesh(points, active, geometry);

        geometry.IncrementMeshIndex();


        // Debug.Log($"Generated {geometry.Vertices.Count} vertices and {geometry.Triangles.Values.Sum(list => list.Count)} triangles in {geometry.LastMeshIndex} meshes for cable '{name}'.");
        mesh.SetVertices(geometry.Vertices);
        mesh.SetUVs(0, geometry.UVs);

        foreach (var triangle in geometry.Triangles)
        {
            mesh.SetTriangles(triangle.Value, triangle.Key);
        }

        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void GenerateSamePlaneSimpleJunctionMesh(List<Cable.CablePoint> points, bool active, Geometry geometry)
    {
        if (points.Count < 2)
        {
            Debug.LogError("Not enough points to generate same plane simple junction mesh. At least 2 points are required.");
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            if (points[i - 1].normal == points[i].normal)
            {
                Vector3 forward = (points[i].position - points[i - 1].position).normalized;
                int vertexOffset = geometry.Vertices.Count;

                foreach (var (vertex, uv) in GetSamePlaneSimpleJunctionVertices(points[i].position, points[i].normal, forward))
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

    private void GenerateStartJunctionMesh(List<Cable.CablePoint> points, bool active, CableJunctionType type, Geometry geometry)
    {
        if(type == CableJunctionType.None) {
            return;
        }

        if(points.Count < 2)
        {
            Debug.LogError("Not enough points to generate logical junction mesh. At least 2 points are required.");
            return;
        }

        Vector3 forward = (points[1].position - points[0].position).normalized;
        
        int vertexOffset = geometry.Vertices.Count;

        foreach (var (vertex, uv) in GetSamePlaneSimpleJunctionVertices(points[0].position, points[0].normal, forward))
        {
            geometry.AddVertex(vertex);
        }

        foreach (var triangle in GetSamePlaneSimpleJunctionTriangles())
        {
            geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
        }

    }

    private void GenerateConcavePlaneChangeJunctionMesh(List<Cable.CablePoint> points, bool active, Geometry geometry)
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

    private void GenerateConvexPlaneChangeJunctionMesh(List<Cable.CablePoint> points, bool active, Geometry geometry)
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

    private void GenerateSegmentMesh(List<Cable.CablePoint> points, bool active, Geometry geometry)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {

            Vector3 forward = (points[i + 1].position - points[i].position);
            
            int vertexOffset = geometry.Vertices.Count;

            float activeUvOffsetX = active ? 0.135f : 0.0f;

            foreach (var (vertex, uvs) in GetSegmentCrosscutVertices(points[i].position, points[i].normal, forward.normalized, new Vector2(activeUvOffsetX, 0.0f)))
            {
                geometry.AddVertex(vertex);
                geometry.AddVertexUV(uvs);
            }

            float segmentLength = forward.magnitude;
            // take the previous point's normal as the "up" vector, so we don't get twisting
            foreach (var (vertex, uvs) in GetSegmentCrosscutVertices(points[i + 1].position, points[i].normal, forward.normalized, new Vector2(activeUvOffsetX, segmentLength)))
            {
                geometry.AddVertex(vertex);
                geometry.AddVertexUV(uvs);
            }
            foreach (var triangle in GetSegmentTriangles())
            {
                geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
            }
            
        }
    }

    private List<(Vector3, Vector2)> GetSegmentCrosscutVertices(Vector3 point, Vector3 normal, Vector3 forward, Vector2 uvOffset)
    {
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var vertices = new List<(Vector3, Vector2)>
        {
            // see concept blend file for the source of magic numbers
            
            // first UV offest is width - we need to shift UVs if cable is active
            // second UV offset is length of the segment

            // 0, 6
            (point + 0.052868f * width * right, new Vector2(0f, 0f) + uvOffset * new Vector2(0, 1)),
            // 1, 7
            (point + 0.031255f * width * right + 0.027186f * height * localUp, new Vector2(0.034764f, 0f) + uvOffset * new Vector2(0, 1)),
            // 2, 8
            (point + 0.031255f * width * right + 0.027186f * height * localUp, new Vector2(0.034764f, 0f) + uvOffset * new Vector2(1, 1)),
            // 3, 9
            (point + 0.031255f * width * -right + 0.027186f * height * localUp, new Vector2(0.097269f, 0f) + uvOffset * new Vector2(1, 1)),
            // 4, 10
            (point + 0.031255f * width * -right + 0.027186f * height * localUp, new Vector2(0.097269f, 0f) + uvOffset * new Vector2(0, 1)),
            // 5, 11
            (point + 0.052868f * width * -right, new Vector2(0.131997f, 0f) + uvOffset * new Vector2(0, 1)),
        };

        return vertices;
    }

    private List<Vector3Int> GetSegmentTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 6, 1),
            new Vector3Int(1, 6, 7),

            new Vector3Int(2, 9, 3),
            new Vector3Int(2, 8, 9),


            new Vector3Int(4, 11, 5),
            new Vector3Int(4, 10, 11)

            // new Vector3Int(1, 7, 2),
            // new Vector3Int(2, 7, 9),
            // new Vector3Int(2, 9, 5),
            // new Vector3Int(5, 9, 11),
        };
    }

    # region Same Plane Simple Junction

    private List<(Vector3, Vector2)> GetSamePlaneSimpleJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward)
    {
        forward = forward.normalized;
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        var vertices = new List<(Vector3, Vector2)>
        {
            // see concept blend file for the source of magic numbers
            //bottom
            (
                point + 0.071789f * width * right + 0.071789f * forward * width,
                new Vector2(0f, 1f)
            ),
            (
                point + 0.071789f * width * right + 0.071789f * -forward * width, 
                new Vector2(0f, 0f)
            ),
            (
                point + 0.071789f * -width * right + 0.071789f * -forward * width, 
                new Vector2(0.131997f, 0f)
            ),
            (
                point + 0.071789f * -width * right + 0.071789f * forward * width, 
                new Vector2(0.131997f, 1f)
            ),
            //top
            (
                point + 0.041164f * width * right + 0.041164f * forward * width + 0.038168f * height * localUp, 
                new Vector2(0.034764f, 1f)
            ),
            (
                point + 0.041164f * width * right + 0.041164f * -forward * width + 0.038168f * height * localUp, 
                new Vector2(0.034764f, 0f)
            ),
            (   
                point + 0.041164f * -width * right + 0.041164f * -forward * width + 0.038168f * height * localUp, 
                new Vector2(0.097269f, 0f)
            ),
            (   
                point + 0.041164f * -width * right + 0.041164f * forward * width + 0.038168f * height * localUp, 
                new Vector2(0.097269f, 1f)
            )
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

        // forward = -forward

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
