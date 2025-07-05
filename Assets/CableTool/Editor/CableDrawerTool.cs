using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;

[EditorTool("Cable Drawer")]
public class CableDrawerTool : EditorTool
{
    Cable cable;

    public override void OnActivated()
    {
        base.OnActivated();
        cable = Selection.activeGameObject?.GetComponent<Cable>();
        if (!cable)
        {
            Debug.LogWarning("Select a GameObject with a Cable component first.");
            return;
        }

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Overlay overlay;
            bool match = sceneView.TryGetOverlay("Cable Tool Overlay", out overlay);
            if (match)
            {
                Debug.Log("Cable Tool Overlay found, displaying it.");
                overlay.displayed = true;
            }
            else
            {
                Debug.LogWarning("Cable Tool Overlay not found. Make sure it is registered correctly.");
            }
        }
        else
        {
            Debug.LogWarning("No active SceneView found. Please open a SceneView to use the Cable Drawer Tool.");
        }
    }

    public override void OnWillBeDeactivated()
    {
        base.OnWillBeDeactivated();
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Overlay overlay;
            bool match = sceneView.TryGetOverlay("Cable Tool Overlay", out overlay);
            if (match)
            {
                Debug.Log("Hiding Cable Tool Overlay.");
                overlay.displayed = false;
            }
        }
    }

    public override void OnToolGUI(EditorWindow window)
    {
        if (!cable)
        {
            Debug.LogWarning("No Cable component selected. Please select a GameObject with a Cable component.");
            return;
        }

        // Draw handles for adjusting points
        for (int i = 0; i < cable.points.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 worldPoint = cable.transform.TransformPoint(cable.points[i]);
            Vector3 newWorldPoint = Handles.PositionHandle(worldPoint, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cable, "Move Cable Point");
                cable.points[i] = cable.transform.InverseTransformPoint(newWorldPoint);
                cable.GenerateMesh();
            }
        }

        // Handles.BeginGUI();
        // GUILayout.BeginArea(new Rect(10, 10, 180, 40), EditorStyles.helpBox);
        // if (GUILayout.Button("Remove Last Point") && cable.points.Count > 0)
        // {
        //     Undo.RecordObject(cable, "Remove Last Cable Point");
        //     cable.points.RemoveAt(cable.points.Count - 1);
        //     cable.GenerateMesh();
        // }
        // GUILayout.EndArea();
        // Handles.EndGUI();
    }
}