#define UNITY_IOS
/**********************************************
* Ancient Craft Games
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using Toolbox.Graphics;

namespace Toolbox.ToolboxEditor
{
    /// <summary>
    /// Custom inspector for the Image3D UI element.
    /// </summary>
    [CustomEditor(typeof(Image3D))]
    [CanEditMultipleObjects]
    public class Image3DEditor : AbstractSuperEditor { }
}
