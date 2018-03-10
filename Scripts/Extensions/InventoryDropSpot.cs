/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using PowerGridInventory;
using UnityEngine.EventSystems;

namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// Attach this to a UI element and attach it's OnDrop handler to a
    /// <see cref="PGIView.OnDragEndInvalid"/> event to make it a target
    /// for dropping items into another specified inventory.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryDropSpot : MonoBehaviour
    {
        /// <summary>
        /// The inventory that should receive the item that was dragged onto this UI element.
        /// </summary>
        [Tooltip("The inventory that should receive the item that was dragged onto this UI element.")]
        public PGIModel DestInventory;

        /// <summary>
        /// A handler that can be attached to a <see cref="PGIView.OnDragEndInvalid"/> event.
        /// </summary>
        /// <param name="data">The pointer event that was triggered.</param>
        /// <param name="item">The item being dropped.</param>
        /// <param name="returnSlot">The slot the item was returned to due to an invalid target.</param>
        /// <param name="dropTarget">The GameObject containing the UI element that was the drop target.</param>
        public void OnDrop(PointerEventData data, PGISlotItem item, PGISlot returnSlot, GameObject dropTarget)
        {
            if (DestInventory != null && item != null && dropTarget != null && dropTarget == this.gameObject)
            {
                if (DestInventory.CanStoreAnywhere(item, DestInventory.AutoEquip, DestInventory.AutoStack, null))
                {
                    var oldModel = item.Model;
                    if (oldModel.Drop(item))
                        if (!DestInventory.Pickup(item)) oldModel.Pickup(item);
                }
            }
            //TODO: we should trigger some kind of 'consume' or 'use' trigger here
        }


    }
}
