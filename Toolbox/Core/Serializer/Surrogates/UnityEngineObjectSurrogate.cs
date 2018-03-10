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
    /// Handles the data preperation for serializing
    /// any kind of UnityEngine.Object. You should derive
    /// from this class when special-case handling is required.
    /// </summary>
    public class UnityEngineObjectSurrogate : SurrogateBase
    {
        /// <summary>
        /// Collects all fields that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;
            //attempt to serialize a resource path first
            if (!SerializeAsResource(obj, info, context))
            {
                //serialize in place as a unityengine object.
                GatherFieldsAndProps(obj, info, context, 
                    "material",     //touching this causes instantiation, sharedMaterial is serialized instead
                    "materials",    //touching this causes instantiation, sharedMaterials is serialized instead
                    "mesh",         //touching this causes instantiation, sharedMesh is serialized instead
                    "useConeFriction");
                //NOITCE: The list of strings for the params[]
                //are properties and fields that should never be serialized.
                //These members always give bad results. We can also guarantee
                //that we won't try to deserialize them if they were never serialized.

                //However, if you ever pass in an object that is handled by this surrogate
                //and it has members with the same name, this might cause problems. In that
                //case it would be better to write another surrogate to handle that type.
            }
        }
        
        /// <summary>
        /// Sets all fields and properties that have been deserialized.
        /// </summary>
        /// <param name="obj">The GameObject that will be used to initialize this component.</param>
        /// <param name="info">The fields that were deserialized already.</param>
        /// <param name="context">A context container that stores a <see cref="XmlDeserializer.DeserializeContext"/> object within.</param>
        /// <param name="selector">Always null.</param>
        /// <returns>The component that was deserialized.</returns>
        public override object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            //see if we can deserialize this as a resource path
            object o = DeserializeAsResource(context.Context as XmlDeserializer.DeserializeContext);
            if (o != null) return o;

            //just deserialize as normal in-place object
            ReplaceState(obj, info);
            return obj;
        }
        
        
    }
}
