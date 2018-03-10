/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Interface that exposes the ability to delegate activation (construction)
    /// on an object to another. The deserializer can use this to activate objects
    /// that might not have a default parameterless constructor.
    /// </summary>
    public interface IActivationSurrogate
    {
        object ActivateInstance();
    }
}