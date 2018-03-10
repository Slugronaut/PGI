/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using PowerGridInventory.Utility;
using Toolbox.Common;

namespace PowerGridInventory.Editor
{
	
	/// <summary>
	/// Abstract base Editor class that can be derived from for model, view, slot, and item editors.
	/// It facilitates easy setup of folding for any events marked with the proper PGIFoldedEvent attribute.
	/// </summary>
	public abstract class PGIAbstractEditor : UnityEditor.Editor
	{
		List<SerializedProperty> Props = new List<SerializedProperty>(5);
		List<SerializedProperty> Events = new List<SerializedProperty>(5);
		SerializedProperty EventFolder;
		protected Type EditorTargetType = null;

		protected virtual void OnEnable()
		{
			if(EditorTargetType == null) throw new UnityException("EditorTargetType was not set for a derived class.");
            //Gah! Reflection simply doesn't work with the CustomEditorAttribute class I guess!
            /*
			 * CustomEditor attr = Attribute.GetCustomAttribute(this.GetType(), typeof(CustomEditor), true) as CustomEditor;
			if(attr == null) throw new UnityException("This class must have the CustomEditor attribute.");
			//HACK: For some dumb reason, CustomEditor does not expose the inspected type so we need
			//to look for it using reflection.
			Type inspectedType = null;
			FieldInfo property = attr.GetType().GetField("m_InspectedType", BindingFlags.NonPublic | BindingFlags.Instance);
			if(property != null)
			{
				var propVal = property.GetValue(attr);
				inspectedType = propVal.GetType();
				Debug.Log ("Inspected Type: " + inspectedType.Name);

			}
			else throw new UnityException("Could not retrieve the property 'm_InspectedType' from the CustomEditor class. Most likely, this member has changed with an update to Unity.");
			*/
            bool foldFlagFound = false;
            foreach (FieldInfo info in EditorTargetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (Attribute.IsDefined(info, typeof(HideInInspector))) continue;
                //info.GetCustomAttributes(true);
                if (Attribute.IsDefined(info, typeof(PGIFoldedEventAttribute)))
				{
					if(serializedObject.FindProperty(info.Name) == null)
					{
						Debug.LogError("Could not locate the event: " + info.Name);
					}
					else Events.Add(serializedObject.FindProperty(info.Name));
				}
				else if(Attribute.IsDefined(info, typeof(FoldFlagAttribute)))
				{
					if(foldFlagFound)
					{
						throw new UnityException("There can only be one field with the PGIFoldFlagAttrribute within a class.");
					}
					if(!info.FieldType.IsAssignableFrom(typeof(bool)))
					{
						throw new UnityException("PGIFoldFlagAttribute is only valid on a field of type bool.");
					}
					EventFolder = serializedObject.FindProperty(info.Name);
				}
				else
				{
                    if (target == null) return;
                    if (serializedObject == null) return;
					if(serializedObject.FindProperty(info.Name) == null)
					{
						Debug.LogError("Could not locate the field: " + info.Name);
					}
					else Props.Add(serializedObject.FindProperty(info.Name));
				}
			}

		}

        public abstract void OnSubInspectorGUI();

        public virtual void OnPreEventInspectorGUI()
        {

        }

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();

            //render override-specific properties
            OnSubInspectorGUI();

			//Display non-event properties normally.
			foreach(SerializedProperty prop in Props)
			{
				EditorGUILayout.PropertyField(prop, true);
			}

            OnPreEventInspectorGUI();

            GUILayout.Space(10);
			//display events. Try to fold them if possible.
			if(EventFolder == null)
			{
				//no folder flag was provided. Display events as normal.
				foreach(SerializedProperty prop in Events)
				{
					EditorGUILayout.PropertyField(prop, true);
				}
			}
			else
			{
				//There was a folder flag declared.
				//Fold the events using it.
                EventFolder.boolValue = EditorGUILayout.Foldout(EventFolder.boolValue, new GUIContent("Events"));
				if(EventFolder.boolValue)
				{
					foreach(SerializedProperty prop in Events)
					{
						EditorGUILayout.PropertyField(prop, true);
					}
				}
			}

            if (EditorGUI.EndChangeCheck() || GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
		}
	}
}
