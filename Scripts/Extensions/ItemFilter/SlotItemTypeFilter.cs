/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using Toolbox.Common;
using UnityEngine.Events;


namespace PowerGridInventory.Extensions.ItemFilter
{
    /// <summary>
    /// Component used for handling item type filtering in a
    /// Grid Inventory equipment slot.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Item Filter/Item Type Filter")]
    [RequireComponent(typeof(PGISlot))]
    public sealed class SlotItemTypeFilter : AbstractSlotEventHandler
    {
        [Tooltip("The required ids of an item for it to be storable in this slot.")]
        public HashedString[] AllowedIds;

        

        public override void ViewCanStore(UnityAction onFailed, PGISlotItem item, PGISlot slot)
        {
            if (slot != null && slot.Model != null)
            {
                if (!TestFilter(item))
                    onFailed();
            }
            else onFailed();
        }

        public override void ModelCanStore(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest != null && dest.Model != null)
            {
                if (!TestFilter(item))
                    onFailed();
            }
            else onFailed();
        }


        bool TestFilter(PGISlotItem item)
        {
            //filter out what can and can't be stored
            if (AllowedIds != null && AllowedIds.Length > 0)
            {
                var type = item.GetComponent<ItemType>();
                if (type == null) return false;

                if (HashedString.DoNotContain(AllowedIds, type.TypeName.Hash))
                    return false;
            }

            return true;
        }
    }
    
}
