/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;

namespace PowerGridInventory.Extensions.Tooltip
{

    /// <summary>
    /// Example component to attach to an item so that it can provide a
    /// description for a <see cref="HoveringItemDisplay"/>.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Tooltip/Tooltip Info")]
    public class TooltipInfo : MonoBehaviour
    {
        public string Name;
        [TextArea(3, 5)]
        public string Description;
    }
}
