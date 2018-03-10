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
    /// Aids in serialization of C# delegates (MulticastDelegate).
    /// </summary>
    public class DelegateSurrogate : ISerializationSurrogate
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            MulticastDelegate del = obj as MulticastDelegate;
            var sc = context.Context as XmlSerializer.SerializeContext;
            if (sc != null && obj != null && del != null)
            {
                //we have to remove 'this' delegate from it's own invokcation list
                //otherwise we'll infinitely recurse when trying to serialize.
                List<Delegate> list = new List<Delegate>(del.GetInvocationList());
                list.Remove(del);//this doesn't seem to have an effect :/
                info.AddValue("Target", del.Target, typeof(object));
                info.AddValue("Method", del.Method.Name, typeof(string));
                info.AddValue("InvocationList", list, typeof(List<Delegate>));

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            XmlDeserializer.DeserializeContext dc = context.Context as XmlDeserializer.DeserializeContext;
            object target = info.GetValue("Target", typeof(object));
            string method = info.GetValue("Method", typeof(string)) as string;
            MulticastDelegate delObj = MulticastDelegate.CreateDelegate(dc.ObjectType, target, method) as MulticastDelegate;
            List<Delegate> invokeList = info.GetValue("InvocationList", typeof(List<Delegate>)) as List<Delegate>;

            if (invokeList.Count > 0) return MulticastDelegate.Combine(invokeList.ToArray()) as MulticastDelegate;
            else return delObj;
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
        protected static void PrepareCommonFields(object obj, SerializationInfo info, XmlSerializer serializer)
        {
            Type objType = obj.GetType();

            //Use reflection to get a list of all viably serializable fields.
            IDictionary<string, FieldInfo> fields = serializer.GetTypeFieldInfo(objType);
            foreach (KeyValuePair<string, FieldInfo> kv in fields)
            {
                info.AddValue(kv.Key, kv.Value.GetValue(obj));
            }
        }

        /// <summary>
        /// Helper method that prepares a complex object's
        /// properties that are typcially serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="serializer"></param>
        protected static void PrepareCommonProperties(object obj, SerializationInfo info, XmlSerializer serializer)
        {
            Type objType = obj.GetType();

            //Use reflection to get a list of all viably serializable properties.
            IDictionary<string, PropertyInfo> props = serializer.GetTypePropertyInfo(objType);
            foreach (KeyValuePair<string, PropertyInfo> kv in props)
            {
                info.AddValue(kv.Key, kv.Value.GetValue(obj, null));
            }
        }

        
    }
}
