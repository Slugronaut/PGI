/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using Toolbox.Graphics;

namespace PowerGridInventory
{
    /// <summary>
    /// Attach to a GameObject that will represent a Drag Icon for a view.
    /// This component is more-or-less just a convienience holder for 
    /// references to child object that hold image data.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(PowerGridInventory.Utility.IgnoreUIRaycasts))]
    public class DragIcon : MonoBehaviour
    {
        [HideInInspector]
        public Image Icon;
        [HideInInspector]
        public Image3D Icon3D;

        public enum ActiveIcon
        {
            None,
            Icon2D,
            Icon3D,
        }

        void Awake()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        /// <summary>
        /// Sets the icon that is to be displayed.
        /// </summary>
        /// <param name="state"></param>
        public void SetIconActive(ActiveIcon state)
        {
            switch(state)
            {
                case ActiveIcon.Icon2D:
                    {
                        gameObject.SetActive(true);
                        Icon.enabled = true;
                        Icon3D.enabled = false;
                        break;
                    }
                case ActiveIcon.Icon3D:
                    {
                        gameObject.SetActive(true);
                        Icon.enabled = false;
                        Icon3D.enabled = true;
                        break;
                    }
                default:
                    {
                        Icon.enabled = false;
                        Icon3D.enabled = false;
                        gameObject.SetActive(false);
                        break;
                    }
                    
            }
        }


    }
}