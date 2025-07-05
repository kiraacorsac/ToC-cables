using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
[Overlay(typeof(SceneView), "Cable Tool Overlay")]
public class CableOverlay : Overlay
{
    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement();
        var removeLastPointButton = new Button(RemoveLastPoint)
        {
            text = "Remove Last Point",
            tooltip = "Remove the last point from the cable."
        };
        root.Add(removeLastPointButton);

        var forceRecalculateButton = new Button(ForceRecalculate)
        {
            text = "Force Recalculate",
            tooltip = "Force the cable to recalculate its mesh."
        };
        root.Add(forceRecalculateButton);

        var flipCableButton = new Button(FlipCable)
        {
            text = "Flip Cable",
            tooltip = "Reverse the order of points in the cable."
        };
        root.Add(flipCableButton);

        var clearCableButton = new Button(() => { ClearCable(); })
        {
            text = "Clear Cable",
            tooltip = "Remove all points from the cable."
        };
        root.Add(clearCableButton);

        return root;
    }

    private static void WithSelectedCable(System.Action<Cable> action)
    {
        var go = Selection.activeGameObject;
        if (go == null) return;
        var cable = go.GetComponent<Cable>();
        if (cable == null) return;
        action(cable);
    }

    private static void RemoveLastPoint()
    {
        WithSelectedCable(cable =>
        {
            if (cable.Points.Count > 0)
            {
                Undo.RecordObject(cable, "Remove Last Cable Point");
                cable.Points.RemoveAt(cable.Points.Count - 1);
                cable.GenerateMesh();
            }
        });
    }

    private static void ForceRecalculate()
    {
        WithSelectedCable(cable =>
        {
            cable.GenerateMesh();
        });
    }

    private static void FlipCable()
    {
        WithSelectedCable(cable =>
        {
            Undo.RecordObject(cable, "Flip Cable Points");
            cable.Points.Reverse();
            cable.GenerateMesh();
        });
    }

    private void ClearCable()
    {
        WithSelectedCable(cable =>
        {
            Undo.RecordObject(cable, "Clear Cable Points");
            cable.Points.Clear();
            cable.GenerateMesh();
        });
    }
}