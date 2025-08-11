using System;
using System.Collections.Generic;
using UnityEngine;

public class BasicCableMeshGenerator : AbstractCableMeshGenerator
{

    public float radius = 0.05f;
    public int radialSegments = 8;

    private GameObject reminderTextObj;


    override public void GenerateMesh(List<Cable.CablePoint> points, bool active, CableExtensionType extensionType)
    {
        Mesh mesh = new Mesh();

        if (reminderTextObj != null)
        {
            DestroyImmediate(reminderTextObj);
            reminderTextObj = null;
        }
        if (points.Count < 2)
        {
            Debug.LogWarning("Not enough points to generate cable mesh. At least 2 points are required.");


            reminderTextObj = new GameObject("CableReminderText");
            reminderTextObj.transform.SetParent(transform, false);
            reminderTextObj.transform.localPosition = Vector3.zero;
            var textMesh = reminderTextObj.AddComponent<TextMesh>();
            textMesh.text = $"Cable '{name}' is broken :<";
            textMesh.characterSize = 0.3f;
            textMesh.color = Color.magenta;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            GetComponent<MeshFilter>().mesh = null;
            return;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 forward = i < points.Count - 1 ? points[i + 1].position - points[i].position : points[i].position - points[i - 1].position;
            Quaternion rotation = Quaternion.LookRotation(forward == Vector3.zero ? Vector3.forward : forward);
            for (int j = 0; j < radialSegments; j++)
            {
                float angle = j * Mathf.PI * 2f / radialSegments;
                Vector3 circle = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                verts.Add(points[i].position + rotation * circle);
                uvs.Add(new Vector2((float)j / radialSegments, (float)i / (points.Count - 1)));
            }
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int current = i * radialSegments + j;
                int next = current + radialSegments;
                int nextJ = (j + 1) % radialSegments;

                int currentNext = i * radialSegments + nextJ;
                int nextNext = currentNext + radialSegments;

                tris.Add(current);
                tris.Add(next);
                tris.Add(nextNext);

                tris.Add(current);
                tris.Add(nextNext);
                tris.Add(currentNext);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}
