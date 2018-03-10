/**********************************************
* Pantagruel
* Copyright 2015-2017 James Clark
**********************************************/
using System;
using System.Collections.Generic;
using System.Xml;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Xml.Serialization;
using System.IO;
using UnityEngine;
using System.Runtime.Serialization;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Deserializes an object's data using a string of xml-fomratted
    /// text that was previously generated using an XmlSerializer.
    /// </summary>
    public class XmlDeserializer : SerializerBase
    {
        #region Inner Types
        public interface ITypeConverter
        {
            void ProcessType(ref string assemblyFullName, ref string typeFullName);
        }

        /// <summary>
        /// This class is used to store a reference and its container object so that
        /// values may be assigned to that reference at a later time. It supports storing
        /// this reference as a named field of an object, indexed array value, 
        /// or a keyed dictionary value.
        /// </summary>
        public class DeferedReferenceMap
        {
            string KeyIndex;
            int ArrayIndex;
            object OwnerObject;
            
            public DeferedReferenceMap(object owner, string key)
            {
                KeyIndex = key;
                ArrayIndex = -1;
                OwnerObject = owner;
            }

            public DeferedReferenceMap(object owner, int index)
            {
                KeyIndex = null;
                ArrayIndex = index;
                OwnerObject = owner;
            }

            /// <summary>
            /// Assigns an object to the previously stored defered object reference.
            /// </summary>
            /// <param name="reference"></param>
            public void Assign(object reference)
            {
                Type objType = OwnerObject.GetType();
                if(ArrayIndex >= 0)
                {
                    if(objType.IsArray)
                    {
                        object[] objArray = OwnerObject as object[];
                        if (objArray != null) objArray[ArrayIndex] = reference;
                        return;
                    }
                    IList list = OwnerObject as IList;
                    if (list != null)
                    {
                        list[ArrayIndex] = reference;
                        return;
                    }
                }

                if (KeyIndex != null)
                {
                    IDictionary dic = OwnerObject as IDictionary;
                    if(dic != null)
                    {
                        dic[KeyIndex] = reference;
                        return;
                    }

                    FieldInfo field = objType.GetField(KeyIndex, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if(field != null)
                    {
                        field.SetValue(OwnerObject, reference);
                        return;
                    }

                    //BUG ALERT: This could fail if the property expects an array indexer
                    PropertyInfo prop = objType.GetProperty(KeyIndex, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        prop.SetValue(OwnerObject, reference, null);
                        return;
                    }
                }
            }

        }

        public class DeserializeContext
        {
            public XmlDeserializer Deserializer;
            public XmlElement ElementBeingParsed;
            public Type ObjectType;
            public int ObjId = -1;
            public Dictionary<int, Type> deserializationTypeCache;
            public Dictionary<int, object> deserializationObjCache;
        }
        #endregion

        CultureInfo cult;
        Dictionary<int, Type> deserializationTypeCache = null;
        Dictionary<int, object> deserializationObjCache = new Dictionary<int, object>();
        ITypeConverter typeConverter;
        protected Dictionary<int, List<DeferedReferenceMap>> DeferredIds;

        /// <summary>
        /// This is a list of types that we should
        /// never ever try to create an instance of.
        /// </summary>
        private static Type[] BannedActivators = new Type[]
        {
            typeof(Component),
            typeof(Sprite),
            typeof(Material),
            typeof(Shader),
        };
        
        #region Static Methods
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xml"></param>
        /// <param name="maxSupportedVer"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public static T DeserializeComponent<T>(string xml, int maxSupportedVer, GameObject root, bool isSerializerCallback = false) where T : Component
        {
            if (root == null) throw new UnityException("Null GameObject parameter.");
            //throw new UnityException("DeserilizeComponent<T> is not yet implemented.");

            var deserializer = new XmlDeserializer(null);
            deserializer.Doc.LoadXml(xml);
            deserializer.DeferredIds = new Dictionary<int, List<DeferedReferenceMap>>();

            //preload built-in unity resources manifest lookup
            if (!isSerializerCallback)
            {
                GameObject builtinGo = Resources.Load("BuiltinResources") as GameObject;
                SerializerBase.UnityResources = builtinGo.GetComponent<BuiltinResources>();
            }

            deserializer.SetupStandardSurrogates();

            T comp = root.GetComponent<T>();
            if (comp == null) comp = root.AddComponent<T>();

            //run the deserializer
            string version = deserializer.Doc.DocumentElement.GetAttribute("version");
            if (maxSupportedVer < Convert.ToInt32(version))
            {
                return null;
            }
            string culture = deserializer.Doc.DocumentElement.GetAttribute("culture");
            deserializer.cult = new CultureInfo(culture);
            deserializer.DeserializeInPlace(comp, deserializer.Doc.DocumentElement);

            deserializer.AssignDeferedData();

            //remove temporary information
            deserializer.ClearCache();
            SerializerBase.UnityResources = null;
            if (!isSerializerCallback)
                Resources.UnloadUnusedAssets();//we may have loaded some manifests during serialization. Release now
            return comp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="xml"></param>
        /// <param name="maxSupportedVer"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public static Component DeserializeComponent(string xml, int maxSupportedVer, GameObject root, Type type, bool isSerializerCallback = false)
        {
            if (root == null) throw new UnityException("Null GameObject parameter.");
            //throw new UnityException("DeserilizeComponent<T> is not yet implemented.");

            var deserializer = new XmlDeserializer(null);
            deserializer.Doc.LoadXml(xml);
            deserializer.DeferredIds = new Dictionary<int, List<DeferedReferenceMap>>();

            //preload built-in unity resources manifest lookup
            if (!isSerializerCallback)
            {
                GameObject builtinGo = Resources.Load("BuiltinResources") as GameObject;
                SerializerBase.UnityResources = builtinGo.GetComponent<BuiltinResources>();
            }

            deserializer.SetupStandardSurrogates();

            Component comp = root.GetComponent(type);
            if (comp == null) comp = root.AddComponent(type);

            //run the deserializer
            string version = deserializer.Doc.DocumentElement.GetAttribute("version");
            if (maxSupportedVer < Convert.ToInt32(version))
            {
                return null;
            }
            string culture = deserializer.Doc.DocumentElement.GetAttribute("culture");
            deserializer.cult = new CultureInfo(culture);
            deserializer.DeserializeInPlace(comp, deserializer.Doc.DocumentElement);

            deserializer.AssignDeferedData();

            //remove temporary information
            deserializer.ClearCache();
            SerializerBase.UnityResources = null;
            if (!isSerializerCallback)
                Resources.UnloadUnusedAssets();//we may have loaded some manifests during serialization. Release now
            return comp;
        }

        /// <summary>
        /// Deserializes the given element's values directly into the object provided.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The object that will receive the deserialized values.</param>
        /// <param name="element">The Xml element being deserialized and written to the given object.</param>
        protected void DeserializeInPlace<T>(T obj, XmlElement element)
        {
            DeserializeCore(element, obj, obj);
        }

        /// <summary>
        /// Entry-point for deserializing data from an previously serialized xml formatted string.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="maxSupportedVer"></param>
        /// <returns></returns>
        public static object Deserialize(string xml, int maxSupportedVer, bool isSerializerCallback = false)
        {
            return Deserialize(xml, maxSupportedVer, null, isSerializerCallback);
        }

        /// <summary>
        /// Entry-point for deserializing data from an previously serialized xml formatted string.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="maxSupportedVer"></param>
        /// <returns></returns>
        public static object Deserialize(string xml, int maxSupportedVer, ITypeConverter typeConverter, bool isSerializerCallback = false)
        {
            var deserializer = new XmlDeserializer(typeConverter);
            deserializer.Doc.LoadXml(xml);
            deserializer.DeferredIds = new Dictionary<int, List<DeferedReferenceMap>>();

            //preload built-in unity resources manifest lookup
            if (!isSerializerCallback)
            {
                GameObject builtinGo = Resources.Load("BuiltinResources") as GameObject;
                SerializerBase.UnityResources = builtinGo.GetComponent<BuiltinResources>();
            }
            deserializer.SetupStandardSurrogates();

            //run the deserializer
            string version = deserializer.Doc.DocumentElement.GetAttribute("version");
            if (maxSupportedVer < Convert.ToInt32(version))
            {
                throw new InvalidVersionException();
            }
            string culture = deserializer.Doc.DocumentElement.GetAttribute("culture");
            deserializer.cult = new CultureInfo(culture);

            object o = deserializer.DeserializeCore(deserializer.Doc.DocumentElement);

            deserializer.AssignDeferedData();

            ISerializationCallbackReceiver callback = o as ISerializationCallbackReceiver;
            if (callback != null) callback.OnAfterDeserialize();


            //remove temporary information
            deserializer.ClearCache();
            SerializerBase.UnityResources = null;
            if(!isSerializerCallback)
                Resources.UnloadUnusedAssets();//we may have loaded some manifests during serialization. Release now

            return o;
        }
        #endregion


        #region Instance Methods
        /// <summary>
        /// 
        /// </summary>
        protected override void ClearCache()
        {
            base.ClearCache();
            deserializationTypeCache = null;
            deserializationObjCache = new Dictionary<int, object>();
            typeConverter = null;
            DeferredIds = null;
        }

        /// <summary>
        /// Applies values to all previously defered object references.
        /// </summary>
        /// <remarks>
        /// This does not currently support Multidimensional Arrays
        /// </remarks>
        protected void AssignDeferedData()
        {
            foreach (var kv in DeferredIds)
            {
                object value = GetObjFromCache(kv.Key);
                if (value != null)
                {
                    foreach (var refs in kv.Value)
                    {
                        refs.Assign(value);
                    }
                }
                else Debug.LogWarning("Null value stored for id '" + kv.Key + "'.");
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="typeConverter"></param>
        protected XmlDeserializer(ITypeConverter typeConverter)
        {
            this.typeConverter = typeConverter;
        }

        /// <summary>
        /// Main workhorse of the deserilizer. It processes primitives and recursively breaks
        /// complex types down into smaller primitives to be processed.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public object DeserializeCore(XmlElement element, object ownerObj = null, object standin = null)
        {
            // check if this is a reference to another object
            int objId;
            string s = element.GetAttribute("id");
            if (int.TryParse(s, out objId))
            {
                object objCached = GetObjFromCache(objId);
                if (objCached != null) return objCached;
            }
            else
            {
                //TODO: If this is a defered object then we can't deserialize it just yet anyway
                objId = -1;
            }

            // check for null
            string value = element.GetAttribute("value");
            if (value == "null") return null;

            int subItems = element.ChildNodes.Count;
            XmlNode firstChild = element.FirstChild;

            // load type cache if available            
            if (element.GetAttribute("hasTypeCache") == "true")
            {
                LoadTypeCache((XmlElement)firstChild);
                subItems--;
                firstChild = firstChild.NextSibling;
            }
            // get type            
            Type objType;
            string typeId = element.GetAttribute("typeid");
            if (string.IsNullOrEmpty(typeId))
            {
                // no type id so type information must be present
                objType = InferTypeFromElement(element);
            }
            else
            {
                // there is a type id present
                objType = deserializationTypeCache[Convert.ToInt32(typeId)];
            }

            if (objType == null)
            {
                //if there was an id but it's not in the cache yet, this probably means
                //a circular reference to an object currently being deserialized. We can't
                //do anything useful with this information right now so we'll have to
                //add it to the defered deserialization list.
                if(objId != -1)
                {
                    //UPDATE: Unused at the moment. Was original intended to proces
                    //Component.gameObject references that had no type info
                    
                    // the component surrogate doesn't add the 'defered' attribute so we need to do that now.
                    element.SetAttribute("defered", "true");
                    CheckForDeferedDeserialization(ownerObj, element.Name, element);
                    
                    return null;
                }

                //otherwise, something really screwy happened during serialization.
                Debug.LogWarning("No type info supplied for " + element.Name + ". Assigning null value.");
                return null;
            }

            // our value
            object obj = standin;

            //Before we get crazy with built-in deserailizers we want to look at our
            //surrogates and see if any of them handle this datatype.
            ISurrogateSelector temp;
            var surrogate = this.GetSurrogate(objType, new StreamingContext(StreamingContextStates.File), out temp);
            if (surrogate != null)
            {
                obj = DeserializeSurrogate(surrogate, objType, objId, firstChild, element, standin);
                CacheObject(obj, objId);
                if (!SerializerBase.IsReferenceNull(obj)) return obj;
            }

            // process enum
            if (objType.IsEnum)
            {
                long val = Convert.ToInt64(value, cult);
                return Enum.ToObject(objType, val);
            }

            // process some simple types
            switch (Type.GetTypeCode(objType))
            {
                case TypeCode.Boolean: return Convert.ToBoolean(value, cult);
                case TypeCode.Byte: return Convert.ToByte(value, cult);
                case TypeCode.Char: return Convert.ToChar(value, cult);
                case TypeCode.DBNull: return DBNull.Value;
                case TypeCode.DateTime: return Convert.ToDateTime(value, cult);
                case TypeCode.Decimal: return Convert.ToDecimal(value, cult);
                case TypeCode.Double: return Convert.ToDouble(value, cult);
                case TypeCode.Int16: return Convert.ToInt16(value, cult);
                case TypeCode.Int32: return Convert.ToInt32(value, cult);
                case TypeCode.Int64: return Convert.ToInt64(value, cult);
                case TypeCode.SByte: return Convert.ToSByte(value, cult);
                case TypeCode.Single: return Convert.ToSingle(value, cult);
                case TypeCode.String: return value;
                case TypeCode.UInt16: return Convert.ToUInt16(value, cult);
                case TypeCode.UInt32: return Convert.ToUInt32(value, cult);
                case TypeCode.UInt64: return Convert.ToUInt64(value, cult);
            }

            //deserialize array
            if (objType.IsArray)
            {
                //we need to check the attributes to see if we have a multi-dimensional array
                string ranks = element.GetAttribute("Ranks");
                if (!string.IsNullOrEmpty(ranks)) return DeserializeMultiDimensionalArray(objType, objId, subItems, ranks, firstChild);
                return DeserializeArray(objType, objId, subItems, firstChild);
            }

            //last chance attempt to instantiate the object
            if(SerializerBase.IsReferenceNull(obj) && standin == null)
                obj = CreateInstanceOfType(objType);

            //we still have nothing. Just return null. We can't instatiate this object.
            if (SerializerBase.IsReferenceNull(obj)) return null;
            
            //can deserialize self
            IXmlSerializable xmlSer = obj as IXmlSerializable;
            if (xmlSer != null)
            {
                DeserializeSelf(xmlSer, element);
                return obj;
            }

            //it's a list object
            IList lst = obj as IList;
            if (lst != null) return DeserializeIList(lst, firstChild);

            //it's a dictionary object
            IDictionary dict = obj as IDictionary;
            if (dict != null) return DeserializeIDictionary(dict, firstChild);

            //it's a dictionary entry (key/value pair)
            if (objType == typeof(DictionaryEntry) ||
                            (objType.IsGenericType &&
                             objType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)))
                return DeserializeDictionaryEntry(element, firstChild);

            //it's a regular class or struct
            DeserializeComplexType(obj, objType, firstChild);
            CacheObject(obj, objId);
            return obj;
        }

        /// <summary>
        /// Helper method used to cache a deserialized object
        /// if the object and id are valid.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="id"></param>
        protected void CacheObject(object obj, int id)
        {
            if (id >= 0 && !SerializerBase.IsReferenceNull(obj)) deserializationObjCache[id] = obj;
            
        }

        /// <summary>
        /// Helper method that will select a viable method of creating
        /// an instance of an object (default constructor or some other means)
        /// or simply return null if the object cannot be activated.
        /// </summary>
        /// <param name="objType"></param>
        public static object CreateInstanceOfType(Type objType, bool reportErrors = true)
        {
            //make sure we haven't blacklisted this type from being instantiated
            if (IsBannedFromActivation(objType)) return null;

            //TODO: Create an 'Activation Surrogate' list
            //next, see if we have an 'activation surrogate'.
            var activator = SerializerBase.GetActivatorSurrogate(objType);
            if(activator != null)
            {
                var obj = activator.ActivateInstance();
                if (obj != null) return obj;
            }

            //try using the default constructor
            try
            {
                return Activator.CreateInstance(objType, true);
            }
            catch (MissingMethodException e)
            {
                if(reportErrors) Debug.LogWarning("The Type '" + objType.ToString() + "' does not have a default constructor and thus cannot be deserialized using the default system. Try implementing a custom surrogate or marking all members of this type with [NonSerialized] or [XmlIgnore] attributes.\n" + e.Message);
                return null;
            }

           
        }

        /// <summary>
        /// Helper method for determining if a Type belongs to the list of
        /// Types we should never try to instatiate using automation.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        protected static bool IsBannedFromActivation(Type objType)
        {
            if (objType == null) throw new NullReferenceException();
            foreach(Type t in BannedActivators)
            {
                if (SerializerBase.IsSameOrSubclass(t, objType))
                {
                    //if(t == objType)
                    //    Debug.LogWarning("The Type '" + objType.ToString() + "' does not have a default constructor and thus cannot be deserialized using the default system. Try implementing a custom surrogate or marking all members of this type with [NonSerialized] or [XmlIgnore] attributes.");
                    return true;
                }
            }
            return false;
        }
        #endregion


        #region Sub-deserializers
        protected object DeserializeArray(Type objType, int objId, int length, XmlNode firstChild)
        {
            //TODO: Use array functions rather than reflection for this method.
            Type elementType = objType.GetElementType();
            MethodInfo setMethod = objType.GetMethod("Set", new Type[] { typeof(int), elementType });
            //object obj = Array.CreateInstance(elementType, length);
            ConstructorInfo constructor = objType.GetConstructor(new Type[] { typeof(int) });
            var obj = constructor.Invoke(new object[] { length });
            
            // add object to cache if necessary
            CacheObject(obj, objId);
            
            int i = 0;
            foreach (object val in ValuesFromNode(firstChild, obj))
            {
                setMethod.Invoke(obj, new object[] { i, val });
                i++;
            }
            return obj;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// This method makes a *lot* of assumptions. It assumes that the 'ranks' parameter is a well-formed
        /// string that has  two or more comma-delimited integers and that the total number of XmlElements for
        /// the array equals the total length of this array when all dimensions are multiplied.
        /// </remarks>
        /// <param name="arrayType"></param>
        /// <param name="objId"></param>
        /// <param name="length"></param>
        /// <param name="ranks"></param>
        /// <param name="firstChild"></param>
        /// <returns></returns>
        protected object DeserializeMultiDimensionalArray(Type arrayType, int objId, int length, string ranks, XmlNode firstChild)
        {
            //convert ranks string to an array of ints
            string[] rls = ranks.Split(',');
            int[] rli = new int[rls.Length];
            for(int x = 0; x < rls.Length; x++)
            {
                rli[x] = int.Parse(rls[x]);
            }

            Array array = Array.CreateInstance(arrayType.GetElementType(), rli);
            CacheObject(array, objId);
            IterateArray(array, 0, new int[array.Rank], ref firstChild);
            return array;
        }

        /// <summary>
        /// Helper for iterating over a multidimensional array and to deserialize matching elements.
        /// This method makes the assumption that the element being passed in corresponds to the array
        /// indicies being referenced and that the next sibling will correspond to the next set of indicies
        /// regardless of which rank is iterated.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="rank"></param>
        /// <param name="indicies"></param>
        /// <param name="val"></param>
        void IterateArray(Array array, int rank, int[] indicies, ref XmlNode child)
        {
            for (int i = 0; i < array.GetLength(rank); i++)
            {
                indicies[rank] = i;
                if (rank + 1 < array.Rank)
                {
                    IterateArray(array, rank + 1, indicies, ref child);
                }
                else
                {
                    array.SetValue(this.DeserializeCore((XmlElement)child, array), indicies);
                    child = child.NextSibling;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstChild"></param>
        /// <returns></returns>
        IEnumerable ValuesFromNode(XmlNode firstChild, object array, int[] indicies)
        {
            int i = 0;
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                if (CheckForDeferedDeserialization(array, i, (XmlElement)node))
                {
                    i++;
                    yield return null;
                }
                else
                {
                    i++;
                    yield return DeserializeCore((XmlElement)node);
                }

            }
        }

        protected void DeserializeSelf(IXmlSerializable xmlSer, XmlElement element)
        {
            // the object can deserialize itself
            StringReader sr = new StringReader(element.InnerXml);
            XmlReader rd = XmlReader.Create(sr);
            xmlSer.ReadXml(rd);
            rd.Close();
            sr.Close();
        }

        protected object DeserializeIList(IList lst, XmlNode firstChild)
        {
            // it's a list
            foreach (object val in ValuesFromNode(firstChild, lst))
            {
                lst.Add(val);
            }
            return lst;
        }

        protected object DeserializeIDictionary(IDictionary dict, XmlNode firstChild)
        {
            // it's a dictionary
            foreach (object val in ValuesFromNode(firstChild, dict))
            {
                //SLUGGY: So this is a huge bug in the original CustomXmlSerializer.
                //For the record - you cannot cast a KeyValuePair<,> to an an IEnumerable, 
                //to a Dictionary<string, object>. Just gonna leave this here but commented out
                //and use reflection to do essentially the same thing.
                /*
                Dictionary<string, object> dictVal = val as Dictionary<string, object>;
                if (dictVal.ContainsKey("key"))
                {
                    // should be a KeyValuePair
                    dict.Add(dictVal["key"], dictVal["value"]);
                }
                else
                {
                    // should be a DictionaryEntry
                    dict.Add(dictVal["_key"], dictVal["_value"]);
                }
                */

                //HACK ALERT: We are searching for hard-coded named fields
                Type t = val.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo keyField = t.GetField("key", flags);
                if (keyField == null) keyField = t.GetField("_key", flags);
                FieldInfo valueField = t.GetField("value", flags);
                if (valueField == null) t.GetField("_value", flags);

                dict.Add(keyField.GetValue(val), valueField.GetValue(val));

            }
            return dict;
        }

        protected object DeserializeDictionaryEntry(XmlElement element, XmlNode firstChild)
        {
            // load all field contents in a dictionary
            Dictionary<string, object> properties = new Dictionary<string, object>(element.ChildNodes.Count);
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                object val = DeserializeCore((XmlElement)node);
                properties.Add(node.Name, val);
            }
            // return the dictionary
            return properties;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="objType"></param>
        /// <param name="objId"></param>
        /// <param name="firstChild"></param>
        /// <param name="rootElement"></param>
        /// <returns></returns>
        protected object DeserializeSurrogate(ISerializationSurrogate surrogate, Type objType, int objId, XmlNode firstChild, XmlElement rootElement)
        {
            return DeserializeSurrogate(surrogate, objType, objId, firstChild, rootElement, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="obj"></param>
        /// <param name="firstChild"></param>
        /// <returns></returns>
        protected object DeserializeSurrogate(ISerializationSurrogate surrogate, Type objType, int objId, XmlNode firstChild, XmlElement rootElement, object standin)
        {
            //NOTE: Technically this isn't necessary because we can be sure it'll
            //never be called on Components. The GameObject surrogate will handle
            //deserializing components and any loose references will be skipped
            //until the deference phase at the end of deserialization.
            //However, in case serialization wasn't done properly (components were
            //serialized in-place rather than defered to GameObject serialization), 
            //then we need to ensure we simply skip this in such a case.
            if (standin == null)
            {
                if (objType.IsSubclassOf(typeof(Component))) return null;
                if (objType.IsSubclassOf(typeof(Component[]))) return null;
                if (objType == typeof(Component[])) return null;
            }

            SerializationInfo info = new SerializationInfo(objType, new DummyConverter());
            DeserializeContext context = new DeserializeContext();
            context.Deserializer = this;
            context.ObjId = objId;
            context.ElementBeingParsed = rootElement;
            context.ObjectType = objType;
            context.deserializationObjCache = this.deserializationObjCache;
            context.deserializationTypeCache = this.deserializationTypeCache;
            //float through the xml nodes and see what items we need to collect.
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                object val = DeserializeCore((XmlElement)node);
                                    
                if (val != null) info.AddValue(node.Name, val, val.GetType());
                else info.AddValue(node.Name, null, typeof(object));
            }

            //if the standin is null, we need to create an instance.
            //Otherwise, we were given an instance to work with.
            object obj = standin;
            if (obj == null)
            {
                if (SerializerBase.IsSameOrSubclass(typeof(MulticastDelegate), objType))
                {
                    obj = null;
                }
                else
                {
                    obj = CreateInstanceOfType(objType, false);
                    //if(SerializerBase.IsReferenceNull(obj))
                    //    return null;
                }
            }

            //we pass obj in but we also assign it back from the output, just in case
            //the surrogate decided to do something funky. There is also the simple
            //possibility that we couldn't create an instance of the object beforehand
            //so we are actually passing in 'null'.
            obj = surrogate.SetObjectData(obj, 
                info, 
                new StreamingContext(StreamingContextStates.File, context), 
                this);

            if(SerializerBase.IsReferenceNull(obj))
            {
                string typeName = objType == null ? "UNKNOWN" : objType.Name;
                string surrogateName = surrogate == null ? "UNKOWN" : surrogate.ToString();
                string childName = firstChild == null ? "INVALID CHILD" : firstChild.ParentNode.Name;
                Debug.LogWarning("The surrogate '" + surrogateName + "' could not create an instance of '" + typeName + "' for the element '" + childName + "'.");
            }
            CacheObject(obj, objId);
            return obj;
        }

        /// <summary>
        /// Breaks down classes and structs into individual primitives.
        /// May be called recursively to break down further references to complex datatypes.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="objType"></param>
        /// <param name="firstChild"></param>
        protected void DeserializeComplexType(object obj, Type objType, XmlNode firstChild)
        {
            // complex type
            // get the class's fields                                
            IDictionary<string, FieldInfo> dictFields = GetTypeFieldInfo(objType);
            // set values for fields that are found
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                string fieldName = node.Name;
                FieldInfo field = null;
                if (dictFields.TryGetValue(fieldName, out field))
                {
                    if (CheckForDeferedDeserialization(obj, fieldName, (XmlElement)node))
                        continue;
                    // field is present, get value
                    object val = DeserializeCore((XmlElement)node);
                    // set value in object
                    field.SetValue(obj, val);
                }
            }
        }
        #endregion


        #region Other Methods
        /// <summary>
        /// Checks to see if a particular element was defered in serialization
        /// and makes a record of it if so. This record keeps track of the object
        /// and its field that was meant to be assigned the value upon deserization.
        /// </summary>
        /// <param name="destObj">The object whos field was meant to be assigned the deserialized element's data.</param>
        /// <param name="fieldName">The name of the field that was meant to be assigned the deserialized element's data.</param>
        /// <param name="element">The xml element that contains the data to be deserialized that is yet to be determined if the data was defered or not.</param>
        /// <returns></returns>
        public bool CheckForDeferedDeserialization(object destObj, string fieldName, XmlElement element)
        {
            if (destObj == null || fieldName == null || element == null) return false;

            //this object was not serialized in-order, we'll have to defer the process until later.
            string s = element.GetAttribute("defered");
            if (!string.IsNullOrEmpty(s) && s.Equals("true"))
            {
                int i = Convert.ToInt32(element.GetAttribute("id"));
                var df = new DeferedReferenceMap(destObj, fieldName);

                //NOTE: when deserializing other data we will check the defered record made here
                //and add the final 'SourceData' at that time. Then later we can go though this list
                //and reassign all the values that were defered.
                if (DeferredIds.ContainsKey(i))
                {
                    //extend list
                    DeferredIds[i].Add(df);
                }
                else
                {
                    //start a new list for a new index
                    var l = new List<DeferedReferenceMap>();
                    l.Add(df);
                    DeferredIds.Add(i, l);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks to see if a particular element was defered in serialization
        /// and makes a record of it if so. This record keeps track of the object
        /// and its field that was meant to be assigned the value upon deserization.
        /// </summary>
        /// <param name="destObj">The object whos field was meant to be assigned the deserialized element's data.</param>
        /// <param name="fieldName">The name of the field that was meant to be assigned the deserialized element's data.</param>
        /// <param name="element">The xml element that contains the data to be deserialized that is yet to be determined if the data was defered or not.</param>
        /// <returns></returns>
        public bool CheckForDeferedDeserialization(object destObj, int arrayIndex, XmlElement element, bool enumerableOnly = false)
        {
            //this object was not serialized in-order, we'll have to defer the process until later.
            string s = element.GetAttribute("defered");
            if (!string.IsNullOrEmpty(s) && s.Equals("true"))
            {
                int i = Convert.ToInt32(element.GetAttribute("id"));
                var df = new DeferedReferenceMap(destObj, arrayIndex);

                //NOTE: when deserializing other data we will check the defered record made here
                //and add the final 'SourceData' at that time. Then later we can go though this list
                //and reassign all the values that were defered.
                if (DeferredIds.ContainsKey(i))
                {
                    //extend list
                    DeferredIds[i].Add(df);
                }
                else
                {
                    //start a new list for a new index
                    var l = new List<DeferedReferenceMap>();
                    l.Add(df);
                    DeferredIds.Add(i, l);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        void LoadTypeCache(XmlElement element)
        {
            XmlNodeList children = element.GetElementsByTagName("TypeInfo");
            deserializationTypeCache = new Dictionary<int, Type>(children.Count);
            foreach (XmlElement child in children)
            {
                int typeId = Convert.ToInt32(child.GetAttribute("typeid"));
                Type objType = InferTypeFromElement(child);
                deserializationTypeCache.Add(typeId, objType);
            }
        }

        /// <summary>
        /// Helper method to retreive object type info from an xml element.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public Type GetObjectType(XmlElement element)
        {
            // get type            
            Type objType;
            string typeId = element.GetAttribute("typeid");
            if (string.IsNullOrEmpty(typeId))
            {
                // no type id so type information must be present
                objType = InferTypeFromElement(element);
            }
            else
            {
                // there is a type id present
                objType = deserializationTypeCache[Convert.ToInt32(typeId)];
            }

            return objType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firstChild"></param>
        /// <returns></returns>
        IEnumerable ValuesFromNode(XmlNode firstChild, object array)
        {
            int i = 0;
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                if (CheckForDeferedDeserialization(array, i, (XmlElement)node))
                {
                    i++;
                    yield return null;
                }
                else
                {
                    i++;
                    yield return DeserializeCore((XmlElement)node);
                }
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objId"></param>
        /// <returns></returns>
        object GetObjFromCache(int objId)
        {
            object obj;
            if (deserializationObjCache.TryGetValue(objId, out obj))
            {
                return obj;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public Type InferTypeFromElement(XmlElement element)
        {
            Type objType;
            string typeFullName = element.GetAttribute("type");
            string assemblyFullName = element.GetAttribute("assembly");

            if (typeConverter != null)
            {
                typeConverter.ProcessType(ref assemblyFullName, ref typeFullName);
            }

            if (string.IsNullOrEmpty(assemblyFullName))
            {
                if (string.IsNullOrEmpty(typeFullName))
                {
                    return null;
                }
                // type is directly loadable
                objType = Type.GetType(typeFullName, true);
            }
            else
            {
                Assembly asm = Assembly.Load(assemblyFullName);
                objType = asm.GetType(typeFullName, true);
            }
            return objType;
        }

        /// <summary>
        /// helper method used to determine the base datatype from an array of that datatype.
        /// For example if given typeof(int[]) it will return typeof(int). It works for multi-dimensional
        /// arrays as well. Not tested for arrays of arrays.
        /// </summary>
        /// <param name="arrayType"></param>
        /// <returns></returns>
        public static Type StripArrayFromType(Type arrayType)
        {
            if (!arrayType.IsArray) return arrayType;

            string rawTypeName = arrayType.AssemblyQualifiedName.Split('[')[0];
            return arrayType.Assembly.GetType(rawTypeName);
        }

        #endregion

    }
}
