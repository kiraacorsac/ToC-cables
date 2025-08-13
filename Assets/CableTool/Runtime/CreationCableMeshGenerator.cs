using System;
using System.Collections.Generic;
using UnityEditor.Timeline.Actions;
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

    public float glow_height = 0.25f;

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


        CableJunctionType startJunctionType = startJunction;
        if (startJunctionType == CableJunctionType.Default)
        {
            startJunctionType = startJunction.FromCableExtensionType(extensionType);
        }

        // GenerateLastJunctionMesh(points, geometry);

        // foreach (var vert in geometry.Vertices)
        // {
        //     geometry.AddVertexUV(new Vector2(0f, 0f));
        // }

        GenerateConcavePlaneChangeJunctionMesh(points, active, geometry);
        GenerateConvexPlaneChangeJunctionMesh(points, active, geometry);
        GenerateStartJunctionMesh(points, active, startJunctionType, geometry);
        GenerateSamePlaneSimpleJunctionMesh(points, active, geometry);
        GenerateSegmentMesh(points, active, geometry);

        geometry.IncrementMeshIndex();


        // Debug.Log($"Generated {geometry.Vertices.Count} vertices and {geometry.Triangles.Values.Sum(list => list.Count)} triangles in {geometry.LastMeshIndex} meshes for cable '{name}'.");
        mesh.SetVertices(geometry.Vertices);
        mesh.SetUVs(0, geometry.UVs.ConvertAll(uv => new Vector2(uv.x * 2f, uv.y))); // Texture is twice as tall as it is wide

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
                var point = points[i];

                GenerateSamePlaneJunctionMeshForPoint(active, forward, vertexOffset, point, CableJunctionType.SimpleJunction, geometry);
            }
        }
    }

    private void GenerateSamePlaneJunctionMeshForPoint(bool active, Vector3 forward, int vertexOffset, Cable.CablePoint point, CableJunctionType type, Geometry geometry)
    {
        UvTransformation uvTransform = new()
        {
            Scale = new Vector2(0.20f, 0.20f),
            Translate = new Vector2(0.27f, 0f)
        };
        UvTransformation topUVTransform = new();

        switch (type)
        {
            case CableJunctionType.SimpleJunction:
                topUVTransform.Translate += new Vector2(0f, 0.20f);
                break;
            case CableJunctionType.OrJunction:
                topUVTransform.Translate += new Vector2(0f, 0.33f);
                break;
            case CableJunctionType.AndJunction:
                topUVTransform.Translate += new Vector2(0f, 0.46f);
                break;
            default:
                Debug.LogError($"Unknown junction type {type} for same plane simple junction mesh generation.");
                return;
        }

        topUVTransform.Translate += new Vector2(active ? -0.06f : 0.06f, 0f);

        foreach (var (vertex, uv) in GetSamePlaneSimpleJunctionVertices(point.position, point.normal, forward, uvTransform, topUVTransform))
        {
            geometry.AddVertex(vertex);
            geometry.AddVertexUV(uv);
        }

        foreach (var triangle in GetSamePlaneSimpleJunctionTriangles())
        {
            geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
        }
    }

    private void GenerateStartJunctionMesh(List<Cable.CablePoint> points, bool active, CableJunctionType type, Geometry geometry)
    {
        if (type == CableJunctionType.None)
        {
            return;
        }

        if (points.Count < 2)
        {
            Debug.LogError("Not enough points to generate logical junction mesh. At least 2 points are required.");
            return;
        }

        Vector3 forward = (points[1].position - points[0].position).normalized;

        int vertexOffset = geometry.Vertices.Count;

        GenerateSamePlaneJunctionMeshForPoint(active, forward, vertexOffset, points[0], type, geometry);
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
                foreach (var (vertex, uv) in GetConcavePlaneChangeJunctionVertices(points[i].position, points[i - 1].normal, forward))
                {
                    geometry.AddVertex(vertex);
                    geometry.AddVertexUV(uv);
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

                UvTransformation activeUvTransform = new()
                {
                    Translate = active ? new Vector2(0.12f, 0f) : new Vector2(0.0f, 0.0f)
                };

                // take previous point's normal as up
                foreach (var (vertex, uv) in GetConvexPlaneChangeJunctionVertices(points[i].position, points[i - 1].normal, forward, activeUvTransform))
                {
                    geometry.AddVertex(vertex);
                    geometry.AddVertexUV(uv);
                }

                foreach (var triangle in GetConvexPlaneChangeJunctionTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }

                if (active)
                {
                    vertexOffset = geometry.Vertices.Count;

                    foreach (var (vertex, uv) in GetConvexPlaneChangeJunctionGlowVertices(points[i].position, points[i - 1].normal, forward))
                    {
                        geometry.AddVertex(vertex);
                        geometry.AddVertexUV(uv);
                    }

                    foreach (var triangle in GetConvexPlaneChangeJunctionGlowTriangles())
                    {
                        geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                    }
                }
            }
        }
    }

    #region Segment 
    private void GenerateSegmentMesh(List<Cable.CablePoint> points, bool active, Geometry geometry)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {

            Vector3 forward = points[i + 1].position - points[i].position;

            int vertexOffset = geometry.Vertices.Count;


            UvTransformation activeUvTransform = new()
            {
                Translate = new Vector2(active ? 0.12f : 0.0f, 0f)
            };

            foreach (var (vertex, uvs) in GetSegmentCrosscutVertices(points[i].position, points[i].normal, forward.normalized, activeUvTransform))
            {
                geometry.AddVertex(vertex);
                geometry.AddVertexUV(uvs);
            }

            // take the previous point's normal as the "up" vector, so we don't get twisting

            UvTransformation activeLengthUvTransform = new()
            {
                Translate = new Vector2(active ? 0.12f : 0.0f, forward.magnitude)
            };


            foreach (var (vertex, uvs) in GetSegmentCrosscutVertices(points[i + 1].position, points[i].normal, forward.normalized, activeLengthUvTransform))
            {
                geometry.AddVertex(vertex);
                geometry.AddVertexUV(uvs);
            }
            foreach (var triangle in GetSegmentTriangles())
            {
                geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
            }

            if (active)
            {
                vertexOffset = geometry.Vertices.Count;

                foreach (var (vertex, uvs) in GetActiveSegmentGlowVertices(points[i].position, points[i].normal, forward.normalized, activeUvTransform))
                {
                    geometry.AddVertex(vertex);
                    geometry.AddVertexUV(uvs);
                }

                foreach (var (vertex, uvs) in GetActiveSegmentGlowVertices(points[i + 1].position, points[i].normal, forward.normalized, activeLengthUvTransform))
                {
                    geometry.AddVertex(vertex);
                    geometry.AddVertexUV(uvs);
                }


                foreach (var triangle in GetActiveSegmentGlowTriangles())
                {
                    geometry.AddTriangle(triangle.x, triangle.y, triangle.z, vertexOffset);
                }
            }

        }
    }

    private List<(Vector3, Vector2)> GetSegmentCrosscutVertices(Vector3 point, Vector3 normal, Vector3 forward, UvTransformation uvTransform)
    {
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        // we need to shift UVs if cable is active
        UvTransformation activeUvTransform = new(uvTransform)
        {
            Translate = new Vector2(uvTransform.Translate.x, 0)
        };

        // we need to shift UVs for length of the segment
        UvTransformation lengthUvTransform = new(uvTransform)
        {
            Translate = new Vector2(0, uvTransform.Translate.y)
        };

        var vertices = new List<(Vector3, Vector2)>
        {
            // see concept blend file for the source of magic numbers
            // 0, 6
            (
                point + 0.052868f * width * right,
                lengthUvTransform * new Vector2(0f, 0f)
            ),
            // 1, 7
            (
                point + 0.031255f * width * right + 0.027186f * height * localUp,
                lengthUvTransform * new Vector2(0.034764f, 0f)
            ),
            // 2, 8
            (
                point + 0.031255f * width * right + 0.027186f * height * localUp,
                activeUvTransform * lengthUvTransform * new Vector2(0.034764f, 0f)
            ),
            // 3, 9
            (
                point + 0.031255f * width * -right + 0.027186f * height * localUp,
                activeUvTransform * lengthUvTransform * new Vector2(0.097269f, 0f)
            ),
            // 4, 10
            (
                point + 0.031255f * width * -right + 0.027186f * height * localUp,
                lengthUvTransform * new Vector2(0.097269f, 0f)
            ),
            // 5, 11
            (
                point + 0.052868f * width * -right,
                lengthUvTransform * new Vector2(0.131997f, 0f)
            ),
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


    private List<(Vector3, Vector2)> GetActiveSegmentGlowVertices(Vector3 point, Vector3 normal, Vector3 forward, UvTransformation uvTransformation)
    {
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        UvTransformation glowSizeTransformation = new UvTransformation()
        {
            Translate = new Vector2(-0.01f, 0f),
        };

        var vertices = new List<(Vector3, Vector2)>
        {
            // on top of the segment crosscut
            (
                point + 0.031255f * width * right + 0.027186f * height * localUp,
                uvTransformation * new Vector2(0.034764f, 0f)
            ),
            (
                point + 0.031255f * width * right + 0.027186f * (height + glow_height) * localUp,
                glowSizeTransformation * uvTransformation * new Vector2(0.034764f, 0f)
            ),
            (
                point + 0.031255f * width * -right + 0.027186f * height * localUp,
                uvTransformation * new Vector2(0.034764f, 0f)
            ),
            (
                point + 0.031255f * width * -right + 0.027186f * (height + glow_height) * localUp,
                glowSizeTransformation * uvTransformation * new Vector2(0.034764f, 0f)
            ),
        };

        return vertices;
    }

    private List<Vector3Int> GetActiveSegmentGlowTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 5, 4),
            new Vector3Int(0, 1, 5),
            new Vector3Int(2, 6, 7),
            new Vector3Int(2, 7, 3),
        };
    }

    #endregion

    #region Same Plane Simple Junction

    private List<(Vector3, Vector2)> GetSamePlaneSimpleJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward, UvTransformation uvTransform, UvTransformation topUVTransform)
    {
        forward = forward.normalized;
        Vector3 up = normal.normalized;
        Vector3 right = Vector3.Cross(forward, up).normalized;
        Vector3 localUp = -Vector3.Cross(forward, right).normalized;
        if (upsideDown)
        {
            localUp = -localUp;
        }

        // see concept blend file for the source of magic numbers
        const float bottomScalar = 0.071789f;
        const float topScalar = 0.041164f;
        const float heightScalar = 0.038168f;
        var vertices = new List<(Vector3, Vector2)>
        {
            //bottom
            // 1.
            (
                point + bottomScalar * width * right + bottomScalar * forward * width,
                uvTransform * new Vector2(0.898f, 0f)
            ),
            (
                point + bottomScalar * width * right + bottomScalar * -forward * width,
                uvTransform * new Vector2(0.102f, 0f)
            ),
            (
                point + topScalar * width * right + topScalar * -forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.272f, 0.272f)
            ),
            (
                point + topScalar * width * right + topScalar * forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.728f, 0.272f)
            ),

            // 2.
            (
                point + bottomScalar * -width * right + bottomScalar * forward * width,
                uvTransform * new Vector2(1f, 0.898f)
            ),
            (
                point + bottomScalar * width * right + bottomScalar * forward * width,
                uvTransform * new Vector2(1f, 0.102f)
            ),
            (
                point + topScalar * width * right + topScalar * forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.728f, 0.272f)
            ),
            (
                point + topScalar * -width * right + topScalar * forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.728f, 0.728f)
            ),

            // 3.
            (
                point + bottomScalar * -width * right + bottomScalar * -forward * width,
                uvTransform * new Vector2(0.102f, 1f)
            ),
            (
                point + bottomScalar * -width * right + bottomScalar * forward * width,
                uvTransform * new Vector2(0.898f, 1f)
            ),
            (
                point + topScalar * -width * right + topScalar * forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.728f, 0.728f)
            ),
            (
                point + topScalar * -width * right + topScalar * -forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.272f, 0.728f)
            ),

            // 4.
            (
                point + bottomScalar * width * right + bottomScalar * -forward * width,
                uvTransform * new Vector2(0f, 0.102f)
            ),
            (
                point + bottomScalar * -width * right + bottomScalar * -forward * width,
                uvTransform * new Vector2(0f, 0.898f)
            ),
            (
                point + topScalar * -width * right + topScalar * -forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.272f, 0.728f)
            ),
            (
                point + topScalar * width * right + topScalar * -forward * width + heightScalar * height * localUp,
                uvTransform * new Vector2(0.272f, 0.272f)
            ),



            //top
            (
                point + topScalar * width * right + topScalar * -forward * width + heightScalar * height * localUp,
                topUVTransform * uvTransform * new Vector2(0.272f, 0.272f)
            ),
            (
                point + topScalar * width * right + topScalar * forward * width + heightScalar * height * localUp,
                topUVTransform * uvTransform * new Vector2(0.728f, 0.272f)
            ),
            (
                point + topScalar * -width * right + topScalar * forward * width + heightScalar * height * localUp,
                topUVTransform * uvTransform * new Vector2(0.728f, 0.728f)
            ),
            (
                point + topScalar * -width * right + topScalar * -forward * width + heightScalar * height * localUp,
                topUVTransform * uvTransform * new Vector2(0.272f, 0.728f)
            ),
        };

        return vertices;
    }

    private List<Vector3Int> GetSamePlaneSimpleJunctionTriangles()
    {
        return new List<Vector3Int>
        {
            // bottom
            new Vector3Int(2, 1, 0),
            new Vector3Int(3, 2, 0),
            new Vector3Int(6, 5, 4),
            new Vector3Int(7, 6, 4),
            new Vector3Int(10, 9, 8),
            new Vector3Int(11, 10, 8),
            new Vector3Int(14, 13, 12),
            new Vector3Int(15, 14, 12),
            // top
            new Vector3Int(16, 17, 18),
            new Vector3Int(16, 18, 19),
        };
    }
    #endregion

    #region Concave Plane Change Junction

    private List<(Vector3, Vector2)> GetConcavePlaneChangeJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward)
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

        var vertices = new List<(Vector3, Vector2)>
        {
            // see concept blend file for the source of magic numbers
            //front 0
            (
                point + 0.056414f * width * right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.370f, 0.669f)
            ),
            (
                point + 0.033691f * width * right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.395f, 0.637f)
            ),
            (
                point + 0.033691f * width * -right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.467f, 0.637f)
            ),
            (
                point + 0.056414f * width * -right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.491f, 0.669f)
            ),

            //back 4
            (
                point + 0.056414f * width * right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.370f, 0.669f)
            ),
            (
                point + 0.033691f * width * right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.395f, 0.637f)
            ),
            (
                point + 0.033691f * width * -right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.467f, 0.637f)
            ),
            (
                point + 0.056414f * width * -right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.491f, 0.669f)
            ),

            //middle 8
            (
                point + 0.033691f * width * right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.360f, 0.724f)
            ),
            (
                point + 0.033691f * width * -right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.433f, 0.724f)
            ),
            (
                point + 0.033691f * width * -right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.433f, 0.718f)
            ),
            (
                point + 0.033691f * width * right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.360f, 0.718f)
            ),

            // sides - right 12

            (
                point + 0.056414f * width * right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.327f, 0.696f)
            ),
            (
                point + 0.056414f * width * right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.327f, 0.747f)
            ),
            (
                point + 0.033691f * width * right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.360f, 0.724f)
            ),
            (
                point + 0.033691f * width * right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.360f, 0.718f)
            ),
            // sides - left 16

            (
                point + 0.033691f * width * -right + -0.033217f * forward + 0.029491f * height * localUp,
                new Vector2(0.433f, 0.718f)
            ),
            (
                point + 0.033691f * width * -right + -0.029549f * forward + 0.033207f * height * localUp,
                new Vector2(0.433f, 0.724f)
            ),
            (
                point + 0.056414f * width * -right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.466f, 0.747f)
            ),
            (
                point + 0.056414f * width * -right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.466f, 0.696f)
            ),

            // into ground - right 
            (
                point + 0.056414f * width * right + 0.000000f * forward + 0.000000f * height * localUp,
                new Vector2(0.327f, 0.696f)
            ),
            (
                point + 0.056414f * width * right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.327f, 0.747f)
            ),
            (
                point + 0.056414f * width * right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.302f, 0.721f)
            ),

            // into ground - left
            (
                point + 0.056414f * width * -right + 0.000000f * forward + 0.033207f * height * localUp,
                new Vector2(0.466f, 0.747f)
            ),
            (
                point + 0.056414f * width * -right + 0.000000f * forward + 0.000000f * height * localUp,
                new Vector2(0.491f, 0.721f)
            ),
            (
                point + 0.056414f * width * -right + -0.033217f * forward + 0.000000f * height * localUp,
                new Vector2(0.466f, 0.696f)
            ),


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
            // back
            new Vector3Int(5, 4, 7),
            new Vector3Int(5, 7, 6),

            // middle
            new Vector3Int(8, 9, 10),
            new Vector3Int(8, 10, 11),

            // left side
            new Vector3Int(12, 13, 14),
            new Vector3Int(12, 14, 15),

            // right side
            new Vector3Int(16, 17, 18),
            new Vector3Int(16, 18, 19),

            // into ground - right
            new Vector3Int(20, 21, 22),

            // into ground - left
            new Vector3Int(23, 24, 25),

            // // middle front
            // new Vector3Int(0, 5, 1),
            // new Vector3Int(0, 4, 5),
            // new Vector3Int(1, 5, 6),
            // new Vector3Int(1, 6, 2),
            // new Vector3Int(2, 6, 7),
            // new Vector3Int(2, 7, 3),

            // // middle back
            // new Vector3Int(4, 8, 9),
            // new Vector3Int(4, 9, 5),
            // new Vector3Int(5, 9, 10),
            // new Vector3Int(5, 10, 6),
            // new Vector3Int(6, 10, 11),
            // new Vector3Int(6, 11, 7),

        };
    }

    #endregion

    #region Convex Plane Change Junction

    private List<(Vector3, Vector2)> GetConvexPlaneChangeJunctionVertices(Vector3 point, Vector3 normal, Vector3 forward, UvTransformation activeUvTransform)
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
            //front
            // point + 0.052868f * width * right + -0.004057f * forward  + 0.0f * height * localUp,
            // point + 0.031255f * width * right + -0.004057f  * forward + -0.027186f* height * localUp,
            // point + 0.031255f * width * -right + -0.004057f * forward + -0.027186f * height * localUp,
            // point + 0.052868f * width * -right + -0.004057f * forward + 0.0f * height * localUp,

            //corners
            (
                point + 0.052868f * width * right + 0.0f * forward + 0.0f * height * localUp,
                new Vector2(0.262f, 0.650f)
            ),
            (
                point + 0.031255f * width * right + 0.0f * forward + 0.027186f * height * localUp,
                new Vector2(0.298f, 0.674f)
            ),
            (
                point + 0.031255f * width * -right + 0.0f * forward + 0.027186f * height * localUp,
                new Vector2(0.327f, 0.674f)
            ),
            (
                point + 0.052868f * width * -right + 0.0f * forward + 0.0f * height * localUp,
                new Vector2(0.362f, 0.650f)
            ),
            (
                point + 0.031255f * width * right + 0.027186f * forward + 0.0f * height * localUp,
                new Vector2(0.298f, 0.627f)
            ),
            (
                point + 0.031255f * width * -right + 0.027186f * forward + 0.0f * height * localUp,
                new Vector2(0.327f, 0.627f)
            ),

            // start the 'middle' UVs in the middle, so they are not obviously repeating 

            //middle
            (
                point + 0.031255f * width * right + 0.0f * forward + 0.027186f * height * localUp,
                activeUvTransform * new Vector2(0.034764f, 0.5f)
            ),
            (
                point + 0.031255f * width * -right + 0.0f * forward + 0.027186f * height * localUp,
                activeUvTransform * new Vector2(0.097269f, 0.5f)
            ),
            (
                point + 0.031255f * width * right + 0.027186f * forward + 0.0f * height * localUp,
                activeUvTransform * new Vector2(0.034764f, 0.55f)
            ),
            (
                point + 0.031255f * width * -right + 0.027186f * forward + 0.0f * height * localUp,
                activeUvTransform * new Vector2(0.097269f, 0.55f)
            ),
            //top
            // point + 0.052868f * width * right + 0.0f * forward + 0.004057f * height * localUp,
            // point + 0.031255f * width * right + 0.027186f * forward + 0.004057f * height * localUp,
            // point + 0.031255f * width * -right + 0.027186f * forward + 0.004057f * height * localUp,
            // point + 0.052868f * width * -right + 0.0f * forward + 0.004057f * height * localUp
        };
        return vertices;
    }

    private List<Vector3Int> GetConvexPlaneChangeJunctionTriangles()
    {
        return new List<Vector3Int>
        {
            // front
            // new Vector3Int(1,0,4),
            // new Vector3Int(1,4,5),
            // new Vector3Int(2,1,5),
            // new Vector3Int(2,5,6),
            // new Vector3Int(3,2,6),
            // new Vector3Int(3,6,7),
            // corners
            new Vector3Int(0,4,1),
            new Vector3Int(2,5,3),

            // middle
            new Vector3Int(6,8,9),
            new Vector3Int(9,7,6),

            // top
            // new Vector3Int(4,10,11),
            // new Vector3Int(4,11,8),
            // new Vector3Int(8,11,9),
            // new Vector3Int(9,11,12),
            // new Vector3Int(9,12,7),
            // new Vector3Int(7,12,13)
        };
    }

    private List<(Vector3, Vector2)> GetConvexPlaneChangeJunctionGlowVertices(Vector3 point, Vector3 normal, Vector3 forward)
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
            (
                point + 0.031255f * width * right + 0.0f * forward + 0.027186f * height * localUp,
                new Vector2(0.155f, 0f)
            ),
            (
                point + 0.031255f * width * right + 0.0f * forward + 0.027186f * (height + glow_height) * localUp,
                new Vector2(0.145f, 0f)
            ),
            (
                point + 0.031255f * width * -right + 0.0f * forward + 0.027186f * height * localUp,
                new Vector2(0.155f, 0f)
            ),
            (
                point + 0.031255f * width * -right + 0.0f * forward + 0.027186f * (height + glow_height) * localUp,
                new Vector2(0.145f, 0f)
            ),
            (
                point + 0.031255f * width * right + 0.027186f * forward + 0.0f * height * localUp,
                new Vector2(0.155f, 0.05f)
            ),
            (
                point + 0.031255f * width * right + (0.027186f + glow_height * 0.027186f) * forward + 0.0f * height * localUp,
                new Vector2(0.145f, 0.05f)
            ),
            (
                point + 0.031255f * width * -right + 0.027186f * forward + 0.0f * height * localUp,
                new Vector2(0.155f, 0.05f)
            ),
            (
                point + 0.031255f * width * -right + (0.027186f + glow_height * 0.027186f) * forward + 0.0f * height * localUp,
                new Vector2(0.145f, 0.05f)
            ),
        };

        return vertices;
    }

    private List<Vector3Int> GetConvexPlaneChangeJunctionGlowTriangles()
    {
        return new List<Vector3Int>
        {
            new Vector3Int(0, 5, 4),
            new Vector3Int(0, 1, 5),
            new Vector3Int(2, 6, 7),
            new Vector3Int(2, 7, 3),
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

}
