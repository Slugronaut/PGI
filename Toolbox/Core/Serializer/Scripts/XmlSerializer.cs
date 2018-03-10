/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System;
using System.Xml;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Text;
using System.Collections;
using System.Xml.Serialization;
using System.Reflection;

namespace Pantagruel.Serializer
{ 

    /// <summary>
    /// General-purpose serializer for primitives, complex types, and Unity GameObject hierarchies.
    /// </summary>
    public class XmlSerializer : SerializerBase
    {
        #region Inner Classes
        public class SerializeContext
        {
            public XmlSerializer Serializer;
            public XmlElement Element;
        }

        protected class ObjInfo
        {
            internal int Id;
            internal XmlElement OnlyElement;

            internal void WriteObjId(XmlElement element)
            {
                element.SetAttribute("id", Id.ToString());
            }
        }

        protected struct ObjKeyForCache : IEquatable<ObjKeyForCache>
        {
            object m_obj;

            public ObjKeyForCache(object obj)
            {
                m_obj = obj;
            }

            public bool Equals(ObjKeyForCache other)
            {
                return object.ReferenceEquals(m_obj, other.m_obj);
            }
        }

        public class SerializationOptions
        {
            public bool UseTypeCache = true;
            public bool UseGraphSerialization = true;
        }
        #endregion

        protected Dictionary<Type, TypeInfo> TypeCache = new Dictionary<Type, TypeInfo>();
        protected Dictionary<Type, IDictionary<ObjKeyForCache, ObjInfo>> ObjCache = new Dictionary<Type, IDictionary<ObjKeyForCache, ObjInfo>>();
        protected int ObjCacheNextId = 0;
        protected SerializationOptions Options;

        protected HashSet<GameObject> ValidHierarchyTargets;
        protected Dictionary<object, int> DeferedCache;
        protected int DeferedCacheId = 0;
        private Stack<bool> SerializeUnityTypesInPlace = new Stack<bool>(new bool[] { false });


        #region Serialize Instance Methods
        /// <summary>
        /// 
        /// </summary>
        protected override void ClearCache()
        {
            base.ClearCache();
            TypeCache = new Dictionary<Type, TypeInfo>();
            ObjCache = new Dictionary<Type, IDictionary<ObjKeyForCache, ObjInfo>>();
            ValidHierarchyTargets = null;
            DeferedCache = null;
            SerializeUnityTypesInPlace = new Stack<bool>(new bool[] { false });
        }

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="opt"></param>
        protected XmlSerializer(SerializationOptions opt)
        {
            Options = opt;
        }

        /// <summary>
        /// Sets the element's attributes to mark it as a reference
        /// to an object that will have its serialization defered to
        /// a later point in the stream. It also adds the object to the
        /// cache so that future references will know to use this id as well.
        /// </summary>
        /// <param name="comp"></param>
        /// <param name="element"></param>
        public void SetAsDeferedComponent(Component comp, XmlElement element)
        {
            if (comp != null)
            {
                if (DeferedCache == null) DeferedCache = new Dictionary<object, int>();
                int id = GetCacheId(comp.GetType(), comp); ;
                DeferedCache[comp] = id;
                element.SetAttribute("id", id.ToString());
                element.SetAttribute("defered", "true");
                DeferedCacheId++;
            }
        }

