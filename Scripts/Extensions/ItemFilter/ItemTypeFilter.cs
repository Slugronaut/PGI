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
    public sealed class ItemTypeFilter : MonoBehaviour
    {
        [Tooltip("The required ids of an item for it to be storable in this slot.")]
        public HashedString[] AllowedIds;


        void OnEnable()
        {
            GetComponent<PGISlot>().OnCanStoreItem.AddListener(CanStore);
        }

        void OnDisable()
        {
            GetComponent<PGISlot>().OnCanStoreItem.RemoveListener(CanStore);
        }

        void CanStore(UnityAction onFailed, PGISlotItem item, PGISlot dest)
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
