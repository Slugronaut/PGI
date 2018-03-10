/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.UI;

namespace PowerGridInventory.Editor
{
    [CustomEditor(typeof(PGISlot), true)]
    [CanEditMultipleObjects]
    public class PGISlotEditor : PGIAbstractEditor
    {

        protected override void OnEnable()
        {
            //we need to do this because CustomEditor is kinda dumb
            //and won't expose the type we passed to it. Plus relfection
            //can't seem to get at the data either.
            EditorTargetType = typeof(PGISlot);
            base.OnEnable();
        }

        public override void OnSubInspectorGUI()
        {
            SlotInspector();
        }

        protected void SlotInspector()
        {
            PGISlot s = target as PGISlot;

            s.DefaultIcon = EditorGUILayout.ObjectField(new GUIContent("Default Icon",
                "The default icon to use when no item is displayed in this slot."),
                s.DefaultIcon, typeof(Sprite), true) as Sprite;
            GUILayout.Space(10);

            s.transition = (Selectable.Transition)EditorGUILayout.EnumPopup("Transition", (Enum)s.transition);
            if (s.transition != Selectable.Transition.None)
            {
                EditorGUILayout.HelpBox("The following are colors that work with the 'Selectable' UI element. Typically this is needed for proper Gamepad support but can be turned off if not needed.", MessageType.Info);

                var colors = s.colors;
                colors.normalColor = EditorGUILayout.ColorField("Normal Color", colors.normalColor);
                colors.highlightedColor = EditorGUILayout.ColorField("Hilight Color", colors.highlightedColor);
                colors.pressedColor = EditorGUILayout.ColorField("Pressed Color", colors.pressedColor);
                colors.disabledColor = EditorGUILayout.ColorField("Disabled Color", colors.disabledColor);
                colors.colorMultiplier = EditorGUILayout.Slider("Color Multiplier", colors.colorMultiplier, 1, 5);
                colors.fadeDuration = EditorGUILayout.FloatField("Fade Duration", colors.fadeDuration);
                s.colors = colors;

                GUILayout.Space(10);
            }

            //var prop = serializedObject.FindProperty("navigation");
            //EditorGUILayout.PropertyField(prop, true);
            s.NavMode = (Navigation.Mode)EditorGUILayout.EnumPopup("Navigation", s.NavMode);
            if (s.NavMode == Navigation.Mode.Explicit)
            {
                EditorGUI.indentLevel++;
                s.SelectOnUp = EditorGUILayout.ObjectField("Select On Up", s.SelectOnUp, typeof(Selectable), true) as Selectable;
                s.SelectOnDown = EditorGUILayout.ObjectField("Select On Down", s.SelectOnDown, typeof(Selectable), true) as Selectable;
                s.SelectOnLeft = EditorGUILayout.ObjectField("Select On Left", s.SelectOnLeft, typeof(Selectable), true) as Selectable;
                s.SelectOnRight = EditorGUILayout.ObjectField("Select On Right", s.SelectOnRight, typeof(Selectable), true) as Selectable;
                EditorGUI.indentLevel--;
            }
            GUILayout.Space(10);

        }
    }


    [CustomEditor(typeof(LinkedSlot), true)]
    [CanEditMultipleObjects]
    public class LinkedSlotEditor : PGISlotEditor
    {

        protected override void OnEnable()
        {
            //we need to do this because CustomEditor is kinda dumb
            //and won't expose the type we passed to it. Plus relfection
            //can't seem to get at the data either.
            EditorTargetType = typeof(LinkedSlot);
            base.OnEnable();
        }

        public override void OnSubInspectorGUI()
        {
            LinkedSlot s = target as LinkedSlot;
            s.SourceSlot = EditorGUILayout.ObjectField("Source Slot", s.SourceSlot, typeof(PGISlot), true) as PGISlot;
            SlotInspector();
        }
    }
}