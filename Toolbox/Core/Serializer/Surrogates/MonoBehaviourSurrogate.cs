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
    /// any kind of Unity component. You should derive
    /// from this class when special-case handling is
    /// required for custom MonoBehaviours.
    /// 
    /// By default this surrogate only serialized fields
    /// and leaves properties alone.
    /// </summary>
    public class MonoBehaviourSurrogate : ComponentSurrogate
    {
        /// <summary>
        /// Collects all fields and properties that will be serialized.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            //In the case of MonoBehaviours we are just
            //going to grab the fields and not the properties.
            GatherMonoBehaviourFields(obj, info, context);
        }
        
        
        
    }
}
