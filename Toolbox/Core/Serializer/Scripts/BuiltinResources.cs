/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Container for references to all unity built-in resources.
    /// This seems to be the only way to access unity's builtin 
    /// resources at runtime.
    /// </summary>
    public class BuiltinResources : MonoBehaviour
    {
        public List<Object> Resources;
        
        
    }
}