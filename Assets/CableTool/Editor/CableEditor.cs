using System;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Cable))]
public class CableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Cable cable = (Cable)target;

        DrawDefaultInspector();
        GUI.enabled = false;
        if (cable.observers != null && cable.observers.Count > 0)
        {
            EditorGUILayout.LabelField("Is extended by:");
            for (int i = 0; i < cable.observers.Count; i++)
            {
                // if can be casted to GameObject, show it
                // otherwise, just show some message
                if (cable.observers[i] is MonoBehaviour observerObject) {
                    EditorGUILayout.ObjectField(observerObject, typeof(GameObject), true);
                }
                else
                {
                    EditorGUILayout.LabelField("Not a GameObject - can't show in inspector");
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("Is not extended by anything.");
        }
        GUI.enabled = true;
        

        if(cable.extensionType != CableExtensionType.None){
            GUI.enabled = false;
            EditorGUILayout.LabelField("Extension Type is not 'None', can't set IsActive manually");
        }
        // Draw the IsActive toggle, but disable it if the cable is not of type None
        bool isActive = EditorGUILayout.Toggle("IsActive", cable.IsActive);
        GUI.enabled = true;

        if(isActive != cable.IsActive){
            Debug.Log("Toggling IsActive");
            Undo.RecordObject(cable, "Toggle IsActive");
            cable.IsActive = isActive;
            EditorUtility.SetDirty(cable);
        }


    }
}