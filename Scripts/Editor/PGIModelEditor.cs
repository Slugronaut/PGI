/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;

namespace PowerGridInventory.Editor
{
    [CustomEditor(typeof(PGIModel))]
    [CanEditMultipleObjects]
    public class PGIModelEditor : PGIAbstractEditor
    {
        PGIModel TargetModel;
        public const int MaxGridSize = 50;

        protected override void OnEnable()
        {
            TargetModel = this.target as PGIModel;

            //we need to do this because CustomEditor is kinda dumb
            //and won't expose the type we passed to it. Plus relfection
            //can't seem to get at the data either.
            EditorTargetType = typeof(PGIModel);
            base.OnEnable();
        }

        public override void OnSubInspectorGUI()
        {
            EditorGUILayout.Space();
            if (TargetModel != null)
            {
                EditorGUILayout.LabelField("Grid Stats", EditorStyles.boldLabel);

                TargetModel.GridCellsX = EditorGUILayout.IntField(
                    new GUIContent("Grid Columns", "The number of cell-columns this model will provide for the grid. It may be zero, in which case there will be no grid."),
                    TargetModel.GridCellsX);
                TargetModel.GridCellsY = EditorGUILayout.IntField(
                    new GUIContent("Grid Rows", "The number of cell-rows this model will provide for the grid. It may be zero, in which case there will be no grid."),
                    TargetModel.GridCellsY);
                
                EditorGUILayout.Separator();

                EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
                TargetModel.AutoDetectItems = EditorGUILayout.Toggle(new GUIContent("Auto-Detect Items", "If set, this model will automatically scan for items that have entered or left its transform hierarchy and add or remove them from the model as needed."), TargetModel.AutoDetectItems);
                if (TargetModel.AutoDetectItems)
                {
                    EditorGUI.indentLevel++;
                    TargetModel.AutoDetectRate = EditorGUILayout.Slider(new GUIContent("Detection Rate", "The number of seconds between each attempt at automatcially detecting any new items found in, or lost from, this model's hierarchy."), TargetModel.AutoDetectRate, 0.0f, 60.0f);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Separator();
                }

            }


        }

    }
}