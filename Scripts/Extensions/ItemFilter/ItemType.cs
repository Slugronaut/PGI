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
    /// used by an equipment slot that uses an <see cref="ItemTypeFilter"/>
    /// to determine what can and can't be equipped to it.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Item Filter/Item Type")]
    public class ItemType : MonoBehaviour
    {
        public HashedString TypeName = new HashedString("Some Type");

    }
}