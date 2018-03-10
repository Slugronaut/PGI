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

namespace Pantagruel.Serializer.Surrogate
{
    /// <summary>
    /// Handles the data preperation for 
    /// serializing <see cref="UnityEngine.Transform"/> components.
    /// </summary>
    public class KeyValuePairSurrogate : ISerializationSurrogate
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null || info == null) return;

            Type objType = obj.GetType();
            if(objType != null)
            {
                //the stuff we need is private so we'll have to use reflection
                FieldInfo[] fieldInfo = objType.GetFields(BindingFlags.Instance |
                                                          BindingFlags.NonPublic |
                                                          BindingFlags.Public |
                                                          BindingFlags.DeclaredOnly);
                foreach(var field in fieldInfo)
                {
                    info.AddValue(field.Name, field.GetValue(obj), field.FieldType);
                }
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
        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            if (obj == null || info == null) return obj;

            Type objType = obj.GetType();
            if (objType != null)
            {
                //the stuff we need is private so we'll have to use reflection
                FieldInfo[] fieldInfo = objType.GetFields(BindingFlags.Instance |
                                                      BindingFlags.NonPublic |
                                                      BindingFlags.Public |
                                                      BindingFlags.DeclaredOnly);
                foreach (var field in fieldInfo)
                {
                    field.SetValue(obj, info.GetValue(field.Name, field.FieldType));
                }
            }
            return obj;
        }
    }
}