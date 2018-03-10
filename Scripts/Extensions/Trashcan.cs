/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.Events;

namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// Trashcan event handler for a Grid Inventory slot.
    /// When an item is equipped to this slot it will be removed
    /// from its inventory as though it were dropped.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Trashcan", 12)]
    [RequireComponent(typeof(PGISlot))]
    public class Trashcan : MonoBehaviour
    {
        public PGISlotItem.ItemEvent OnTrashed = new PGISlotItem.ItemEvent();

        void Start()
        {
            //Turn off auto-equip for this slot,
            //otherwise we'll be sending items straight
            //to the trash when they enter the inventory!
            PGISlot slot = GetComponent<PGISlot>();

            //make sure we don't try to trash items
            //as soon as they are picked up.
            slot.SkipAutoEquip = true;
            slot.OnStoreItem.AddListener(TrashItem);
        }

        void OnEnable()
        {
            GetComponent<PGISlot>().View.OnViewUpdated.AddListener(HandleViewUpdate);
        }

        void OnDisable()
        {
            PGISlot slot = GetComponent<PGISlot>();
            if (slot.View != null)
                slot.View.OnViewUpdated.RemoveListener(HandleViewUpdate);
        }

        void HandleViewUpdate(PGIView view)
        {
            if(view.Model != null)
            {
                var slot = GetComponent<PGISlot>();
                var cell = slot.CorrespondingCell;
                if (cell != null)
                    cell.SkipAutoEquip = slot.SkipAutoEquip;
            }
        }

        public void TrashItem(PGISlotItem item, PGISlot dest, PGISlot notUsed)
        {
            if (dest == null || dest == dest.Model) return;
            TrashItem(item, dest.Model, dest);
        }

        public void TrashItem(PGISlotItem item, PGIModel inv, PGISlot slot)
        {
            //This helper method will handle all of the magic for us.
            //It makes sure the item is unequipped and removed from the inventory
            //and triggers all of the necessary events.
            inv.Drop(item);
            PGIModel.PostMovementEvents(item, null, slot.CorrespondingCell.Model); //this is a workaround for the fact that the item doesn't have a model currently due to the way drag n drop works
        }

    }
}