/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System;
using System.Collections.Generic;

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Handles the data preperation for 
    /// serializing <see cref="UnityEngine.Transform"/> components.
    /// </summary>
    public class TransformSurrogate : ComponentSurrogate
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            //No need for reflection here, we know exactly what we want to store.
            //Collect Transform-specific data: position, rotation, scale, and children.
            Transform t = obj as Transform;
            if (t != null)
            {
                List<GameObject> children = null;
                if (t.childCount > 0)
                {
                    children = new List<GameObject>();
                    for (int i = 0; i < t.childCount; i++)
                    {
                        children.Add(t.GetChild(i).gameObject);
                    }
                }
                
                info.AddValue("localPosition", t.localPosition);
                info.AddValue("localRotation", t.localRotation);
                info.AddValue("localScale", t.localScale);
                info.AddValue("Children", children);
            }
        }

        /// <summary>
        /// Sets all fields that have been deserialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            object o = base.SetObjectData(obj, info, context, selector);
            
            //Restore Transform-specific data: position, rotation, scale, and children.
            Transform t = o as Transform;
            if (t != null)
            {
                List<GameObject> gos = (List<GameObject>)info.GetValue("Children", typeof(List<GameObject>));
                if(gos != null)
                {
                    foreach (var go in gos) go.transform.SetParent(t.transform, false);
                }

                t.localPosition = (Vector3)info.GetValue("localPosition", typeof(Vector3));
                t.localRotation = (Quaternion)info.GetValue("localRotation", typeof(Quaternion));
                t.localScale = (Vector3)info.GetValue("localScale", typeof(Vector3));

            }
            return o;
        }
    }
}