using System.Linq;
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
        Undo.undoRedoPerformed += OnUndoRedo;

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("No active SceneView found. Please open a SceneView to use the Cable Drawer Tool.");
            return;
        }

        Overlay overlay;
        bool match = sceneView.TryGetOverlay("Cable Tool Overlay", out overlay);
        if (!match)
        {
            Debug.LogWarning("Cable Tool Overlay not found. Make sure it is registered correctly.");
            return;
        }
        overlay.displayed = true;
    }

    public override void OnWillBeDeactivated()
    {
        base.OnWillBeDeactivated();
        Undo.undoRedoPerformed -= OnUndoRedo;
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("No active SceneView found. Cannot hide Cable Tool Overlay.");
            return;
        }

        Overlay overlay;
        bool match = sceneView.TryGetOverlay("Cable Tool Overlay", out overlay);
        if (!match)
        {
            Debug.LogWarning("Cable Tool Overlay not found. Cannot hide it.");
            return;
        }

        overlay.displayed = false;
    }

    public override void OnToolGUI(EditorWindow window)
    {
        cable = Selection.activeGameObject?.GetComponent<Cable>();

        if (cable == null)
        {
            Debug.LogWarning("No Cable component selected. Please select a GameObject with a Cable component.");
            return;
        }

        DrawCableHandles();
        DrawAddPointPreview();
        HandleAddPoint();


        SceneView.RepaintAll();
    }


    /// Draws position handles for each cable point in the Scene view.
    private void DrawCableHandles()
    {
        for (int i = 0; i < cable.Points.Count; i++)
        {


            EditorGUI.BeginChangeCheck();
            Vector3 worldPoint = cable.transform.TransformPoint(cable.Points[i].position);
            Vector3 newWorldPoint = Handles.FreeMoveHandle(
                worldPoint,
                HandleUtility.GetHandleSize(worldPoint) * 0.2f,
                Vector3.zero,
                Handles.SphereHandleCap
            );
            Handles.Label(worldPoint + Vector3.up * 0.1f, $"#{i}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.black } });
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cable, "Move Cable Point");
                cable.Points[i] = new Cable.CablePoint { position = cable.transform.InverseTransformPoint(newWorldPoint), normal = cable.Points[i].normal };
                cable.GenerateMesh();
                EditorUtility.SetDirty(cable);
            }
        }
    }

    private void DrawAddPointPreview()
    {
        Event e = Event.current;
        if (e.type != EventType.Layout && e.type != EventType.Repaint && e.type != EventType.MouseMove)
        {
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
        {
            return;
        }

        using (new Handles.DrawingScope(Color.yellow))
        {
            if (cable.Points.Count > 0)
            {
                Handles.DrawLine(cable.transform.TransformPoint(cable.Points.Last().position), hit.point);
            }
            Handles.DrawWireDisc(hit.point, hit.normal, 0.1f);
        }

        // Debug.Log($"Hit Point: {hit.point}, Hit Normal: {hit.normal}");

        var adjustedPoint = cable.transform.TransformPoint(
            ComputeNextPossiblePoint(hit.point, hit.normal, Event.current.shift)
        );
        using (new Handles.DrawingScope(Color.green))
        {
            if (cable.Points.Count > 0)
            {
                Handles.DrawLine(cable.transform.TransformPoint(cable.Points.Last().position), adjustedPoint);
            }
            Handles.DrawWireDisc(adjustedPoint, hit.normal, 0.1f);
        }
    }

    private void HandleAddPoint()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseUp && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.LogWarning("No valid point hit to add to the cable.");
                e.Use();
                return;
            }

            Undo.RecordObject(cable, "Add Cable Point");
            cable.Points.Add(new Cable.CablePoint
            {
                position = ComputeNextPossiblePoint(hit.point, hit.normal, Event.current.shift),
                normal = hit.normal
            });
            cable.GenerateMesh();
            EditorUtility.SetDirty(cable);
            e.Use();
        }
    }

    private Vector3 ComputeNextPossiblePoint(Vector3 targetPoint, Vector3 targetNormal, bool snapNormal)
    {

        if (cable.Points.Count > 0)
        {
            // snap to one of the axis, where distance is the largest
            Vector3 lastPoint = cable.transform.TransformPoint(cable.Points.Last().position);
            Vector3 distance = lastPoint - targetPoint;
            // Debug.Log($"Last Point: {lastPoint}, Target Point: {targetPoint}, Distance: {distance}");
            int snapDirectionAxis = GetLargestComponentIndex(distance);
            // Debug.Log($"Snap Direction Axis: {snapDirectionAxis} - ({distance[snapDirectionAxis]})");
            Vector3 snappedPoint = lastPoint;
            snappedPoint[snapDirectionAxis] = targetPoint[snapDirectionAxis];
            if (snapNormal)
            {
                // snap the normal to the axis where the distance is largest
                int snapNormalAxis = GetLargestComponentIndex(targetNormal);
                snappedPoint[snapNormalAxis] = targetPoint[snapNormalAxis];
            }

            return cable.transform.InverseTransformPoint(snappedPoint);
        }
        return cable.transform.InverseTransformPoint(targetPoint);
    }

    private int GetLargestComponentIndex(Vector3 v)
    {
        float absX = Mathf.Abs(v.x);
        float absY = Mathf.Abs(v.y);
        float absZ = Mathf.Abs(v.z);


        if (absX >= absY && absX >= absZ)
        {
            return 0; // X
        }
        else if (absY >= absX && absY >= absZ)
        {
            return 1; // Y
        }
        else
        {
            return 2; // Z
        }
    }
    private void OnUndoRedo()
    {
        Debug.Log("Undo/Redo performed, regenerating cable mesh.");
        if (cable != null)
        {
            cable.GenerateMesh();
            EditorUtility.SetDirty(cable);
        }
    }
}