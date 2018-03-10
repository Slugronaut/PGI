/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEditor;
using UnityEngine;
using System.Collections;


namespace Pantagruel.Serializer.Editor
{
    /// <summary>
    /// Custom editor for PrefabId component. This helps ensure the
    /// PrefabId component has an internal reference to the name of
    /// the GameObject at all times. If the name of the object changes,
    /// this editor will ensure that the old manifest is updated to match
    /// the new id.
    /// </summary>
    [CustomEditor(typeof(PrefabId))]
    public class PrefabIdEditor : UnityEditor.Editor
    {
        PrefabId Id;

        void Awake()
        {
            Id = this.target as PrefabId;
        }

        public override void OnInspectorGUI()
        {
            
            //make sure our id always matches the name of the gameobject
            if (Id.ManifestId != Id.gameObject.name)
            {
                Id.ManifestId = Id.gameObject.name;

                
                //TODO:
                // - check old manifest to see if any other resources exist in in
                //      -if so, leave old manifest but remove this reference in it
                //      -otherwise, delete old manifest asset
                // - create new manifest asset to match this object's new id
            }

            
            EditorGUILayout.Space();
            GUILayout.TextArea("The 'Manifest Id' below is currently unused but future versions will allow it to track " +
                "the name of the object and update the manifest library accordingly should it change.", GUILayout.Height(60));
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel("Manifest Id", GUILayout.MaxWidth(80));
            EditorGUILayout.SelectableLabel(Id.ManifestId, EditorStyles.textArea, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();
        }
    }
}
