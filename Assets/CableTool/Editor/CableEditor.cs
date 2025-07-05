using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Cable))]
public class CableEditor : Editor
{
    private Cable cable;

    private void OnSceneGUI()
    {
        // cable = (Cable)target;

        // for (int i = 0; i < cable.points.Count; i++)
        // {
        //     EditorGUI.BeginChangeCheck();
        //     Vector3 worldPoint = cable.transform.TransformPoint(cable.points[i]);
        //     Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);
        //     bool changed = EditorGUI.EndChangeCheck();
        //     if (changed)
        //     {
        //         Undo.RecordObject(cable, "Move Cable Point");
        //         cable.points[i] = cable.transform.InverseTransformPoint(newWorldPoint);
        //         cable.GenerateMesh();
        //     }
        // }
    }
}
