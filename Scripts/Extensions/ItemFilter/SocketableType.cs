/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using Toolbox.Common;

namespace PowerGridInventory.Extensions.ItemFilter
{
    /// <summary>
    /// Simple example of an item type identifier that can be
    /// used by an a socketable item. <see cref="SocketedTypeFilter"/>
    /// to determine what can and can't be equipped to it.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Item Filter/Socketable Item Type")]
    public class SocketableType : MonoBehaviour
    {
        public HashedString TypeName = new HashedString("Some Type");

    }
}