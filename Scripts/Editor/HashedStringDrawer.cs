/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using Toolbox.Common;

namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Draws a HasheString as a text field and a label that displays the resulting hash.
    /// </summary>
    [CustomPropertyDrawer(typeof(HashedString), true)]
    public class HashedStringDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 2) + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect r = position;
            r.height /= 2;
            EditorGUI.BeginProperty(position, label, property);
            var prop = property.FindPropertyRelative("_Value");
            string value = prop.stringValue;


            Rect first = r;
            r.height -= EditorGUIUtility.standardVerticalSpacing;
            value = EditorGUI.TextField(first, label, value);
            prop.stringValue = value;

            EditorGUI.BeginDisabledGroup(true);
            Rect second = r;
            second.position = new Vector2(r.position.x, r.position.y + r.height);
            EditorGUI.TextField(second, "Hashed Value", HashedString.StringToHash(value).ToString());
            EditorGUI.EndDisabledGroup();

            EditorGUI.EndProperty();
        }
    }
}