/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.EventSystems;

namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// Attach to the root of an item that should provide its
    /// own storage through the use of a <see cref="PGIModel"/> that is also
    /// attached to this GameObject. Useful for bags, chests, and
    /// containers of various sorts.
    /// <remarks>
    /// This component is designed accept double-clicks to open a view
    /// of its storage as well as manage and avoid the posibilities of
    /// inserting this item into its own storage and closing the
    /// view when dropped.
    /// </remarks>
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Inventory Item", 10)]
    [RequireComponent(typeof(PGIModel))]
    public class InventoryItem : MonoBehaviour
    {
        /// <summary>
        /// Reference to the <see cref="PGIView"/>
        /// that displays this item's storage <see cref="PGIModel"/>.
        /// </summary>
        [Tooltip("The GameObject to which this storage's PGIView is attached. It wil be activated and de-activated by double-clicking the item.")]
        public GameObject ContainerPanel;

        /// <summary>
        /// Opens the container if we right-click on it.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        /// <param name="slot">Slot.</param>
        public void OnOpenStorage(PointerEventData eventData, PGISlot slot)
        {
            if (ContainerPanel != null && eventData.clickCount > 1)// eventData.button == PointerEventData.InputButton.Right)
            {
                ContainerPanel.SetActive(!ContainerPanel.activeSelf);
            }
        }

        /// <summary>
        /// Makes sure we close the container if we remove it from our inventory.
        /// </summary>
        /// <param name="item">The item being equipped.</param>
        /// <param name="model">The model the item is being equipped within.</param>
        public void OnDrop(PGISlotItem item, PGIModel model)
        {
            //POINT OF INTERNEST: The view being closed would normally return the DraggedItem (i.e. this item)
            //to it's model when it is disabled. However, a slight delay in the view's
            //internal OnDisable() method allows us the time we need to actually drop
            //this item and finish up processing. 
            ContainerPanel.SetActive(false);
            
        }

        /// <summary>
        /// Used by OnCanStore event of this gameobject's PGISlotItem component.
        /// Makes sure that we don't store this item in it's own inventory.
        /// </summary>
        /// <returns><c>true</c> if this instance can store the specified item model; otherwise, <c>false</c>.</returns>
        /// <param name="item">The item being equipped.</param>
        /// <param name="model">The model the item is being equipped within.</param>
        public void CanStore(PGISlotItem item, PGIModel model)
        {
            //make sure we aren't trying to store this container in itself
            PGIModel storage = GetComponent<PGIModel>();

            if (storage == model)
            {
                model.CanPerformAction = false;
            }
        }

        /// <summary>
        /// Used by OnCanEquip event of this gameobject's PGISlotItem component.
        /// Ensures that we don't equip this item to it's own inventory.
        /// </summary>
        /// <returns><c>true</c> if this instance can store the specified item model; otherwise, <c>false</c>.</returns>
        /// <param name="item">The item being equipped.</param>
        /// <param name="model">The model the item is being equipped within.</param>
        /// <param name="slot">The equipment slot the item is being equipped to.</param>
        public void CanEquip(PGISlotItem item, PGIModel model, PGISlot slot)
        {
            //make sure we aren't trying to store this container in itself
            PGIModel storage = GetComponent<PGIModel>();
            if (storage == model)
            {
                model.CanPerformAction = false;
            }
        }

    }
}