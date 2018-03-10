/**********************************************
* Ancient Craft Games
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.UI;

namespace Toolbox.Graphics
{
    /// <summary>
    /// Utility class for handling various UI actions.
    /// </summary>
    public static class UIUtility
    {
        /// <summary>
        /// Disables all Unity UI elements attached to a GameObject.
        /// </summary>
        /// <param name="go">The GameObject to disable UI elements on.</param>
        /// <param name="recursive">If <c>true</c>, all GameObjects within the supplied
        /// GameObject hierarchy will also have their UI elemnents disabled.</param>
        /// <param name="enabledState">A flag that is used to set the enabled state of all found UI elements.</param>
        public static void SetUIElementsState(GameObject go, bool recursive, bool enabledState)
        {
            foreach (var element in go.GetComponents<Graphic>())
                element.enabled = enabledState;
            if (recursive)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    SetUIElementsState(go.transform.GetChild(i).gameObject, recursive, enabledState);
                }
            }
        }
    }
}