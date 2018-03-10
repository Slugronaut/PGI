/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using UnityEditor;
using UnityEngine;


namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Provides a few convienience controls for draining pools at runtime.
    /// </summary>
    [CustomEditor(typeof(Lazarus))]
    public class AutoPoolEditor : Editor
    {
        int DrainLevel = 0;


        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (EditorApplication.isPlaying)
            {
                GUILayout.Space(15);
                EditorGUILayout.Separator();
                GUILayout.Space(15);
                DrainLevel = EditorGUILayout.IntField("Drain To Level", DrainLevel);
                if (DrainLevel < 0) DrainLevel = 0;
                if (GUILayout.Button("Drain Pools"))
                    Lazarus.DrainPools(DrainLevel);
                GUILayout.Space(10);
                if (GUILayout.Button("Relenquish All"))
                    Lazarus.RelenquishAll();
            }
        }
    }
}
