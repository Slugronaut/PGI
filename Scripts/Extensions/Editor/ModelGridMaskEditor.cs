using UnityEngine;
using UnityEditor;
using PowerGridInventory.Extensions;

namespace PowerGridInventory
{
    /// <summary>
    /// Custome editor for displaying a grid of check boxes where each one
    /// represents the enabled/disabled state of a single grid cell in a PGIModel.
    /// </summary>
    [CustomEditor(typeof(ModelGridMask))]
    public class ModelGridMaskEditor : UnityEditor.Editor
    {
        static int CheckSize = 16;
        ModelGridMask Mask;
        PGIModel Model { get { return Mask.SourceModel; } }


        public void OnEnable()
        {
            Mask = target as ModelGridMask;
        }

        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();
            GUILayout.Space(10);
            
            if (Model == null) return;
            EditorGUILayout.HelpBox("Cells that are un-checked are disabled in the model.", MessageType.None);
            for(int y = 0; y < Model.GridCellsY; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for(int x = 0; x < Model.GridCellsX; x++)
                    Model.EnableCell(x, y, EditorGUILayout.Toggle(!Model.IsCellDisabled(x, y), GUILayout.Width(CheckSize)));
                EditorGUILayout.EndHorizontal();
            }

        }
    }
}
