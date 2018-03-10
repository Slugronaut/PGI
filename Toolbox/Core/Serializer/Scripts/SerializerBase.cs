/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Pantagruel.Serializer.Surrogate;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Base class with functionality for both serializers and deserializers of xml data.
    /// </summary>
    /// <remarks>
    /// TODO: Surrogates should be selected in reverse order they were added.
    /// That way custom surrogates can override built-in ones.
    /// </remarks>
    public abstract class SerializerBase : ISurrogateSelector
    {
        #region Inner Classes
        /// <summary>
        /// 
        /// </summary>
        protected class TypeInfo
        {
            internal int TypeId;
            internal XmlElement OnlyElement;

            internal void WriteTypeId(XmlElement element)
            {
                element.SetAttribute("typeid", TypeId.ToString());
            }
        }
        #endregion

        protected XmlDocument Doc = new XmlDocument();
        protected Dictionary<string, IDictionary<string, FieldInfo>> FieldInfoCache = new Dictionary<string, IDictionary<string, FieldInfo>>();
        protected Dictionary<string, IDictionary<string, PropertyInfo>> PropertyInfoCache = new Dictionary<string, IDictionary<string, PropertyInfo>>();
        private List<KeyValuePair<Type, ISerializationSurrogate>> Surrogates;
        private BindingFlags AllMembers = BindingFlags.Instance |
           BindingFlags.Public |
           BindingFlags.NonPublic |
           BindingFlags.DeclaredOnly;

        private static ISurrogateSelector NextSelector;
        private static List<KeyValuePair<Type, ISerializationSurrogate>> TempSurrogates;
        private static List<KeyValuePair<Type, IActivationSurrogate>> TempActivators;

        /// <summary>
        /// This should be treated as readonly data. The serializer and deserialize will
        /// load and unload this object automatically. It is simply for reference lookup when
        /// serializing resources that are potentially built into Unity itself.
        /// </summary>
        public static BuiltinResources UnityResources;


        #region Instance Methods
        /// <summary>
        /// Used to clear out all the temporary data used when serializing/deserializing
        /// </summary>
        protected virtual void ClearCache()
        {
            FieldInfoCache = new Dictionary<string, IDictionary<string, FieldInfo>>();
            PropertyInfoCache = new Dictionary<string, IDictionary<string, PropertyInfo>>();
            Surrogates = null;
            ClearTempSurrogates();
        }

        /// <summary>
        /// Applies the commonly used surrogates.
        /// </summary>
        protected void SetupStandardSurrogates()
        {
            //always add surrogates in order of most-specific to most-generic

            //system types
            AddSurrogate(typeof(KeyValuePair<,>), new KeyValuePairSurrogate());
            //REMOVED FOR SAFETY - should be added in using AddTemporarySurrogate() as needed
            //AddSurrogate(typeof(Delegate), new DelegateSurrogate());

            //unity core types
            AddSurrogate(typeof(UnityEngine.Object), new UnityEngineObjectSurrogate());
            AddSurrogate(typeof(GameObject), new GameObjectSurrogate());
            AddSurrogate(typeof(Component), new ComponentSurrogate());

            //unity built-in components and types
            AddSurrogate(typeof(Transform), new TransformSurrogate());
            AddSurrogate(typeof(RectTransform), new RectTransformSurrogate());
            AddSurrogate(typeof(Renderer), new RendererSurrogate());
            AddSurrogate(typeof(Collider2D), new Collider2DSurrogate());

            //monobehaviours
            AddSurrogate(typeof(MonoBehaviour), new MonoBehaviourSurrogate());
        }

        /// <summary>
        /// Returns a list of all fields that are valid for XML serialization for a given datatype.
        /// </summary>
        /// <param name="objType">The type of object being process. Must be a complex type.</param>
        /// <param name="forcedFields">Fields that are being forced to be serialized regardless of public/private state or any attributes.</param>
        /// <returns></returns>
        public IDictionary<string, FieldInfo> GetTypeFieldInfo(Type objType, params string[] forcedFields)
        {
            List<string> forced = new List<string>(forcedFields);
            string typeName = objType.FullName;
            IDictionary<string, FieldInfo> fields;
            if (!FieldInfoCache.TryGetValue(typeName, out fields))
            {
                // fetch fields
                FieldInfo[] fieldInfo = objType.GetFields(AllMembers);
                
                Dictionary<string, FieldInfo> dict = new Dictionary<string, FieldInfo>(fieldInfo.Length);
                foreach (FieldInfo field in fieldInfo)
                {
                    //if (!field.FieldType.IsSubclassOf(typeof(MulticastDelegate)))
                    //{
                        object[] xmlAttr = field.GetCustomAttributes(typeof(XmlIgnoreAttribute), false);
                        object[] nonAttr = field.GetCustomAttributes(typeof(NonSerializedAttribute), false);
                        object[] serAttr = field.GetCustomAttributes(typeof(SerializeField), false);
                        object[] obsAttr = field.GetCustomAttributes(typeof(ObsoleteAttribute), false);
                        if (forced.Contains(field.Name)) dict.Add(field.Name, field);
                        else
                        {
                            if (field.IsPrivate && nonAttr.Length == 0 && serAttr.Length > 0 && xmlAttr.Length == 0)
                            {
                                if (obsAttr.Length == 0) dict.Add(field.Name, field);
                            }
                            if (field.IsPublic && nonAttr.Length == 0 && xmlAttr.Length == 0)
                            {
                                if (obsAttr.Length == 0) dict.Add(field.Name, field);
                            }
                        }
                        //FOR TESTING PURPOSES
                        //dict.Add(field.Name, field);

                    //}
                }

                // check base class as well
                Type baseType = objType.BaseType;
                if (baseType != null && baseType != typeof(object))
                {
                    // should we include this base class?
                    object[] attribs = baseType.GetCustomAttributes(typeof(XmlIgnoreBaseTypeAttribute), false);
                    if (attribs.Length == 0)
                    {
                        IDictionary<string, FieldInfo> baseFields = GetTypeFieldInfo(baseType);
                        // add fields
                        foreach (KeyValuePair<string, FieldInfo> kv in baseFields)
                        {
                            string key = kv.Key;
                            if (dict.ContainsKey(key))
                            {
                                // make field name unique
                                key = "base." + key;
                            }
                            dict.Add(key, kv.Value);
                        }
                    }
                }

                fields = dict;
                FieldInfoCache.Add(typeName, fields);
            }
            return fields;
        }

        /// <summary>
        /// Returns a list of all properties that are valid for XML serialization for a given datatype.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        public IDictionary<string, PropertyInfo> GetTypePropertyInfo(Type objType)
        {
            string typeName = objType.FullName;
            IDictionary<string, PropertyInfo> fields;
            if (!PropertyInfoCache.TryGetValue(typeName, out fields))
            {
                // fetch fields
                PropertyInfo[] fieldInfo = objType.GetProperties(AllMembers);

                Dictionary<string, PropertyInfo> dict = new Dictionary<string, PropertyInfo>(fieldInfo.Length);
                foreach (PropertyInfo field in fieldInfo)
                {
                    //if (!field.PropertyType.IsSubclassOf(typeof(MulticastDelegate)))
                    //{
                        object[] nonAttr = field.GetCustomAttributes(typeof(NonSerializedAttribute), false);
                        object[] obsAttr = field.GetCustomAttributes(typeof(ObsoleteAttribute), false);
                        if (field.CanWrite && field.CanRead && nonAttr.Length == 0)
                        {
                            if(obsAttr.Length == 0) dict.Add(field.Name, field);
                        }
                        //FOR TESTING PURPOSES
                        //else dict.Add(field.Name, field);
                    //}
                }

                // check base class as well
                Type baseType = objType.BaseType;
                if (baseType != null && baseType != typeof(object))
                {
                    // should we include this base class?
                    object[] attribs = baseType.GetCustomAttributes(typeof(XmlIgnoreBaseTypeAttribute), false);
                    if (attribs.Length == 0)
                    {
                        IDictionary<string, PropertyInfo> baseFields = GetTypePropertyInfo(baseType);
                        // add fields
                        foreach(KeyValuePair<string, PropertyInfo> kv in baseFields)
                        {
                            string key = kv.Key;
                            if (dict.ContainsKey(key))
                            {
                                // make field name unique
                                key = "base." + key;
                            }
                            dict.Add(key, kv.Value);
                        }
                    }
                }

                fields = dict;
                PropertyInfoCache.Add(typeName, fields);
            }
            return fields;
        }

        /// <summary>
        /// Adds a serialization surrogate for the 
        /// given type or any type derived from it.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="surrogate"></param>
        public void AddSurrogate(Type type, ISerializationSurrogate surrogate)
        {
            if (type == null || surrogate == null) throw new NullReferenceException();
            if (Surrogates == null) Surrogates = new List<KeyValuePair<Type, ISerializationSurrogate>>();
            Surrogates.Insert(0, new KeyValuePair<Type, ISerializationSurrogate>(type, surrogate));
        }

        /// <summary>
        /// Helper for returning the correct surrogate, if any, for a given type.
        /// </summary>
        /// <param name="type">The type that will be handled by this surrogate.</param>
        /// <returns></returns>
        private ISerializationSurrogate SelectSurrogate(Type type)
        {
            var surrogate = SelectInternalSurrogate(TempSurrogates, type);
            if (surrogate == null) surrogate = SelectInternalSurrogate(Surrogates, type);
            return surrogate;
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        /// <param name="selector"></param>
        public void ChainSelector(ISurrogateSelector selector)
        {
            SerializerBase.NextSelector = selector;
        }

        /// <summary>
        /// Returns the next surrogate selector in the chain.
        /// </summary>
        /// <returns></returns>
        public ISurrogateSelector GetNextSelector()
        {
            return SerializerBase.NextSelector;
        }

        /// <summary>
        /// Returns the approptiate surrogate, if any, for handling
        /// serialziation of a given type or sub-class of a type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="context"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            selector = this;
            var surrogate = SelectSurrogate(type);
            if (surrogate == null)
            {
                var next = GetNextSelector();
                if(next != null)
                {
                    surrogate = next.GetSurrogate(type, context, out selector);
                }
            }

            if (surrogate == null) selector = null;
            return surrogate;
        }
        #endregion


        #region static methods
        /// <summary>
        /// Some unity types can appear to have their references be null but are in fact
        /// using a place-holder null-like object. This method can help compare references types
        /// to both situations.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool IsReferenceNull(object obj)
        {
            if (obj == null) return true;
            else if (obj.Equals(null)) return true;
            else return false;
        }

        /// <summary>
        /// Helper for checking type equality.
        /// </summary>
        /// <param name="baseCls"></param>
        /// <param name="typeToCheck"></param>
        /// <returns></returns>
        public static bool IsSameOrSubclass(Type baseCls, Type typeToCheck)
        {
            return (typeToCheck.IsSubclassOf(baseCls) || typeToCheck == baseCls);
        }

        /// <summary>
        /// Obtains an activator surrogate for the given type if any exist.
        /// </summary>
        /// <param name="objType"></param>
        /// <returns></returns>
        public static IActivationSurrogate GetActivatorSurrogate(Type objType)
        {
            if (objType == null) throw new NullReferenceException();

            if (TempActivators == null) return null;
            for (int i = 0; i < TempActivators.Count; i++)
            {
                KeyValuePair<Type, IActivationSurrogate> kv = TempActivators[i];
                if (kv.Key == objType) return kv.Value;
            }

            return null;
        }

        /// <summary>
        /// Adds a serialization surrogate for the given type to handle specific type serialization.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="surrogate"></param>
        public static void AddTemporarySurrogate(Type type, ISerializationSurrogate surrogate)
        {
            if (type == null || surrogate == null) throw new NullReferenceException();
            if (TempSurrogates == null) TempSurrogates = new List<KeyValuePair<Type, ISerializationSurrogate>>();
            TempSurrogates.Insert(0, new KeyValuePair<Type, ISerializationSurrogate>(type, surrogate));

        }

        /// <summary>
        /// Adds a surrogate that will be used to create instances
        /// of the given type during serialization. This is mainly
        /// for types that do not have default parameterless constructors
        /// and cannot be instantiated by default.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="surrogate"></param>
        public static void AddTemporaryActivationSurrogate(Type type, IActivationSurrogate surrogate)
        {
            if (type == null || surrogate == null) throw new NullReferenceException();
            if (TempActivators == null) TempActivators = new List<KeyValuePair<Type, IActivationSurrogate>>();
            TempActivators.Insert(0, new KeyValuePair<Type, IActivationSurrogate>(type, surrogate));

        }

        /// <summary>
        /// Helper method used by derived classes to clear the static surrogates that
        /// should only last for the current instance of serialization/deserialization.
        /// </summary>
        protected static void ClearTempSurrogates()
        {
            TempSurrogates = null;
            TempActivators = null;
        }

        /// <summary>
        /// Internal helper for getting a surrogate from a specific collection.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static ISerializationSurrogate SelectInternalSurrogate(List<KeyValuePair<Type, ISerializationSurrogate>> surrogateMap, Type type)
        {
            if (type == null) throw new ArgumentNullException();
            if (surrogateMap == null) return null;

            for(int i = 0; i < surrogateMap.Count; i++)
            {
                KeyValuePair<Type, ISerializationSurrogate> kv = surrogateMap[i];
                if(type.IsGenericType && kv.Key.IsGenericType)
                {
                    //we need to be sure both types are generic and both types are compatible
                    //before trying to compare. This is because we have to dynamically make a type
                    //out of the incoming data and the existing data.
                    var tt = kv.Key.GetGenericTypeDefinition();
                    var tr = type.GetGenericTypeDefinition();
                    if (tt != tr) continue;
                    Type t = kv.Key.MakeGenericType(type.GetGenericArguments());
                    if (type == t ||
                        t.IsInstanceOfType(t) ||
                        t.IsAssignableFrom(type))
                        return kv.Value;
                }
                else if (IsSameOrSubclass(kv.Key, type)) return kv.Value;

            }
            return null;
        }

        #endregion
    }


    /// <summary>
    /// Thrown when a exception occurs during serialization or deserialization.
    /// </summary>
    public class SerializationException : Exception
    {

    }


    /// <summary>
    /// Thrown when a deserializer's expected version does not match the file it is deserializing.
    /// </summary>
    public class InvalidVersionException : Exception
    {

    }
}
