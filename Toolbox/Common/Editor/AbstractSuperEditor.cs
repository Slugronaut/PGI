/**********************************************
* Ancient Craft Games
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using Toolbox.Common;
using UnityEditor.SceneManagement;
using UnityEngine.Assertions;
using UnityEngine.Events;
#if ACG_TOOLBOX_FULL
using Toolbox.Game;
#endif

namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Abstract base Editor class that can be derived from for custom editors.
    /// It has support for many custom attributes not present in Unity's default
    /// editor - such as: ShowInInspector, Inspectable, and FoldedEvent
    /// 
    /// The two biggest features are the ability to automatically group fields
    /// together within a single foldout using [FoldGroupField] and [FoldFlag]
    /// and the ability to display properties in the inspector using
    /// [ShowInInspector] or [Inspectable].
    /// 
    /// Public Service Anouncment: I never write good code when it comes to editors and tools.
    /// Please don't take this file as an example of how to do things well ;)
    /// 
    /// TODO / BUGS:
    /// 
    /// -needs to support property drawers
    /// 
    /// -all regular serialized properties need to be processed and stored into
    /// the MemberData list so that we can properly preserve the order in which elements are rendered
    /// in the inspector.
    /// 
    /// -some class properties still seem to revert to older values when recompiling code.
    /// 
    /// -need support for 'order' field in many attributes for properties. This way
    /// order of rendering for things like [PropertyHeader] and [PropertySpace] can specified.
    /// 
    /// -does not support non-public or static fold flags
    /// 
    /// -does not support grouping of properties?
    /// 
    /// -very limited support for arrays!
    /// 
    /// -no support for properties that expose arrays!
    /// 
    /// </summary>
    public abstract class AbstractSuperEditor : UnityEditor.Editor
    {
#region Inner Datatypes
        public enum MemberType
        {
            SerializedProperty,
            FoldedSerializedProperty,
            ClassProperty,
            ClassField,
        }

        public sealed class MemberData
        {
            public float Spacing;
            public MemberType PropType;
            public Type ForcedType;
            public Type DataType;
            public object Data;
            public SerializedProperty SerProp;
            public object Owner;
            public string Name;
            public string Tooltip;
            public string Header;
            public MemberInfo Info;
            public bool Grouped;
            public bool Folded; //for whatever reason, we cannot convert object Data to a bool value! this is the workaround
        }

        public sealed class GroupData
        {
            public string Id { get; private set; }
            public List<MemberData> Props;
            public MemberData Folder;

            public GroupData(string id)
            {
                Id = id;
                Props = new List<MemberData>(20);
            }
        }

        public sealed class InspectorInstance
        {
            //public List<SerializedProperty> Props = new List<SerializedProperty>(5);
            public List<MemberData> Members = new List<MemberData>(20);
            public Dictionary<string, GroupData> Groups = new Dictionary<string, GroupData>();


            public void AddGroupMember(string id, MemberData member)
            {
                GroupData d = null;
                if (!Groups.TryGetValue(id, out d))
                {
                    d = new GroupData(id);
                    Groups[id] = d;
                }

                d.Props.Add(member);
            }

            public void AddGroupFoldFlag(string id, MemberData member)
            {
                GroupData d = null;
                if (!Groups.TryGetValue(id, out d))
                {
                    d = new GroupData(id);
                    Groups[id] = d;
                }

                d.Folder = member;
            }

            public void ProcessMemberGrouping(MemberData member)
            {
                if (Attribute.IsDefined(member.Info, typeof(FoldedGroupFieldAttribute)))
                {
                    var attrs = member.Info.GetCustomAttributes(typeof(FoldedGroupFieldAttribute), false);
                    var attr = attrs[0] as FoldedGroupFieldAttribute;
                    member.Grouped = true;
                    AddGroupMember(attr.GroupId, member);
                }
                //check to see if this is the flag used to store the folding state of the events
                else if (Attribute.IsDefined(member.Info, typeof(FoldFlagAttribute)))
                {
                    var attrs = member.Info.GetCustomAttributes(typeof(FoldFlagAttribute), false);
                    var attr = attrs[0] as FoldFlagAttribute;
                    member.Grouped = true;
                    AddGroupFoldFlag(attr.GroupId, member);
                }
            }
        }
#endregion


#region Fields and Properties
        InspectorInstance Inspector = new InspectorInstance();

        protected bool DisableUI;
        protected bool ConstantUpdate;
        bool RegisteredUpdate;


        /// <summary>
        /// This will tell us what type of data we are dealing with when processing
        /// a MonoBehaviour's inspector GUIs. Never override this unless you know
        /// exactly what you are doing and why.
        /// </summary>
        protected virtual Type EditorTargetType { get { return target.GetType(); } }
#endregion


#region Virtual Methods
        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnEnable()
        {
            if (target == null) return;
            if (EditorTargetType == null) throw new UnityException("EditorTargetType was not set for a derived class. Please be sure to set it in your custom inspector.");

            //build a collection of all fields that are to be displayed in the inspector
            Inspector.Members = BuildMembersTable(target, serializedObject);

            //build collection data for grouping
            foreach (var member in Inspector.Members)
                Inspector.ProcessMemberGrouping(member);
            

            if(ConstantUpdate)
            {
                SceneView.onSceneGUIDelegate += OnUpdate;
                RegisteredUpdate = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnDisable()
        {
            if(RegisteredUpdate)
            {
                SceneView.onSceneGUIDelegate -= OnUpdate;
                RegisteredUpdate = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="view"></param>
        protected virtual void OnUpdate(SceneView view)
        {
            Repaint();
        }

        /// <summary>
        /// This is processed before all normal properties are rendered
        /// and can be used to handle special data that cannot
        /// be processed automatically.
        /// </summary>
        protected virtual void OnPreAutoInspector()
        {

        }

        /// <summary>
        /// This is called after all normal properties are
        /// rendered but before processing grouped properties.
        /// </summary>
        protected virtual void OnPostAutoInspector()
        {
            GUILayout.Space(10);
        }

        /// <summary>
        /// Helper for rendering a set of MemberData fields.
        /// </summary>
        /// <param name="members"></param>
        void RenderMemberDataSet(List<MemberData> members, bool allowGrouped)
        {
            foreach (var member in members)
            {
                if (member.Data == null) continue;
                if ((!member.Grouped || allowGrouped) && (member.PropType == MemberType.ClassProperty || member.PropType == MemberType.ClassField))
                    MemberDataControl(member);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnInspectorGUI()
        {
            OnPreAutoInspector();
            if (DisableUI) return;
            
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();

            //display fields and properties that are inspectable
            RenderMemberDataSet(Inspector.Members, false);
            OnPostAutoInspector();

            //TODO: We need to add support for several things
            //1) static folding flags (static should be the only one allowed actually?)
            //2) private folding flags
            //3) MemberData structures for grouped data

            //render automatic group-folded properties
            foreach (var group in Inspector.Groups.Values)
            {
                var folder = group.Folder;

                if (folder == null) //no folder flag was provided. Display as normal.
                    RenderMemberDataSet(group.Props, true);
                else
                {
                    //There was a folder flag declared.
                    //Fold the groups using it.
                    folder.Folded = EditorGUILayout.Foldout(folder.Folded, new GUIContent(group.Id), true, EditorStyles.helpBox);
                    if (folder.Folded)
                    {
                        EditorGUI.indentLevel++;
                        RenderMemberDataSet(group.Props, true);
                        EditorGUI.indentLevel--;
                    }
                }
                GUILayout.Space(15);
            }
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck() || GUI.changed)
            {
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.SetDirty(target); //this is important - it lets the scene view know it needs to update
            }
            

        }
#endregion


#region Helpers
        
        /// <summary>
        /// Renders the appropriate control based on the context
        /// of the type of collected memberdata incoming.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        protected static object MemberDataControl(MemberData member)
        {
            object data = member.Data;
            if (data == null) return null;
            PropertyInfo systemProp = data as PropertyInfo;
            FieldInfo systemField = data as FieldInfo;
            SerializedProperty unityProp = data as SerializedProperty;

            //check for arrays. There is no way to handle that in this editor
            //so it is deferred to Unity's serialized property control renderer.
            bool isList = false;
            if( (member.SerProp != null && member.SerProp.isArray) ||
                (unityProp != null && unityProp.isArray) )
                isList = true;

            bool useUnityInspector = false;
            MemberInfo memInfo = data as MemberInfo;
            if(memInfo != null)
            {
                if (Attribute.IsDefined(memInfo, typeof(UseUnityInspectorAttribute)))
                    useUnityInspector = true;
            }

            if (data != null && !isList && !useUnityInspector)
            {
                if (systemProp != null)
                {
                    //TODO: need to add an 'order' value to this attributes to allow re-arranging
                    //we'll have to handle data parsing and control selection ourselves
                    if (!string.IsNullOrEmpty(member.Header))
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField(member.Header, EditorStyles.boldLabel);
                    }
                    GUILayout.Space(member.Spacing);
                    return ReflectiveField(systemProp, member.Owner, member.ForcedType, new GUIContent(systemProp.Name, member.Tooltip));
                }
                else if(systemField != null)
                {
                    //TODO: need to add an 'order' value to this attributes to allow rearranging
                    //we'll have to handle data parsing and control selection ourselves
                    if (!string.IsNullOrEmpty(member.Header))
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField(member.Header, EditorStyles.boldLabel);
                    }
                    GUILayout.Space(member.Spacing);
                    return ReflectiveField(systemField, member.Owner, new GUIContent(systemField.Name, member.Tooltip));
                }
                else
                {
                    //TODO: need to add an 'order' value to this attributes to allow rearranging
                    //we'll have to handle data parsing and control selection ourselves
                    if (!string.IsNullOrEmpty(member.Header))
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField(member.Header, EditorStyles.boldLabel);
                    }
                    GUILayout.Space(member.Spacing);
                    //no property, field, or member data - it was stored a straight object value
                    AnyField(member.DataType, ref data, new GUIContent(string.Empty));
                }
            }
            else if (unityProp != null) return EditorGUILayout.PropertyField(unityProp, new GUIContent(unityProp.displayName, unityProp.tooltip), true);
            else if(member.SerProp != null) return EditorGUILayout.PropertyField(member.SerProp, new GUIContent(member.SerProp.displayName, member.SerProp.tooltip), true);

            return false;
        }

        /// <summary>
        /// Renders the appropriate control based on the context
        /// of the type of collected memberdata incoming.
        /// </summary>
        /// <param name="member"></param>
        /// <returns></returns>
        protected static object MemberDataControl(Rect rect, MemberData member)
        {
            object data = member.Data;
            if (data == null) return null;
            PropertyInfo systemProp = data as PropertyInfo;
            FieldInfo systemField = data as FieldInfo;
            SerializedProperty unityProp = data as SerializedProperty;

            if (data != null)
            {
                if (systemProp != null)
                    return ReflectiveField(rect, systemProp, member.Owner, member.ForcedType, new GUIContent(systemProp.Name, member.Tooltip));
                else if (systemField != null)
                    return ReflectiveField(rect, systemField, member.Owner, new GUIContent(systemField.Name, member.Tooltip));
            }

            else if (unityProp != null) return EditorGUILayout.PropertyField(unityProp, new GUIContent(unityProp.displayName, unityProp.tooltip), true);
            return false;
        }
#endregion


#region Static Helper Methods
        /// <summary>
        /// Displays a control that allows choosing a binding source string as well as a binding source destination.
        /// </summary>
        /// <param name="keys">The list of keys to display in a dropdown for binding sources.</param>
        /// <param name="binding">The binding datatype.</param>
        /// <param name="destType">The type that will be bound to in the destination object. A dropdown list of all fields and properties with this type will be displayed.</param>
        public static bool BindingField(string[] keys, TypeHelper.BindingMap binding, Type destType, object input, Func<string[], TypeHelper.BindingMap, object, bool> editorInjection)
        {
            //controls for selecting dest key
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(20))) return true;
            binding.KeyIndex = EditorGUILayout.Popup(binding.KeyIndex, keys, GUILayout.MaxWidth(150));
            if (binding.KeyIndex >= keys.Length)
            {
                binding.DestObj = null;
                binding.SourceKey = string.Empty;
            }
            else
            {
                BindingField(binding, destType);
                binding.SourceKey = keys[binding.KeyIndex];
            }
            EditorGUILayout.EndHorizontal();

            if (editorInjection != null)
                return editorInjection(keys, binding, input);
            else return false;
        }

        /// <summary>
        /// Displays a selection control for all properties and fields of a given type on an object.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destType">The type that will be bound to in the destination object. A dropdown list of all fields and properties with this type will be displayed.</param>
        public static void BindingField(TypeHelper.BindingMap source, Type destType)
        {
            Assert.IsNotNull(source);
            EditorGUILayout.BeginHorizontal();
            source.DestObj = EditorGUILayout.ObjectField(source.DestObj, typeof(UnityEngine.Object), true, GUILayout.ExpandWidth(false));

            if (source.DestObj != null)
            {
                //compile list of all properties and fields on this dest
                List<string> names = new List<string>(10);
                FieldInfo[] fields = source.DestObj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var member in fields)
                {
                    if (member.FieldType == destType) names.Add(member.Name);
                }

                PropertyInfo[] props = source.DestObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var member in props)
                {
                    if (member.PropertyType == destType && member.CanWrite) names.Add(member.Name);
                }

                //display controls for member path
                GUILayout.Space(25);
                source.PathIndex = EditorGUILayout.Popup(source.PathIndex, names.ToArray());
                if (source.PathIndex >= names.Count) source.Path = "";
                else source.Path = names[source.PathIndex];
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="owner"></param>
        /// <param name="forcedType"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(PropertyInfo prop, object owner, Type forcedType, GUIContent content = null)
        {
            if (forcedType == null) return ReflectiveField(prop, owner, content);

            if (!prop.CanRead || !prop.CanWrite) return 0;
            Type type = forcedType;
            var value = prop.GetValue(owner, null);
            if (content == null) content = new GUIContent(prop.Name, "");

            if (TypeHelper.IsSameOrSubclass(typeof(UnityEngine.Object), type)) value = EditorGUILayout.ObjectField(content, (UnityEngine.Object)value, type, true);

            if (!type.IsArray) prop.SetValue(owner, value, null);
            return 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="owner"></param>
        /// <param name="forcedType"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(Rect rect, PropertyInfo prop, object owner, Type forcedType, GUIContent content = null)
        {
            if (!prop.CanRead || !prop.CanWrite) return 0;

            if (forcedType == null) return ReflectiveField(rect, prop, owner, content);

            //we are trying to force the type of data that is being processed
            //This will only support UnityObjects for now.
            Type type = forcedType;
            var value = prop.GetValue(owner, null);
            if (content == null) content = new GUIContent(prop.Name, "");

            if (TypeHelper.IsSameOrSubclass(typeof(UnityEngine.Object), type)) value = EditorGUI.ObjectField(rect, content, (UnityEngine.Object)value, type, true);

            if (!type.IsArray) prop.SetValue(owner, value, null);
            return 1;
        }

        /// <summary>
        /// Attempts to render an EditorGUILayout control appropriate to the datatype of the given property.
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="owner"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(PropertyInfo prop, object owner, GUIContent content = null)
        {
            if (!prop.CanRead || !prop.CanWrite) return 0;
            Type type = prop.PropertyType;
            var value = prop.GetValue(owner, null);
            if (content == null) content = new GUIContent(prop.Name, "");
            AnyField(type, ref value, content);
            prop.SetValue(owner, value, null);
            return 1;
        }

        /// <summary>
        /// Attempts to render an EditorGUILayout control appropriate to the datatype of the given property.
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="owner"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(Rect rect, PropertyInfo prop, object owner, GUIContent content = null)
        {
            if (!prop.CanRead || !prop.CanWrite) return 0;
            Type type = prop.PropertyType;
            var value = prop.GetValue(owner, null);
            if (content == null) content = new GUIContent(prop.Name, "");
            AnyField(rect, type, ref value, content);
            if (!type.IsArray) prop.SetValue(owner, value, null);
            return 1;
        }

        /// <summary>
        /// Attempts to render an EditorGUILayout control appropriate to the datatype of the given field.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="owner"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(FieldInfo field, object owner, GUIContent content = null)
        {
            Type type = field.FieldType;
            var value = field.GetValue(owner);
            if (content == null) content = new GUIContent(field.Name, "");
            AnyField(type, ref value, content);
            if (!type.IsArray) field.SetValue(owner, value);
            return 1;
        }

        /// <summary>
        /// Attempts to render an EditorGUI control appropriate to the datatype of the given field.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="owner"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static int ReflectiveField(Rect rect, FieldInfo field, object owner, GUIContent content = null)
        {
            Type type = field.FieldType;
            var value = field.GetValue(owner);
            if (content == null) content = new GUIContent(field.Name, "");
            AnyField(rect, type, ref value, content);
            if (!type.IsArray) field.SetValue(owner, value);
            return 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="label"></param>
        /// <param name="layerMask"></param>
        /// <returns></returns>
        static LayerMask LayerMaskField(GUIContent content, LayerMask layerMask)
        {
            List<string> layers = new List<string>();
            List<int> layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (layerName != "")
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }
            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                    maskWithoutEmpty |= (1 << i);
            }
            maskWithoutEmpty = EditorGUILayout.MaskField(content, maskWithoutEmpty, layers.ToArray());
            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                    mask |= (1 << layerNumbers[i]);
            }
            layerMask.value = mask;
            return layerMask;
        }

        /// <summary>
        /// Helper method for rendering various controls.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="content"></param>
        public static int AnyField(Type type, ref object value, GUIContent content)
        {
            if (type == typeof(bool)) value = EditorGUILayout.Toggle(content, (bool)value);
            else if (type == typeof(byte)) value = (byte)EditorGUILayout.DelayedIntField(content, (byte)value);
            else if (type == typeof(short)) value = (short)EditorGUILayout.DelayedIntField(content, (short)value);
            else if (type == typeof(ushort)) value = (ushort)EditorGUILayout.DelayedIntField(content, (ushort)value);
            else if (type == typeof(int)) value = EditorGUILayout.IntField(content, (int)value);
            else if (type == typeof(uint)) value = (uint)EditorGUILayout.IntField(content, (int)value);
            else if (type == typeof(long)) value = EditorGUILayout.LongField(content, (long)value);
            else if (type == typeof(ulong)) value = (ulong)EditorGUILayout.LongField(content, (long)value);
            else if (type == typeof(float)) value = EditorGUILayout.FloatField(content, (float)value);
            else if (type == typeof(double)) value = EditorGUILayout.DoubleField(content, (double)value);
            else if (type == typeof(string)) value = EditorGUILayout.TextField(content, (string)value);
            else if (TypeHelper.IsSameOrSubclass(typeof(Enum), type)) value = EditorGUILayout.EnumPopup(content, (Enum)value);
            else if (type == typeof(Vector2)) value = EditorGUILayout.Vector2Field(content, (Vector2)value);
            else if (type == typeof(Vector3)) value = EditorGUILayout.Vector3Field(content, (Vector3)value);
            else if (type == typeof(Vector4)) value = EditorGUILayout.Vector4Field(content, (Vector4)value);
            else if (type == typeof(Quaternion))
            {
                Quaternion q = (Quaternion)value;
                q.eulerAngles = EditorGUILayout.Vector3Field(content, ((Quaternion)value).eulerAngles);
                value = q;
            }
            else if (type == typeof(Color)) value = EditorGUILayout.ColorField(content, (Color)value);
            else if (type == typeof(Bounds)) value = EditorGUILayout.BoundsField(content, (Bounds)value);
            else if (type == typeof(Rect)) value = EditorGUILayout.RectField(content, (Rect)value);
            else if (type == typeof(AnimationCurve)) value = EditorGUILayout.CurveField(content, (AnimationCurve)value);
            else if (TypeHelper.IsSameOrSubclass(typeof(UnityEngine.Object), type)) value = EditorGUILayout.ObjectField(content, (UnityEngine.Object)value, type, true);
            else if (type == typeof(LayerMask)) value = LayerMaskField(content, (LayerMask)value);
#if ACG_TOOLBOX_FULL
            else if (type == typeof(IBindTargets)) BindTargetsEditorUtility.DisplayBindingsControls((IBindTargets)value);
            
#endif
            else if (type == typeof(HashedString))
            {
                HashedString hs = value as HashedString;
                hs.Value = EditorGUILayout.TextField(content, hs.Value);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Hashed Value", hs.Hash.ToString());
                EditorGUI.EndDisabledGroup();
            }
            else if (TypeHelper.IsSubclassOfRawGeneric(typeof(UnityEvent), type) || TypeHelper.IsSameOrSubclass(typeof(UnityEvent), type))
                value = EditorGUILayout.ObjectField(content, (UnityEngine.Object)value, typeof(UnityEvent), false);
            else if (type.IsArray) return ArrayField(content, ref value);
            else return ComplexField(content, value);
            //TODO: general EnumMask, Assets (Sprite, Texture, etc...)

            return 1;
        }

        /// <summary>
        /// Helper method for rendering various controls.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="content"></param>
        public static int AnyField(Rect rect, Type type, ref object value, GUIContent content)
        {
            if (type == typeof(bool)) value = EditorGUI.Toggle(rect, content, (bool)value);
            else if (type == typeof(byte)) value = (byte)EditorGUI.DelayedIntField(rect, content, (byte)value);
            else if (type == typeof(short)) value = (short)EditorGUI.DelayedIntField(rect, content, (short)value);
            else if (type == typeof(ushort)) value = (ushort)EditorGUI.DelayedIntField(rect, content, (ushort)value);
            else if (type == typeof(int)) value = EditorGUI.IntField(rect, content, (int)value);
            else if (type == typeof(uint)) value = (uint)EditorGUI.IntField(rect, content, (int)value);
            else if (type == typeof(long)) value = EditorGUI.LongField(rect, content, (long)value);
            else if (type == typeof(ulong)) value = (ulong)EditorGUI.LongField(rect, content, (long)value);
            else if (type == typeof(float)) value = EditorGUI.FloatField(rect, content, (float)value);
            else if (type == typeof(double)) value = EditorGUI.DoubleField(rect, content, (double)value);
            else if (type == typeof(string)) value = EditorGUI.TextField(rect, content, (string)value);
            else if (TypeHelper.IsSameOrSubclass(typeof(Enum), type)) value = EditorGUI.EnumPopup(rect, content, (Enum)value);
            else if (type == typeof(Vector2)) value = EditorGUI.Vector2Field(rect, content, (Vector2)value);
            else if (type == typeof(Vector3)) value = EditorGUI.Vector3Field(rect, content, (Vector3)value);
            else if (type == typeof(Vector4)) value = EditorGUI.Vector4Field(rect, content, (Vector4)value);
            else if (type == typeof(Quaternion))
            {
                Quaternion q = (Quaternion)value;
                q.eulerAngles = EditorGUI.Vector3Field(rect, content, ((Quaternion)value).eulerAngles);
                value = q;
            }
            else if (type == typeof(Color)) value = EditorGUI.ColorField(rect, content, (Color)value);
            else if (type == typeof(Bounds)) value = EditorGUI.BoundsField(rect, content, (Bounds)value);
            else if (type == typeof(Rect)) value = EditorGUI.RectField(rect, content, (Rect)value);
            else if (type == typeof(AnimationCurve)) value = EditorGUI.CurveField(rect, content, (AnimationCurve)value);
            else if (TypeHelper.IsSameOrSubclass(typeof(UnityEngine.Object), type)) value = EditorGUI.ObjectField(rect, content, (UnityEngine.Object)value, type, true);
            else if (type == typeof(LayerMask)) value = LayerMaskField(content, (LayerMask)value);
#if ACG_TOOLBOX_FULL
            else if (type == typeof(IBindTargets)) BindTargetsEditorUtility.DisplayBindingsControls((IBindTargets)value);
            
#endif
            else if (type == typeof(HashedString))
            {
                GUILayout.BeginArea(rect);
                HashedString hs = value as HashedString;
                hs.Value = EditorGUILayout.TextField(content, hs.Value);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Hashed Value", hs.Hash.ToString());
                EditorGUI.EndDisabledGroup();
                GUILayout.EndArea();
            }
            else if (TypeHelper.IsSubclassOfRawGeneric(typeof(UnityEvent), type) || TypeHelper.IsSameOrSubclass(typeof(UnityEvent), type))
                value = EditorGUI.ObjectField(rect, content, (UnityEngine.Object)value, typeof(UnityEvent), false);
            //NO SUPPORT FOR ARRAYS CURRENTLY!
            else return ComplexField(rect, content, value);
            //TODO: general EnumMask, Assets (Sprite, Texture, etc...)

            return 1;
        }

        /// <summary>
        /// Recursively renders complex types.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="complexObj"></param>
        /// <returns></returns>
        public static int ComplexField(GUIContent content, object complexObj)
        {
            if (complexObj == null) return 0;
            //Type objType = complexObj.GetType();

            //build a collection of all fields that are to be displayed in the inspector
            List<MemberData> mbs;
            //decomposed complex type
            mbs = BuildMembersTable(complexObj);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(content, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            int count = 0;
            foreach (var member in mbs)
            {
                if (member.Data == null) continue;
                if (member.PropType == MemberType.ClassProperty || member.PropType == MemberType.ClassField)
                {
                    MemberDataControl(member);
                    count++;
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
            return count;
        }

        /// <summary>
        /// Recursively renders complex types.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="complexObj"></param>
        /// <returns></returns>
        public static int ComplexField(Rect rect, GUIContent content, object complexObj)
        {
            if (complexObj == null) return 0;
            
            //build a collection of all fields that are to be displayed in the inspector
            var mbs = BuildMembersTable(complexObj);
            EditorGUILayout.LabelField(content, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            int count = 0;
            foreach (var member in mbs)
            {
                if (member.Data == null) continue;
                if (member.PropType == MemberType.ClassProperty || member.PropType == MemberType.ClassField)
                {
                    MemberDataControl(rect, member);
                    count++;
                }
            }
            EditorGUI.indentLevel--;
            return count;
        }

        /// <summary>
        /// Recursively renders complex types.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="complexObj"></param>
        /// <returns></returns>
        public static int ArrayField(GUIContent content, ref object complexObj)
        {
            if (complexObj == null) return 0;
            Type objType = complexObj.GetType();

            //build a collection of all fields that are to be displayed in the inspector
            List<MemberData> mbs;
            if (objType.IsArray)
            {
                //array
                object[] a = complexObj as object[];
                mbs = BuildArrayTable(complexObj);
                bool changedArray = false;
                int count = 0;

                EditorGUILayout.BeginVertical();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(content, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(15), GUILayout.Height(15)))
                {
                    ArrayUtility.Add(ref a, TypeHelper.GetDefault(objType));
                    changedArray = true;
                    complexObj = a;
                }
                EditorGUI.indentLevel--;


                EditorGUI.indentLevel++;

                if (!changedArray)
                {
                    foreach (var member in mbs)
                    {
                        if (member.Data == null) continue;
                        if (member.PropType == MemberType.ClassProperty || member.PropType == MemberType.ClassField)
                        {
                            MemberDataControl(member);
                            count++;
                        }
                    }

                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                return count;
            }
            return 0;
        }

        /// <summary>
        /// Dispplays a control for editing the HashedString object in an inspector.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="input"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static string HashedStringField(GUIContent content, HashedString input, params GUILayoutOption[] options)
        {
            string output = EditorGUILayout.TextField(content, input.Value);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Hashed Value", input.Hash.ToString());
            EditorGUI.EndDisabledGroup();
            return output;
        }

        /// <summary>
        /// Helper method that creates a table of data that can be used to generate a custom editor.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<MemberData> BuildMembersTable(object value, SerializedObject valueAsSer = null)
        {
            List<MemberData> members = new List<MemberData>(20);
            var type = value.GetType();
            if (!type.IsClass) return members;
            
            var mems = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);// BindingFlags.DeclaredOnly)
            foreach (MemberInfo member in mems)
            {
                FieldInfo fieldInfo = member as FieldInfo;
                PropertyInfo propInfo = member as PropertyInfo;
                MemberData output = null;
                if (fieldInfo != null) output = BuildMemberFromField(value, fieldInfo, valueAsSer);
                else if (propInfo != null) output = BuildMemberFromProperty(value, propInfo, valueAsSer);
                else continue;

                if (output != null)
                {
                    output.Info = member;
                    members.Add(output);
                }
            }

            members.TrimExcess();
            return members;
        }

        /// <summary>
        /// Helper method that creates a table of data that can be used to generate a custom editor.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static List<MemberData> BuildArrayTable(object value, SerializedObject valueAsSer = null)
        {
            List<MemberData> members = new List<MemberData>(20);
            var type = value.GetType();
            if (!type.IsArray) return members;

            Array a = value as Array;
            for(int i = 0; i < a.Length; i++)
            {
                var obj = a.GetValue(i);
                var output = BuildMemberFromArrayIndex(value, obj, i);
                if(output != null)
                {
                    output.Info = null;
                    members.Add(output);
                }
            }
            
            members.TrimExcess();
            return members;
        }

        /// <summary>
        /// Creates a memberdata structure from an object's property.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="propInfo"></param>
        /// <returns></returns>
        public static MemberData BuildMemberFromArrayIndex(object source, object obj, int index)
        {
            Type type = obj.GetType();
            var attrs = type.GetCustomAttributes(typeof(InspectableAttribute), true);
            if (attrs.Length <= 0) return null;

            //store a general-purpose data elements - we'll parse it later when processing the controls
            MemberData d = new MemberData();
            d.Data = obj;
            d.DataType = type;
            d.SerProp = null;
            d.Owner = source;
            d.Name = index.ToString();
            d.Tooltip = (attrs[0] as InspectableAttribute).Tooltip;
            d.ForcedType = (attrs[0] as InspectableAttribute).ForcedType;
            d.PropType = MemberType.ClassProperty;

            attrs = type.GetCustomAttributes(typeof(PropertyHeaderAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Header = (attrs[0] as PropertyHeaderAttribute).Title;

            attrs = type.GetCustomAttributes(typeof(PropertySpaceAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Spacing = (attrs[0] as PropertySpaceAttribute).Height;

            return d;
        }

        /// <summary>
        /// Creates a memberdata structure from an object's property.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="propInfo"></param>
        /// <returns></returns>
        public static MemberData BuildMemberFromProperty(object source, PropertyInfo propInfo, SerializedObject sourceAsSer = null)
        {
            var attrs = propInfo.GetCustomAttributes(typeof(InspectableAttribute), true);
            if (attrs.Length <= 0) return null;
            
            //store a general-purpose data elements - we'll parse it later when processing the controls
            MemberData d = new MemberData();
            d.Data = propInfo;
            d.DataType = propInfo.PropertyType;
            d.SerProp = sourceAsSer.FindProperty(propInfo.Name);
            d.Owner = source;
            d.Name = propInfo.Name;
            d.Tooltip = (attrs[0] as InspectableAttribute).Tooltip;
            d.ForcedType = (attrs[0] as InspectableAttribute).ForcedType;
            d.PropType = MemberType.ClassProperty;

            attrs = propInfo.GetCustomAttributes(typeof(PropertyHeaderAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Header = (attrs[0] as PropertyHeaderAttribute).Title;

            attrs = propInfo.GetCustomAttributes(typeof(PropertySpaceAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Spacing = (attrs[0] as PropertySpaceAttribute).Height;

            return d;
        }

        /// <summary>
        /// Creates a memberdata structure from an object's field.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="serializedObject"></param>
        /// <param name="field"></param>
        public static MemberData BuildMemberFromField(object source, FieldInfo fieldInfo, SerializedObject sourceAsSer)
        {
            //skip if it is supposed to be hidden
            if( !fieldInfo.IsPublic && !Attribute.IsDefined(fieldInfo, typeof(ForceShow)) ) return null;
            if ( Attribute.IsDefined(fieldInfo, typeof(HideInInspector)) ) return null;
            var tooltip = fieldInfo.GetCustomAttributes(typeof(TooltipAttribute), true);
            

            //store a general-purpose data elements - we'll parse it later when processing the controls
            MemberData d = new MemberData();
            d.Data = fieldInfo;
            d.SerProp = sourceAsSer.FindProperty(fieldInfo.Name);
            d.Owner = source;
            d.Name = fieldInfo.Name;
            if (tooltip.Length > 0) d.Tooltip = (tooltip[0] as TooltipAttribute).tooltip;
            d.ForcedType = null;// fieldInfo.FieldType;
            d.PropType = MemberType.ClassField;

            var attrs = fieldInfo.GetCustomAttributes(typeof(HeaderAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Header = (attrs[0] as HeaderAttribute).header;

            attrs = fieldInfo.GetCustomAttributes(typeof(SpaceAttribute), true);
            if (attrs != null && attrs.Length > 0) d.Spacing = (attrs[0] as SpaceAttribute).height;

            return d;
        }

#endregion

    }


    /// <summary>
    /// Base editor for common property drawers of custom classes.
    /// Object must be a reference type (class), must be serializable,
    /// and must have a public default constructor.
    /// </summary>
    public abstract class PropertyDrawerEx<T> : PropertyDrawer where T : class
    {
        protected T Edited;

        
        /// <summary>
        /// Ensures that the data being editor is never null.
        /// </summary>
        /// <param name="prop"></param>
        protected void Init(SerializedProperty prop)
        {
            var target = prop.serializedObject.targetObject;
            Edited = fieldInfo.GetValue(target) as T;
            if (Edited == null)
            {
                Edited = Activator.CreateInstance<T>();
                fieldInfo.SetValue(target, Edited);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init(property);
            return 0;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Init(property);
            EditorGUI.BeginChangeCheck();
            OnInnerGUI(position, property, label);
            if (EditorGUI.EndChangeCheck() || GUI.changed)
            {
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        protected abstract void OnInnerGUI(Rect position, SerializedProperty property, GUIContent label);

    }
}
