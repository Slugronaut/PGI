/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEditor;
using UnityEngine;

namespace PowerGridInventory.Editor
{
    [CustomEditor(typeof(Socketed))]
    [CanEditMultipleObjects]
    public class PGISocketedEditor : PGIAbstractEditor
    {

        protected override void OnEnable()
        {
            //we need to do this because CustomEditor is kinda dumb
            //and won't expose the type we passed to it. Plus relfection
            //can't seem to get at the data either.
            EditorTargetType = typeof(Socketed);
            base.OnEnable();
        }

        public override void OnSubInspectorGUI()
        {
            var soc = target as Socketed;
            soc.SocketCount = EditorGUILayout.IntField(new GUIContent("Socket Count", "The number of sockets this socketable has. Changing this number at runtime is not advised as data may be lost if the number of sockets is reduced."), soc.SocketCount);
        }

    }
}
