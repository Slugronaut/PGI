/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using UnityEditor;
using PowerGridInventory;

namespace PowerGridInventory.Editor
{
    [CustomEditor(typeof(PGISlotItem))]
    [CanEditMultipleObjects]
    public class PGISlotItemEditor : PGIAbstractEditor
    {
        PGISlotItem TargetItem;

        protected override void OnEnable()
        {
            TargetItem = this.target as PGISlotItem;

            //we need to do this because CustomEditor is kinda dumb
            //and won't expose the type we passed to it. Plus relfection
            //can't seem to get at the data either.
            EditorTargetType = typeof(PGISlotItem);
            base.OnEnable();
        }

        public override void OnSubInspectorGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Icon", EditorStyles.boldLabel);
            if (TargetItem != null)
            {
                TargetItem.IconType = (PGISlotItem.IconAssetType)EditorGUILayout.EnumPopup(new GUIContent("Icon Asset Type", "The type of asset to use for this item's view icon."),
                                                                                            TargetItem.IconType);
                TargetItem.IconOrientation = EditorGUILayout.Vector3Field(new GUIContent("Icon Orientation", "The angle that this icon will have within an inventory slot."),
                                                                    TargetItem.IconOrientation);

                if (TargetItem.IconType == PGISlotItem.IconAssetType.Sprite)
                {
                    EditorGUI.indentLevel++;
                    TargetItem.Icon = EditorGUILayout.ObjectField(new GUIContent("Icon", "The sprite that represents this item in an inventory view."),
                                                                   TargetItem.Icon, typeof(Sprite), true) as Sprite;
                    
                    EditorGUI.indentLevel--;

                }
                else
                {
                    EditorGUI.indentLevel++;
                    TargetItem.Icon3D = EditorGUILayout.ObjectField(new GUIContent("Icon", "The 3D mesh that represents ths item in an inventory view."),
                                                                   TargetItem.Icon3D, typeof(Mesh), true) as Mesh;
                    TargetItem.IconMaterial = EditorGUILayout.ObjectField(new GUIContent("Icon Material", "The material used by the 3D mesh icon."),
                                                                   TargetItem.IconMaterial, typeof(Material), true) as Material;
                    EditorGUI.indentLevel--;
                }

            }

            EditorGUILayout.Separator();

        }
        
    }
}