using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Toolbars;
using UnityEngine;

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
        var button = new Button(() =>
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
        })
        { text = "Remove Last Point" };

        root.Add(button);

        return root;
    }
}