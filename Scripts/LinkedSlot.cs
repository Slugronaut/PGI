/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PowerGridInventory
{
    /// <summary>
    /// This acts like a normal slot for the user but it shares its data with another 'concrete' PGISlot
    /// that actually contains the item data. This allows for mutiple 'linked' slots that can point to the same
    /// source and all be updated at the same time when the item changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Power Grid Inventory/Linked Slot", 14)]
    [Serializable]
    public class LinkedSlot : PGISlot
    {
        [Tooltip("The Slot that will actually store the item. This hotbar will reflect its contents to match that slot.")]
        public PGISlot SourceSlot;

        protected override void Awake()
        {
            base.Awake();
            SkipAutoEquip = true;
        }
        
        public override CellModel CorrespondingCell
        {
            get { return SourceSlot == null ? null : SourceSlot.CorrespondingCell; }
            set
            {
                if (SourceSlot != null) SourceSlot.CorrespondingCell = value;
            }
        }
    }
    
}