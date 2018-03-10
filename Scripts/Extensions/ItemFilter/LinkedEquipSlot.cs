/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections.Generic;
using Toolbox.Common;
using UnityEngine.Events;

namespace PowerGridInventory.Extensions.ItemFilter
{
    /// <summary>
    /// Use this script to disable other slots when one slot is equipped.
    /// Useful for things like two-handed weapons and such.
    /// <remarks>
    /// The linked slots will only become blocked if the item being equipped to
    /// this slot has an <see cref="ItemType"/> component atached and has
    /// an indentifier string that appears in this component's <see cref="LinkedEquipSlot.TypesThatUseMultiSlots"/> list.
    /// </remarks>
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Item Filter/Linked Equipment Slot", 11)]
    public class LinkedEquipSlot : MonoBehaviour
    {
        [Tooltip("A list of equipment slots that will become blocked when this slot is equipped with an item.")]
        public PGISlot[] LinkedSlots;

        [Tooltip("A list of item types identified with an ItemType component that will activate the linked slots when they are equipped to this slot.")]
        public List<string> TypesThatUseMultiSlots;

        public List<int> HashedTypes;

        void Start()
        {
            PGISlot slot = GetComponent<PGISlot>();
            if (slot != null)
            {
                slot.OnRemoveItem.AddListener(OnRemove);
                slot.OnStoreItem.AddListener(OnStore);
                slot.OnCanStoreItem.AddListener(CanStore);
            }


            HashedTypes = new List<int>(TypesThatUseMultiSlots.Count);
            for (int i = 0; i < TypesThatUseMultiSlots.Count; i++)
                HashedTypes.Add(HashedString.StringToHash(TypesThatUseMultiSlots[i]));

        }

        public void OnStore(PGISlotItem item, PGISlot slot, PGISlot notUsed)
        {
            if (!this.enabled) return;

            //Check to see if the equipped item has an ItemType component
            ItemType type = item.GetComponent<ItemType>();
            if (type != null && HashedTypes != null)//type.TypeName.Equals("Two-handed Weapon"))
            {
                //Does the item's type exist in this component's filter list?
                if (HashedTypes.Contains(type.TypeName.Hash))
                {
                    //It does. So we need to block
                    if (LinkedSlots != null)
                    {
                        foreach (PGISlot linked in LinkedSlots)
                        {
                            linked.Blocked = true;

                            //HACK ALERT:
                            //This is a work-around for a bug introduced with the advent of 3D mesh icons.
                            //This simply ensures the linked slot's default icon is restored as it should be.
                            linked.gameObject.SetActive(false);
                            linked.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        public void OnRemove(PGISlotItem item, PGISlot slot)
        {
            if (!this.enabled) return;

            if (LinkedSlots != null)
            {
                foreach (PGISlot linked in LinkedSlots)
                {
                    //Warning, we are making the assumption that nothing else
                    //had previously blocked this slot.

                    //HACK ALERT: We need to check for Blocked stat before changing it here
                    //due to the changes made for the 3D icon system and the highlight colors
                    //used by items when equipped to slots.
                    if (linked.Blocked) linked.Blocked = false;
                }
            }
        }

        public void CanStore(UnityAction onFailed, PGISlotItem item, PGISlot slot)
        {
            var inv = slot.Model;
            //If we are equipping on of the filter's types, then we need to see if
            //any of the linked slots are full or blocked.
            if (LinkedSlots != null)
            {
                ItemType type = item.GetComponent<ItemType>();
                if (type != null && HashedTypes != null)
                {
                    if (HashedTypes.Contains(type.TypeName.Hash))
                    {
                        foreach (PGISlot linked in LinkedSlots)
                        {
                            if (linked.Item != null || linked.Blocked)
                            {
                                //disallow equipping
                                onFailed();
                                return;
                            }
                        }
                    }
                }
            }

            //Either the item is not using multiple slots or, if it is,
            //the linked slots are all empty and unblocked. Good to go!
            return;
        }
    }
}