        /// <summary>
        /// Helper method for serializing a simple or complex type and
        /// formating the data into an xml element.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected XmlElement SerializeCore(string name, object obj)
        {
            //if we are serializing a null object, we have already created the element for it
            //so now we need to let it know there is no data to be found.
            if (IsReferenceNull(obj))
            {
                XmlElement e = Doc.CreateElement(name);
                e.SetAttribute("value", "null");
                return e;
            }

            //we want Unity objects to be identified by their type
            Type objType = obj.GetType();
            XmlElement element = null;
            if (obj is UnityEngine.Object && name == null) element = Doc.CreateElement(objType.Name);
            else element = Doc.CreateElement(name);
            
            //WARNING: I'm guessing 'AnsiClass' means 'struct'. Not entirely sure though.
            //And I have no idea what an 'AutoClass' or 'UnicodeClass' is at all.
            //we are serializing a complex object of some kind
            if(objType.IsClass && objType != typeof(string))
           {
                //This object is a Unity component that had its serialization previously defered.
                //We now need to know what that defered element used as an id and store that
                //when we serialize this object.
                if (DeferedCache != null && DeferedCache.ContainsKey(obj))
                {
                    element.SetAttribute("id", GetCacheId(objType, obj).ToString());
                    //element.SetAttribute("DeferedId", DeferedCache[obj].ToString());
                    //tj
                    //SetAsDeferedComponent((Component)obj, element);
                    //continue serialization as normal from here...
                }
                //if this is not a unity object we still need to check the object cache and see
                //if we already serialized it once.
                else if (Options.UseGraphSerialization && !AddObjToCache(objType, obj, element))
                {
                    return element;
                }
                
                // the object has just been added                
                SetTypeInfo(objType, element);

                //TODO: For general-surrogate support we'll need to implement it here.


                if (CheckForcedSerialization(objType))
                {
                    // serialize as complex type
                    SerializeComplexType(obj, ref element);
                    return element;
                }

                IXmlSerializable xmlSer = obj as IXmlSerializable;
                if (xmlSer == null)
                {
                    //HACK ALERT: Hard-coding logic tied to specific datatypes!!
                    //We need to intercept Unity built-in types here because some of
                    //them expose the IEnumerable interface and will cause inifite loops
                    //(namely, the Transform component).
                    if (objType.IsSubclassOf(typeof(Component)))
                    {
                        SerializeComplexType(obj as Component, ref element);
                        return element;
                    }

                    // does not know about automatic serialization
                    IEnumerable enumerable = obj as IEnumerable;
                    if (enumerable == null)
                    {
                        SerializeComplexType(obj, ref element);
                    }
                    else
                    {
                        //This gets a little poo-poo here. We can't simply serialize
                        //as a list of enumerables if this is a multi-dimensional array
                        //since we'll need to know the individual ranks and rank lengths
                        //when we deserialize. So we'll try casting to see what we do next.
                        var arr = obj as Array;
                        if (arr != null && arr.Rank > 1)
                        {
                            element.SetAttribute("Ranks", ArrayRankCounter(arr));
                            SerializeMultiDimensionalArray(name, arr, 0, new int[arr.Rank], element);
                        }
                        else
                        {
                            //either a single-dimensional array or just an enumerable list of some kind
                            foreach (object arrObj in enumerable)
                            {
                                XmlElement e = SerializeCore(name, arrObj);
                                element.AppendChild(e);
                            }
                        }
                        
                    }
                }
                else
                {
                    // can perform the serialization itself
                    StringBuilder sb = new StringBuilder();
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.ConformanceLevel = ConformanceLevel.Fragment;
                    settings.Encoding = Encoding.UTF8;
                    settings.OmitXmlDeclaration = true;
                    XmlWriter wr = XmlWriter.Create(sb, settings);
                    wr.WriteStartElement("value");
                    xmlSer.WriteXml(wr);
                    wr.WriteEndElement();
                    wr.Close();

                    element.InnerXml = sb.ToString();
                }
            }
            else
            {
                // the object has just been added                
                SetTypeInfo(objType, element);

                if (CheckForcedSerialization(objType))
                {
                    // serialize as complex type
                    SerializeComplexType(obj, ref element);
                    return element;
                }

                if (objType.IsEnum)
                {
                    object val = Enum.Format(objType, obj, "d");
                    element.SetAttribute("value", val.ToString());
                }
                else
                {
                    if (objType.IsPrimitive || objType == typeof(string) ||
                        objType == typeof(DateTime) || objType == typeof(decimal))
                    {
                        element.SetAttribute("value", obj.ToString());
                    }
                    else
                    {
                        // this is most probably a struct (or autoclass or unicodeclass, whatever those are)
                        SerializeComplexType(obj, ref element);
                    }
                }
            }

            return element;
        }

        /// <summary>
        /// Helper method for getting the lengths of all Ranks in a multi-dimensional array
        /// and build them into a string that can be parsed.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="a"></param>
        /// <param name="rank"></param>
        /// <param name="indicies"></param>
        string ArrayRankCounter(Array a)
        {
            StringBuilder str = new StringBuilder();
            for(int rank = 0; rank < a.Rank; rank++)
            {
                if (str.Length > 0) str.Append(","); 
                str.Append(a.GetLength(rank));
            }

            return str.ToString();
        }

