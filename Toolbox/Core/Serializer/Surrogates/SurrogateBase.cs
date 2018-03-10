/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Base class to help quickly create surrogates.
    /// </summary>
    public abstract class SurrogateBase : ISerializationSurrogate
    {
        public const string SubResourceDelimeter = "@";



        /// <summary>
        /// Definition used to force members to be serialized as certain types. Useful
        /// when classes define several implict type casts as part of their definition.
        /// </summary>
        public class ForcedMemberType
        {
            public string Name;
            public Type Type;

            public ForcedMemberType(string name, Type type)
            {
                Name = name;
                Type = type;
            }
        }

        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public abstract void GetObjectData(object obj, SerializationInfo info, StreamingContext context);

        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public abstract object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector);

        /// <summary>
        /// Helper method that is used to push data from a SerializationInfo
        /// structure back into an object's fields and properties.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        protected static void ReplaceState(object obj, SerializationInfo info, params string[] filter)
        {
            if (SerializerBase.IsReferenceNull(obj))
            {
                if (obj != null) Debug.LogWarning("An object of type: " + info.FullTypeName + " isn't initialized on the C++ side of things. This is likely due to activation of a Unity object in a way that Unity doesn't allow.");
                return;
                //throw new NullReferenceException();
            }
            Type objType = obj.GetType();
            foreach (var entry in info)
            {
                MemberInfo[] members = objType.GetMember(entry.Name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                bool skip;
                foreach (var member in members)
                {
                    skip = false;
                    if (filter != null)
                    {
                        for (int i = 0; i < filter.Length; i++)
                        {
                            if (member.Name == filter[i]) skip = true;
                        }
                    }
                    if (!skip)
                    {
                        try
                        {
                            FieldInfo field = member as FieldInfo;
                            if (field != null) field.SetValue(obj, entry.Value);
                            PropertyInfo property = member as PropertyInfo;
                            if (property != null) property.SetValue(obj, entry.Value, null);
                        }
                        #pragma warning disable 0168
                        catch(ArgumentException e)
                        {
                            Debug.Log("<color=red>" + e.Message + " ->  " + obj.GetType().Name + " from info type " + entry.Value.GetType() + "</color>");
                        }
                        #pragma warning restore 0168
                    }
                }
            }

        }

        /// <summary>
        /// Helper method that attempts to store the object as a string to a runtime
        /// resource that can be loaded with <see cref="Resources.Load()"/>. Only
        /// types that are supported by the resource manifest system will be considered.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <returns><c>true</c> if the object was added to the SerializationInfo as a rsource string, <c>false</c> otherwise.</returns>
        protected static bool SerializeAsResource(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return false;
            var sc = context.Context as XmlSerializer.SerializeContext;


            //let's see if this is a unity resource that can be stored as just a string
            Type objType = obj.GetType();
            for (int i = 0; i < Constants.ResourceTypes.Length; i++)
            {
                if (objType == Constants.ResourceTypes[i])
                {
                    UnityEngine.Object uo = obj as UnityEngine.Object;

                    //before we check the manifests, see if this object exists in the BuiltinResources list
                    int uId = SerializerBase.UnityResources.Resources.IndexOf(uo);
                    if(uId >= 0)
                    {
                        //we have a unity, builtin resource, we need to serialize a slightly different way.
                        sc.Element.SetAttribute("BuiltinId", uId.ToString());
                    }
                    
                    //try to serialize this as a runtime 
                    //resource path if it exists in a manifest
                    string manifestFile = "Manifests/" + ResourceManifest.GetManifestName(uo.name);

                    var manifest = Resources.Load(manifestFile, typeof(ResourceManifest)) as ResourceManifest;
                    if (manifest != null)
                    {
                        string path = manifest.GetPathToResource(uo);
                        if (!string.IsNullOrEmpty(path))
                        {
                            //Make it an attribute rather than a node.
                            //That way we can identify if it was serialized
                            //in-place or not during deserialization.
                            sc.Element.SetAttribute("ResourcePath", path);
                            return true;
                        }
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Helper method that attempts to deserialize an entry as a resource path where
        /// that path can be used by <see cref="Resources.Load"/> to retrieve an instance
        /// of the desired object.
        /// </summary>
        /// <param name="context">The <see cref="XmlDeserializer.DeserializeContext"/> that should have been passed in as the <see cref="StreamingContext.Context"/> member.</param>
        /// <returns>The loaded object if xml element has a resource path and the path was valid. <c>Null</c> otherwise.</returns>
        protected object DeserializeAsResource(XmlDeserializer.DeserializeContext context)
        {
            if (context == null) return null;

            var type = context.ObjectType;
            if (type == null) throw new UnityException("The object type passed to the deserializer context was null.");

            //check to see if the object was serialized as a resource string
            string path = context.ElementBeingParsed.GetAttribute("ResourcePath");
            if (!string.IsNullOrEmpty(path))
            {
                //HACK ALERT: in the case of sub-resources (like sliced sprites)
                //we've encoded the sub resource index directly into the path name.
                //It's been delimited by a special character that hopefully won't have to change :(
                if(path.Contains(SubResourceDelimeter))
                {
                    var s = path.Split(new string[] { SubResourceDelimeter}, StringSplitOptions.RemoveEmptyEntries);
                    if (s.Length >= 2)
                    {
                        int index;
                        if (int.TryParse(s[s.Length - 1], out index))
                        {
                            //uh-oh! This is likely a sub-resource. Need to get all resources at the location to confirm
                            var all = Resources.LoadAll(s[0], type);
                            if (index < all.Length)
                            {
                                //BUG ALERT: Not bothering with type check here. Wouldn't make a difference
                                //anyway since this train is off the tracks if it doesn't match!
                                return all[index];
                            }
                            else return Resources.Load(path, type); //likely to not work, but it's our last chance :O *jumps from the train*
                        }
                    }
                }
                return Resources.Load(path, type);
            }

            //is it perhaps a unity builtin resource?
            path = context.ElementBeingParsed.GetAttribute("BuiltinId");
            if (!string.IsNullOrEmpty(path))
            {
                int id = -1;
                Int32.TryParse(path, out id);
                if (id >= 0)
                {
                    return SerializerBase.UnityResources.Resources[id];
                }
            }

            return null;
        }

        /// <summary>
        /// Helper method that prepares info that is typcially
        /// serialized for MonoBehaviour components
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        protected static void GatherMonoBehaviourFields(object obj, SerializationInfo info, StreamingContext context)
        {
            if (SerializerBase.IsReferenceNull(obj)) return;// throw new NullReferenceException();
            var c = obj as MonoBehaviour;
            var sc = context.Context as XmlSerializer.SerializeContext;
            if (sc != null && c != null)
            {
                Type objType = obj.GetType();

                //REMOVED: This won't hurt anything here but the serializer won't make use of it.
                //It was too complicated of a mess to change the ObjectCache system to track
                //these kinds of changes. Left it here in case someone decided to
                //work on this feature themselves.
                /*
                //In this case, we want to replace the element created by the serializer
                //with one that was created by this surrogate. That way we can name it
                //after the component.
                //var element = sc.Doc.CreateElement(objType.Name);
                //sc.Element = element;
                */

                //UPDATE: This is actually usless info right now and can really screw things up if
                //the deserializer tries to process it.
                /*
                //Keep a reference to the GameObject to which this component is attached.
                //We might need it for during deserialization.
                info.AddValue("gameObject", c.gameObject, typeof(GameObject));
                */

                //If this component has an 'enabled' property, we'll want to save that.
                PropertyInfo pi = objType.GetProperty("enabled");
                if (pi != null) info.AddValue("enabled", (bool)pi.GetValue(obj, null));

                //prepares all viably serializable fields using reflection
                GatherFields(c, info, sc.Serializer);

                //REMOVED: This tends to add too much info.
                //The 'enabled' property is handled above manually.
                //PrepareCommonProperties(c, info, sc.Serializer);
            }
        }

        /// <summary>
        /// Helper method that prepares info that is typcially serialized for
        /// components
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        protected static void GatherFieldsAndProps(object obj, SerializationInfo info, StreamingContext context, params string[] filter)
        {
            if (SerializerBase.IsReferenceNull(obj)) return;// throw new NullReferenceException();
            var sc = context.Context as XmlSerializer.SerializeContext;
            if (sc != null)
            {
                //prepares all viably serializable fields using reflection
                GatherFields(obj, info, sc.Serializer, filter);
                GatherProperties(obj, info, sc.Serializer, filter);
            }
        }

        /// <summary>
        /// Helper method that prepares a complex object's
        /// fields that are typcially serialized.
        /// </summary>
        /// <remarks>
        /// Usually it is unecessary to call this as it gets 
        /// called by <see cref="ComponentSurrogate.PreparComponentFields(object, SerializationInfo)"/>.
        /// </remarks>
        /// <param name="obj"></param>
        /// <param name=""></param>
        protected static void GatherFields(object obj, SerializationInfo info, XmlSerializer serializer, params string[] filter)
        {
            //TODO: we need another version of this method that allows us to specify additional fields to add
            if (SerializerBase.IsReferenceNull(obj)) return;// throw new NullReferenceException();
            Type objType = obj.GetType();

            //Use reflection to get a list of all viably serializable fields.
            bool skip;
            IDictionary<string, FieldInfo> fields = serializer.GetTypeFieldInfo(objType);
            foreach (KeyValuePair<string, FieldInfo> kv in fields)
            {
                skip = false;
                if(filter != null)
                {
                    for(int i = 0; i < filter.Length; i++)
                    {
                        if (kv.Key == filter[i]) skip = true;
                    }
                }
                if (!skip)
                {
                    //we always want to see if the type is a complex type first, otherwise implicit casts might cause it to change to the wrong thing
                    if (kv.Value.GetType().IsClass || kv.Value.GetType().IsAnsiClass) info.AddValue(kv.Key, (object)kv.Value.GetValue(obj) as object);
                    else info.AddValue(kv.Key, kv.Value.GetValue(obj));
                }
            }
        }
        
        /// <summary>
        /// Helper method that prepares a complex object's
        /// fields that are typcially serialized. It also allows
        /// the caller to supply a list of additional fields that
        /// must be serialized regardless of status.
        /// </summary>
        /// <remarks>
        /// Usually it is unecessary to call this as it gets 
        /// called by <see cref="ComponentSurrogate.PreparComponentFields(object, SerializationInfo)"/>.
        /// </remarks>
        /// <param name="obj"></param>
        /// <param name=""></param>
        protected static void GatherForcedFields(object obj, SerializationInfo info, XmlSerializer serializer, params string[] forced)
        {
            //TODO: we need another version of this method that allows us to specify additional fields to add
            if (SerializerBase.IsReferenceNull(obj)) return;// throw new NullReferenceException();
            Type objType = obj.GetType();

            //Use reflection to get a list of all viably serializable fields.
            IDictionary<string, FieldInfo> fields = serializer.GetTypeFieldInfo(objType, forced);
            foreach (KeyValuePair<string, FieldInfo> kv in fields)
            {
                //we always want to see if the type is a complex type first, otherwise implicit casts might cause it to change to the wrong thing
                if (kv.Value.GetType().IsClass || kv.Value.GetType().IsAnsiClass) info.AddValue(kv.Key, (object)kv.Value.GetValue(obj) as object);
                else info.AddValue(kv.Key, kv.Value.GetValue(obj));
            }
        }

        /// <summary>
        /// Helper method that prepares a complex object's
        /// properties that are typcially serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="serializer"></param>
        protected static void GatherProperties(object obj, SerializationInfo info, XmlSerializer serializer, params string[] filter)
        {
            if (SerializerBase.IsReferenceNull(obj)) return;// throw new NullReferenceException();
            Type objType = obj.GetType();

            //Use reflection to get a list of all viably serializable fields.
            bool skip;
            IDictionary<string, PropertyInfo> fields = serializer.GetTypePropertyInfo(objType);
            foreach (KeyValuePair<string, PropertyInfo> kv in fields)
            {
                skip = false;
                if (filter != null)
                {
                    for (int i = 0; i < filter.Length; i++)
                    {
                        if (kv.Key == filter[i]) skip = true;
                    }
                }
                if (!skip)
                {
                    //we always want to see if the type is a complex type first, otherwise implicit casts might cause it to change to the wrong thing
                    if (kv.Value.GetType().IsClass || kv.Value.GetType().IsAnsiClass) info.AddValue(kv.Key, (object)kv.Value.GetValue(obj, null) as object);
                    else info.AddValue(kv.Key, kv.Value.GetValue(obj, null));
                }
            }
        }
    }
}
