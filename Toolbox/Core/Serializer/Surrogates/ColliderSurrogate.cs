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
    /// colliders. Specifically, it checks the state of
    /// </summary>
    public class Collider2DSurrogate : UnityEngineObjectSurrogate
    {
        public override void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            if (obj == null) return;

            //we are doing this to avoid annoying errors about setting
            //density with no auto-mass during deserialization
            Collider2D col = obj as Collider2D;
            var body2D = col.gameObject.GetComponent<Rigidbody2D>();
            if (body2D == null || !body2D.useAutoMass)
                GatherFieldsAndProps(obj, info, context, "material", "density");
            else GatherFieldsAndProps(obj, info, context, "material");

        }

    }
}
