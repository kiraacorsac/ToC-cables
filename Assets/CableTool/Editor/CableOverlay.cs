using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
[Overlay(typeof(SceneView), "Cable Tool Overlay")]
public class CableOverlay : Overlay
{
    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        var cable = Selection.activeGameObject?.GetComponent<Cable>();
        if (cable == null)
        {
            root.visible = false;
            root.Add(new Label("Select a GameObject with a Cable component to use the tool."));
            return root;
        }
        var removeLastPointButton = new Button(() => RemoveLastPoint(cable))
        {
            text = "Remove Last Point",
            tooltip = "Remove the last point from the cable."
        };
        root.Add(removeLastPointButton);

        var forceRecalculateButton = new Button(() => ForceRecalculate(cable))
        {
            text = "Force Recalculate",
            tooltip = "Force the cable to recalculate its mesh."
        };
        root.Add(forceRecalculateButton);

        var flipCableButton = new Button(() => FlipCable(cable))
        {
            text = "Flip Cable",
            tooltip = "Reverse the order of points in the cable."
        };
        root.Add(flipCableButton);

        return root;
    }


    private static void RemoveLastPoint(Cable cable)
    {
        if (cable != null)
        {
            if (cable != null && cable.points.Count > 0)
            {
                Undo.RecordObject(cable, "Remove Last Cable Point");
                cable.points.RemoveAt(cable.points.Count - 1);
                cable.GenerateMesh();
            }
        }
    }

    private static void ForceRecalculate(Cable cable)
    {
        if (cable != null)
        {
            cable.GenerateMesh();
        }
    }

    private static void FlipCable(Cable cable)
    {
        if (cable != null && cable.points.Count > 0)
        {
            Undo.RecordObject(cable, "Flip Cable Points");
            cable.points.Reverse();
            cable.GenerateMesh();
        }
    }
}