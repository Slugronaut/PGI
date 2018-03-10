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
    /// required for certain Unity components.
    /// </summary>
    public class ComponentSurrogate : UnityEngineObjectSurrogate
    { 
        //Weee! Don't need to do anything special. The UnityEngine.Object surrogate
        //already handles it for us! It's like running naked through the woods!
    }
}
