/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// 
    /// </summary>
    public class GameObjectSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            GameObject go = obj as GameObject;
            if (go != null)
            {
                //first, let's see if this is a prefab that needs to be serialized as a resource.
                if(go.GetComponent<PrefabId>() != null)
                {
                    if (SerializeAsResource(obj, info, context)) return;
                }

                info.AddValue("name", go.name);
                info.AddValue("tag", go.tag);
                info.AddValue("layer", go.layer);
                info.AddValue("isStatic", go.isStatic);
                info.AddValue("activeSelf", go.activeSelf);
                info.AddValue("Components", go.GetComponents<Component>());
            }
        }

        /// <summary>
        /// Sets all fields that have been deserialized.
        /// </summary>
        /// <param name="obj">This will always be null.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">The object that will help select the correct surrogate when
        /// we recursively deseriablize the components of this GameObject.</param>
        /// <returns>A newly generated GameObject.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            GameObject go = obj as GameObject;
            if (go != null)
            {
                //first, see if this was stored as a resource string for a prefab
                GameObject prefab = DeserializeAsResource(context.Context as XmlDeserializer.DeserializeContext) as GameObject;
                if(prefab != null)
                {
                    //we'll have to remove the GO that was passed to
                    //this method. It is no longer valid.
                    #if UNITY_EDITOR
                    if (Application.isPlaying) GameObject.Destroy(go);
                    else GameObject.DestroyImmediate(go);
                    #else
                    GameObject.Destroy(go);
                    #endif
                    return prefab;
                }

                go.name = (string)info.GetValue("name", typeof(string));
                go.tag = (string)info.GetValue("tag", typeof(string));
                go.layer = (int)info.GetValue("layer", typeof(int));
                go.isStatic = (bool)info.GetValue("isStatic", typeof(bool));
                go.SetActive((bool)info.GetValue("activeSelf", typeof(bool)));

                //This is where it alls goes down hill very very fast. We need to recursively
                //decompose these elements *here* using the Deserializer and xml element
                //that was passed in. We'll have to use the surrogate selector provided to
                //see what surrogate to use.
                XmlDeserializer.DeserializeContext dc = context.Context as XmlDeserializer.DeserializeContext;
                if (dc != null)
                {
                    //we need to store the component in the cache now otherwise we might
                    //not be able to reference it within itself.
                    if (dc.ObjId >= 0) dc.deserializationObjCache[dc.ObjId] = go;

                    //seek out the 'Components' node
                    for (XmlNode n = dc.ElementBeingParsed.FirstChild; n != null; n = n.NextSibling)
                    {
                        if (n.Name == "Components")
                        {
                            for (XmlNode node = n.FirstChild; node != null; node = node.NextSibling)
                            {
                                DeserializeComponent(go, node as XmlElement, dc, selector);
                            }
                            break;
                        }
                    }
                }
            }
            return go;
        }

        /// <summary>
        /// Parses, deserializes, and attaches an xml element that describes a component.
        /// </summary>
        protected void DeserializeComponent(GameObject go, XmlElement element, XmlDeserializer.DeserializeContext context, ISurrogateSelector selector)
        {
            //check for null value
            string value = element.GetAttribute("value");
            if (value == "null") return;

            //get object id if it has one
            int objId;
            if (int.TryParse(element.GetAttribute("id"), out objId))
            {
                //store object ref in cache with the given id.
                //Also add it to the defered list if it has a defered id.
            }
            else objId = -1;

            //int subItems = element.ChildNodes.Count;

            // get type            
            Type objType;
            string typeId = element.GetAttribute("typeid");
            if (string.IsNullOrEmpty(typeId))
            {
                // no type id so type information must be present
                objType = context.Deserializer.InferTypeFromElement(element);
            }
            else
            {
                // there is a type id present
                objType = context.deserializationTypeCache[Convert.ToInt32(typeId)];
            }

            if (objType == null)
            {
                Debug.LogWarning("No type info supplied for " + element.Name + ". Assigning null value.");
                return;
            }

            Component comp;
            if (objType == typeof(Transform)) comp = go.GetComponent<Transform>();
            else comp = go.AddComponent(objType);
            if (comp != null)
            {
                ISurrogateSelector temp;
                var surrogate = selector.GetSurrogate(objType, new StreamingContext(StreamingContextStates.File), out temp);
                if (surrogate != null)
                {
                    DeserializeSurrogate(surrogate, context, comp, objType, objId, element.FirstChild);
                }
            }
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="surrogate"></param>
        /// <param name="obj"></param>
        /// <param name="firstChild"></param>
        /// <returns></returns>
        protected object DeserializeSurrogate(ISerializationSurrogate surrogate, 
            XmlDeserializer.DeserializeContext context,
            Component comp, Type objType, int objId, 
            XmlNode firstChild)
        {
            SerializationInfo info = new SerializationInfo(objType, new DummyConverter());
            var cnt = new XmlDeserializer.DeserializeContext();
            cnt.Deserializer = context.Deserializer;
            cnt.ElementBeingParsed = (XmlElement)firstChild;
            cnt.ObjectType = objType;
            cnt.deserializationObjCache = context.deserializationObjCache;
            cnt.deserializationTypeCache = context.deserializationTypeCache;

            //we need to store the component in the cache now otherwise we might
            //not be able to reference it within itself.
            if (objId >= 0) context.deserializationObjCache[objId] = comp;


            //float through the xml nodes and see what items we need to collect.
            for (XmlNode node = firstChild; node != null; node = node.NextSibling)
            {
                if (cnt.Deserializer.CheckForDeferedDeserialization(comp, node.Name, (XmlElement)node))
                    continue;
                //Take Note! We are passing the component as the owner object. This is so that we
                //can defer deserialization of reference back to this gameObject since we haven't added
                //it to the cache just yet.
                object val = context.Deserializer.DeserializeCore((XmlElement)node, null);// comp);
                if (val != null) info.AddValue(node.Name, val, val.GetType());
                else info.AddValue(node.Name, null, typeof(object));
            }

            var obj = surrogate.SetObjectData(comp,
                info,
                new StreamingContext(StreamingContextStates.File, context),
                context.Deserializer);

            return obj;
        }
    }
}
