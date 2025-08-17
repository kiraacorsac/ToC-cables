using System;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using System.Collections.Generic;

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

        DrawCablePointHandles();
        DrawCableSegmentHandles();
        DrawAddPointPreview();
        HandleAddPoint();


        SceneView.RepaintAll();
    }


    private void DrawCablePointHandles()
    {
        for (int i = 1; i < cable.Points.Count; i++)
        {
            Vector3 worldPoint = cable.transform.TransformPoint(cable.Points[i].position);
            Handles.FreeMoveHandle(
                worldPoint,
                HandleUtility.GetHandleSize(worldPoint) * 0.05f,
                Vector3.zero,
                Handles.SphereHandleCap
            );
            Handles.Label(worldPoint + Vector3.up * 0.1f, $"#{i}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.black } });
        }
        // Draw the first point handle separately
        if (cable.Points.Count < 1)
            return;

        Vector3 worldPointFirst = cable.transform.TransformPoint(cable.Points[0].position);
        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPoint = Handles.FreeMoveHandle(
            worldPointFirst,
            HandleUtility.GetHandleSize(worldPointFirst) * 0.3f,
            Vector3.zero,
            Handles.SphereHandleCap
        );
        Handles.Label(worldPointFirst + Vector3.up * 0.1f, "Start", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.black } });

        if (cable.Points.Count < 2)
            return;

        Vector3 worldPointSecond = cable.transform.TransformPoint(cable.Points[1].position);
        Vector3 segmentForward = (worldPointSecond - worldPointFirst).normalized;


        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cable, "Move Cable Point");
            // Only allow movement in the segmentForward direction
            Vector3 offset = newWorldPoint - worldPointFirst;
            offset = Vector3.Project(offset, segmentForward);
            Vector3 constrainedWorldPoint = worldPointFirst + offset;
            cable.Points[0] = new Cable.CablePoint
            {
                position = cable.transform.InverseTransformPoint(constrainedWorldPoint),
                normal = cable.Points[0].normal
            };
            cable.GenerateMesh();
            EditorUtility.SetDirty(cable);
        }
    }

    private void DrawCableSegmentHandles()
    {
        if (cable.Points.Count < 2)
            return;

        List<(Vector3 normal, Vector3 forward, Vector3 side)> segmentInfo = new List<(Vector3, Vector3, Vector3)>();

        for (int i = 0; i < cable.Points.Count - 1; i++)
        {
            var p0 = cable.Points[i];
            var p1 = cable.Points[i + 1];

            Vector3 worldP0 = cable.transform.TransformPoint(p0.position);
            Vector3 worldP1 = cable.transform.TransformPoint(p1.position);

            Vector3 normal = p0.normal;
            Vector3 forward = (worldP1 - worldP0).normalized;
            Vector3 side = Vector3.Cross(normal, forward).normalized;
            segmentInfo.Add((normal, forward, side));
        }

        for (int i = 0; i < cable.Points.Count - 1; i++)
        {
            var p0 = cable.Points[i];
            var p1 = cable.Points[i + 1];

            Vector3 worldP0 = cable.transform.TransformPoint(p0.position);
            Vector3 worldP1 = cable.transform.TransformPoint(p1.position);

            Vector3 normal = segmentInfo[i].normal;
            Vector3 forward = segmentInfo[i].forward;
            Vector3 side = segmentInfo[i].side;
            Vector3 mid = (worldP0 + worldP1) * 0.5f;

            EditorGUI.BeginChangeCheck();
            Vector3 newMid = Handles.Slider2D(
                mid,
                normal,
                forward,
                side,
                HandleUtility.GetHandleSize(mid) * 0.1f,
                Handles.RectangleHandleCap,
                0f
            );

            // Handles.Label(newMid + Vector3.up * 0.1f, $"{side}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = Color.black } });

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cable, "Move Cable Segment");
                Vector3 offset = newMid - mid;
                offset -= Vector3.Project(offset, forward); //only allow movement in the side direction

                Debug.Log($"Moving segment {i} from {mid} to {newMid}, offset: {offset}");

                List<int> indicesToOffset = new();
                for (int prevIndex = i; prevIndex >= 0; prevIndex--)
                {
                    // If we are not checking the last point, we need to check if the it's aligned with the previous segment
                    if (prevIndex > 0)
                    {
                        Vector3 prevSide = segmentInfo[prevIndex].side;
                        if (AbsVector(prevSide) != AbsVector(side))
                        {
                            break;
                        }
                    }
                    indicesToOffset.Add(prevIndex);
                }
                for (int nextIndex = i + 1; nextIndex < cable.Points.Count; nextIndex++)
                {
                    indicesToOffset.Add(nextIndex);
                    // If we are not checking the last point, we need to check if the it's aligned with the previous segment
                    if (nextIndex < cable.Points.Count - 1)
                    {
                        Vector3 nextSide = segmentInfo[nextIndex].side;
                        if (AbsVector(nextSide) != AbsVector(side))
                        {
                            break;
                        }
                    }
                }

                foreach (int indexToOffset in indicesToOffset)
                {
                    Vector3 newWorldPoint = cable.transform.TransformPoint(cable.Points[indexToOffset].position) + offset * 0.5f;
                    cable.Points[indexToOffset] = new Cable.CablePoint
                    {
                        position = cable.transform.InverseTransformPoint(newWorldPoint),
                        normal = cable.Points[indexToOffset].normal
                    };
                }

                // cable.Points[i] = new Cable.CablePoint
                // {
                //     position = cable.transform.InverseTransformPoint(newWorldP0),
                //     normal = p0.normal
                // };
                // cable.Points[i + 1] = new Cable.CablePoint
                // {
                //     position = cable.transform.InverseTransformPoint(newWorldP1),
                //     normal = p1.normal
                // };


                // splitting the cables - works like crap 

                // Cable.CablePoint? prevPoint = (i > 0) ? cable.Points[i - 1] : null;
                // if (prevPoint.HasValue)
                // {
                //     Vector3 prevWorldP = cable.transform.TransformPoint(prevPoint.Value.position);
                //     Vector3 prevForward = newWorldP0 - prevWorldP;
                //     prevForward.Normalize();
                //     if (Math.Abs(Vector3.Dot(prevForward, forward)) > 0.001f)
                //     {
                //         pendingInsertion = pendingInsertion == null ? (i, new Cable.CablePoint
                //         {
                //             position = p0.position,
                //             normal = p0.normal
                //         }) : pendingInsertion;
                //         Debug.Log($"Inserting segment {i} from point {p0.position} with normal {p0.normal}");
                //     }
                //     else
                //     {
                //         Debug.Log($"No insertion needed at index {i}.");
                //     }
                // }

                cable.GenerateMesh();
                EditorUtility.SetDirty(cable);
            }
        }

        // Handle pending insertion

        // if (pendingInsertion.HasValue)
        // {
        //     Debug.Log("Waiting to release, inserting pending point.");
        //     if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
        //     {
        //         Debug.Log("Mouse released, inserting pending point.");
        //         var (index, point) = pendingInsertion.Value;
        //         cable.Points.Insert(index, point);
        //         Debug.Log($"Inserted new point at index {index} on mouse release.");
        //         pendingInsertion = null;
        //     }
        // }
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
            Handles.DrawWireDisc(hit.point, hit.normal, HandleUtility.GetHandleSize(hit.point) * 0.2f);
        }

        // Debug.Log($"Hit Point: {hit.point}, Hit Normal: {hit.normal}");
        var adjustedColor = Color.green;
        var adjustedNormal = hit.normal;
        var requestedPoint = hit.point;
        var collidedCable = hit.collider.GetComponent<Cable>();
        if (collidedCable != null && collidedCable.Points.Count > 0)
        {
            if (hit.collider == collidedCable.firstPointCollider)
            {
                adjustedColor = Color.red;
                Vector3 worldPoint = collidedCable.Points[0].position;
                requestedPoint = collidedCable.transform.TransformPoint(worldPoint);
                adjustedNormal = collidedCable.Points[0].normal;
            }
            else if (hit.collider == collidedCable.lastPointCollider)
            {
                adjustedColor = Color.blue;
                Vector3 worldPoint = collidedCable.Points.Last().position;
                requestedPoint = collidedCable.transform.TransformPoint(worldPoint);
                adjustedNormal = collidedCable.Points.Last().normal;
            }
        }

        var adjustedPoint = cable.transform.TransformPoint(
            ComputeNextPossibleGroundPoint(requestedPoint, hit.normal, Event.current.shift, Event.current.control)
        );

        using (new Handles.DrawingScope(adjustedColor))
        {
            if (cable.Points.Count > 0)
            {
                Handles.DrawLine(cable.transform.TransformPoint(cable.Points.Last().position), adjustedPoint);
            }
            Handles.DrawWireDisc(adjustedPoint, adjustedNormal, HandleUtility.GetHandleSize(adjustedPoint) * 0.2f);
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

            Debug.Log($"Hit Collider: {hit.collider.name}");
            // if collider is a cable, we can add a special point
            Undo.RecordObject(cable, "Add Cable Point");
            var requestedPosition = hit.point;
            var requestedNormal = hit.normal;
            var collidedCable = hit.collider.GetComponent<Cable>();
            if (collidedCable != null)
            {
                // Align the new point to the cable's first or last point
                if (hit.collider == collidedCable.firstPointCollider)
                {
                    requestedPosition = collidedCable.transform.TransformPoint(collidedCable.Points[0].position);
                    requestedNormal = collidedCable.Points[0].normal;
                }
                else if (hit.collider == collidedCable.lastPointCollider)
                {
                    requestedPosition = collidedCable.transform.TransformPoint(collidedCable.Points.Last().position);
                    requestedNormal = collidedCable.Points.Last().normal;
                }
            }

            var newPosition = ComputeNextPossibleGroundPoint(requestedPosition, requestedNormal, Event.current.shift, Event.current.control);
            if (cable.Points.Count > 1)
            {
                //if last point is the same direction as the previous segment, remove the last point so we don't have multiple points in the same direction
                Vector3 lastPoint = cable.transform.TransformPoint(cable.Points.Last().position);
                Vector3 secondLastPoint = cable.transform.TransformPoint(cable.Points[cable.Points.Count - 2].position);
                Vector3 newWorldPosition = cable.transform.TransformPoint(newPosition);
                Vector3 prevForward = (lastPoint - secondLastPoint).normalized;
                Vector3 forward = (newWorldPosition - lastPoint).normalized;

                Debug.Log($"Last Point: {lastPoint}, Second Last Point: {secondLastPoint}, New Position: {newPosition}");
                Debug.Log($"Previous Forward: {prevForward}, New Forward: {forward}");
                if (Mathf.Abs(Math.Abs(Vector3.Dot(forward, prevForward)) - 1.0f) < 1e-4f)
                {
                    Debug.Log("Last point is in the same direction as the previous segment, removing last point.");
                    cable.Points.RemoveAt(cable.Points.Count - 1);
                }


            }

            Debug.Log($"Adding new point at {newPosition} with normal {hit.normal}");


            cable.Points.Add(new Cable.CablePoint
            {
                position = newPosition,
                normal = requestedNormal
            });
            cable.GenerateMesh();
            EditorUtility.SetDirty(cable);
            e.Use();
        }
    }

    private Vector3 ComputeNextPossibleGroundPoint(Vector3 targetPoint, Vector3 targetNormal, bool snapNormal, bool snapSecondAxis)
    {

        if (cable.Points.Count > 0)
        {
            // snap to one of the axis, where distance is the largest
            Vector3 lastPoint = cable.transform.TransformPoint(cable.Points.Last().position);
            Vector3 distance = lastPoint - targetPoint;
            // Debug.Log($"Last Point: {lastPoint}, Target Point: {targetPoint}, Distance: {distance}");
            int snapDirectionAxis = GetLargestComponentIndex(distance);
            if (snapSecondAxis)
            {
                // snap to the second largest axis
                snapDirectionAxis = GetSecondLargestComponentIndex(distance);
            }
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
    private Vector3 AbsVector(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
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

    private int GetSecondLargestComponentIndex(Vector3 v)
    {
        int largestIndex = GetLargestComponentIndex(v);
        float[] components = { Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z) };
        components[largestIndex] = 0;
        return GetLargestComponentIndex(new Vector3(components[0], components[1], components[2]));
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