        /// <summary>
        /// Recursive helper method for serializing multi-dimensional arrays.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="rank"></param>
        /// <param name="indicies"></param>
        /// <param name="element"></param>
        void SerializeMultiDimensionalArray(string name, Array array, int rank, int[] indicies, XmlElement element)
        {
            foreach(var val in array)
            {
                XmlElement e = SerializeCore(name, val);
                element.AppendChild(e);
            }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="rank"></param>
        /// <param name="indicies"></param>
        /// <param name="element"></param>
        void DeserializeMultiDimensionalArray(ref Array a, int rank, int[] indicies, XmlElement element)
        {
            for(int i = 0; i < a.GetLength(rank); i++)
            {
                indicies[rank] = i;
                if(rank + 1 < a.Rank)
                {
                    //go deeper
                    DeserializeMultiDimensionalArray(ref a, rank + 1, indicies, element);
                }
                else
                {
                    //deserialize element
                    //object val = DeserializeCore(element.NextSibling)
                }
            }
        }

        /// <summary>
        /// Helper method for serializing a complex type and
        /// formatting the data into an xml element.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="element"></param>
        protected void SerializeComplexType(object obj, ref XmlElement element)
        {
            //Just in case we call this directly from outside the SerializeCore method,
            //we'll check for null values again here.
            if (IsReferenceNull(obj))
            {
                element.Attributes.RemoveNamedItem("typeid");
                element.SetAttribute("value", "null");
                return;
            }

            Type objType = obj.GetType();

            //if we are serializing components or GameObjects they must be part of a hierarchy
            //that is being serialized. If they are not, simply supply 'null' as the value.
            //This one exception to this is if they are prefabs.
            if(objType == typeof(GameObject) || objType.IsSubclassOf(typeof(Component)))
            {
                //check to see if this is a prefab
                GameObject goTemp = obj as GameObject;
                if (goTemp != null && goTemp.GetComponent<PrefabId>() != null) {/*dummy off*/ }
                else
                {
                    //not a prefab and not part of this hierarchy.
                    //Just set value attributes to "null"
                    if (ValidHierarchyTargets == null || (objType == typeof(GameObject)) ?
                        !ValidHierarchyTargets.Contains(obj as GameObject) :
                        !ValidHierarchyTargets.Contains((obj as Component).gameObject))
                    {
                        element.Attributes.RemoveNamedItem("typeid");
                        element.SetAttribute("value", "null");
                        return;
                    }
                }
            }
            

            //check for defered serialization
            if(objType.IsSubclassOf(typeof(Component)) && !SerializeUnityTypesInPlace.Peek() && !ComponentMode)
            {
                SetAsDeferedComponent(obj as Component, element);
                return;
            }

            //this stack helps us track when to serialize
            //components in place and when to defer them
            if (objType == typeof(GameObject))
                SerializeUnityTypesInPlace.Push(true);
            else SerializeUnityTypesInPlace.Push(false);


            //first, we want to look at our surrogates and
            //see if any of them handle this datatype.
            ISurrogateSelector temp;
            var surrogate = this.GetSurrogate(objType, new StreamingContext(StreamingContextStates.File), out temp);
            if (surrogate != null)
            {
                SerializeSurrogate(surrogate, obj, ref element);
                SerializeUnityTypesInPlace.Pop();
                return;
            }

            //no surrogates, simply use the fallback general-purpose complex type serializer
            // get all instance fields
            IDictionary<string, FieldInfo> fields = GetTypeFieldInfo(objType);
            foreach (KeyValuePair<string, FieldInfo> kv in fields)
            {
                // serialize field
                XmlElement e = SerializeCore(kv.Key, kv.Value.GetValue(obj));
                element.AppendChild(e);
            }

            SerializeUnityTypesInPlace.Pop();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="obj"></param>
        /// <param name="objType"></param>
        /// <returns></returns>
        protected void SerializeSurrogate(ISerializationSurrogate surrogate, object obj, ref XmlElement element)
        {
            if (surrogate != null && obj != null && element != null)
            {
                SerializationInfo info = new SerializationInfo(obj.GetType(), new DummyConverter());
                var context = new SerializeContext();
                context.Serializer = this;
                context.Element = element;

                surrogate.GetObjectData(obj, info, new StreamingContext(StreamingContextStates.File, context));

                //BUG: THIS BREAKS THE ObjCache ID SYSTEM
                //It is possible that the surrogate wants to create the element
                //for itself (often for the sake of renaming it). Check for such
                //a change now, and if so we need to assign it back to the one
                //that is written to the document. We'll also have to copy over
                //the attributes Of the original element.
                /*if(element != context.Element)
                {
                    foreach(XmlNode att in element.Attributes)
                    {
                        context.Element.SetAttribute(att.Name, att.Value);
                    }
                    element = context.Element;
                }*/

                foreach (var pair in info)
                {
                    var e = SerializeCore(pair.Name, pair.Value);
                    element.AppendChild(e);
                }
            }
            
        }
        #endregion


        #region Other Instance Methods
        /// <summary>
        /// Adds the object to a 'previously serialized' cache. This way we
        /// know if we are trying to serialize something that already has been.
        /// </summary>
        /// <param name="objType"></param>
        /// <param name="obj"></param>
        /// <param name="element"></param>
        /// <returns></returns>
        public bool AddObjToCache(Type objType, object obj, XmlElement element)
        {
            ObjKeyForCache kfc = new ObjKeyForCache(obj);
            IDictionary<ObjKeyForCache, ObjInfo> entry;
            if (ObjCache.TryGetValue(objType, out entry))
            {
                // look for this particular object                
                ObjInfo objInfoFound;
                if (entry.TryGetValue(kfc, out objInfoFound))
                {
                    // the object has already been added
                    if (objInfoFound.OnlyElement != null)
                    {
                        objInfoFound.WriteObjId(objInfoFound.OnlyElement);
                        objInfoFound.OnlyElement = null;
                    }
                    // write id to element
                    objInfoFound.WriteObjId(element);
                    return false;
                }
            }
            else
            {
                // brand new type in the cache
                entry = new Dictionary<ObjKeyForCache, ObjInfo>(1);
                ObjCache.Add(objType, entry);
            }
            // object not found, add it
            ObjInfo objInfo = new ObjInfo();
            objInfo.Id = ObjCacheNextId;
            objInfo.OnlyElement = element;
            entry.Add(kfc, objInfo);
            ObjCacheNextId++;
            return true;
        }

        /// <summary>
        /// Returns the cache id for a given type.
        /// If the type is not currently in the cache
        /// a value of -1 will be returned.
        /// </summary>
        /// <param name="objType"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int GetCacheId(Type objType, object obj)
        {
            ObjKeyForCache kfc = new ObjKeyForCache(obj);
            IDictionary<ObjKeyForCache, ObjInfo> entry;
            if (ObjCache.TryGetValue(objType, out entry))
            {
                // look for this particular object                
                ObjInfo objInfoFound;
                if (entry.TryGetValue(kfc, out objInfoFound))
                {
                    return objInfoFound.Id;
                }
            }
            return -1;
        }

        /// <summary>
        /// Sets the given type info for an xml element and then stores
        /// that type info in the cache. If the type info already exists
        /// in the type cache, a lookup type id is set for the element instead.
        /// </summary>
        /// <param name="objType"></param>
        /// <param name="element"></param>
        public void SetTypeInfo(Type objType, XmlElement element)
        {
            if (!Options.UseTypeCache)
            {
                // add detailed type information
                WriteTypeToNode(element, objType);
                return;
            }
            TypeInfo typeInfo;
            if (TypeCache.TryGetValue(objType, out typeInfo))
            {
                XmlElement onlyElement = typeInfo.OnlyElement;
                if (onlyElement != null)
                {
                    // set the type of the element to be a reference to the type ID
                    // since the element is no longer the only one of this type                    
                    typeInfo.WriteTypeId(onlyElement);
                    onlyElement.RemoveAttribute("type");
                    onlyElement.RemoveAttribute("assembly");
                    typeInfo.OnlyElement = null;
                }
                typeInfo.WriteTypeId(element);
            }
            else
            {
                // add type to cache
                typeInfo = new TypeInfo();
                typeInfo.TypeId = TypeCache.Count;
                typeInfo.OnlyElement = element;
                TypeCache.Add(objType, typeInfo);
                // add detailed type information
                WriteTypeToNode(element, objType);
            }
        }

        /// <summary>
        /// Locates and returns the XML element that contains the
        /// type cache for the currently active document.
        /// </summary>
        /// <returns></returns>
        XmlElement GetTypeInfoNode()
        {
            XmlElement element = Doc.CreateElement("TypeCache");
            foreach (KeyValuePair<Type, TypeInfo> kv in TypeCache)
            {
                if (kv.Value.OnlyElement == null)
                {
                    // there is more than one element having this type
                    XmlElement e = Doc.CreateElement("TypeInfo");
                    kv.Value.WriteTypeId(e);
                    WriteTypeToNode(e, kv.Key);
                    element.AppendChild(e);
                }
            }
            return element.HasChildNodes ? element : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        void SetHierarchyRoot(GameObject obj)
        {
            ValidHierarchyTargets = null;
            if(obj != null)
            {
                ValidHierarchyTargets = new HashSet<GameObject>();
                AddChildrenToHierarchy(obj.transform, ValidHierarchyTargets);
                
            }
        }

        /// <summary>
        /// Helper for recursively adding GameObjects' transform children to a hierarchy set.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="hierarchy"></param>
        void AddChildrenToHierarchy(Transform root, HashSet<GameObject> hierarchy)
        {
            hierarchy.Add(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
            {
                AddChildrenToHierarchy(root.GetChild(i), hierarchy);
            }
        }
        #endregion


        #region Static Methods
        static bool ComponentMode = false;
        /// <summary>
        /// Serializes a given object's data and formats it in an xml layout.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="version"></param>
        /// <param name="rootName"></param>
        public static XmlDocument Serialize(object obj, int version, string rootName, bool isSerializeCallback = false)
        {
            if (obj == null) return null;
            ComponentMode = false;

            // determine serialization options
            SerializationOptions serOptions = new SerializationOptions();
            Type objType = obj.GetType();
            if (obj != null)
            {
                object[] attribs = objType.GetCustomAttributes(typeof(CustomXmlSerializationOptionsAttribute), false);
                if (attribs.Length > 0)
                {
                    serOptions = ((CustomXmlSerializationOptionsAttribute)attribs[0]).SerializationOptions;
                }
               
            }

            // create serializer
            var serializer = new XmlSerializer(serOptions);
            serializer.SetHierarchyRoot(obj as GameObject);

            //preload built-in unity resources
            if (!isSerializeCallback)
            {
                GameObject builtinGo = Resources.Load("BuiltinResources") as GameObject;
                SerializerBase.UnityResources = builtinGo.GetComponent<BuiltinResources>();
            }

            serializer.SetupStandardSurrogates();

            //we are serializing a single component, set the mode for that
            Component comp = obj as Component;
            if(comp != null)
            {
                ComponentMode = true;
                serializer.ValidHierarchyTargets = new HashSet<GameObject>();
                serializer.ValidHierarchyTargets.Add(comp.gameObject);
            }

            ISerializationCallbackReceiver callback = obj as ISerializationCallbackReceiver;
            if (callback != null) callback.OnBeforeSerialize();

            XmlElement element = serializer.SerializeCore(rootName, obj);
            element.SetAttribute("version", version.ToString());
            element.SetAttribute("culture", Thread.CurrentThread.CurrentCulture.ToString());

            // add typeinfo
            XmlElement typeInfo = serializer.GetTypeInfoNode();
            if (typeInfo != null)
            {
                element.PrependChild(typeInfo);
                element.SetAttribute("hasTypeCache", "true");
            }
            // add serialized data
            serializer.Doc.AppendChild(element);

            //remove temporary surrogates
            serializer.ClearCache();
            SerializerBase.UnityResources = null;
            Resources.UnloadUnusedAssets();//we may have loaded some manifests during serialization. Release now
            return serializer.Doc;
        }

        /// <summary>
        /// Similar to <see cref="XmlSerializer.Serialize(GameObject, int)"/> but it precludes
        /// the need for a rootName by supplying the value "GameObject" automatically.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="version"></param>
        public static XmlDocument Serialize(GameObject obj, int version)
        {
            return Serialize(obj, version, "GameObject");
        }

        /// <summary>
        /// Helper method for writing the type info to an xml element.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="objType"></param>
        static void WriteTypeToNode(XmlElement element, Type objType)
        {
            element.SetAttribute("type", objType.FullName);
            element.SetAttribute("assembly", objType.Assembly.FullName);
        }

        /// <summary>
        /// Returns true if the given type is forced to be serialized using this serializer.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        static bool CheckForcedSerialization(Type objType)
        {
            object[] attribs = objType.GetCustomAttributes(typeof(XmlSerializeAsCustomTypeAttribute), false);
            return attribs.Length > 0;
        }
        #endregion
    }
}