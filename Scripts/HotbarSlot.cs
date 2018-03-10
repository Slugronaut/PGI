/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace PowerGridInventory
{
    /// <summary>
    /// When an item from another slot is dropped to this one, that item
    /// remains in it's original slot whist this slot gains a reference to it.
    /// This allows for 'hotbars' that can have items dropped into them without
    /// removing them from their original location.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Power Grid Inventory/Hotbar Slot", 15)]
    [Serializable]
    public class HotbarSlot : PGISlot
    {
        PGISlot LinkedSlot;
        CellModel OriginalCell;

        /// <summary>
        /// Triggered when this hotbar looses its link to another slots contents.
        /// </summary>
        public ViewUpdateEvent OnLink = new ViewUpdateEvent();

        /// <summary>
        /// Triggered when this hotbar links to another slot's contents.
        /// </summary>
        public ViewUpdateEvent OnUnlink = new ViewUpdateEvent();


        public override CellModel CorrespondingCell
        {
            get
            {
                return base.CorrespondingCell;
            }

            set
            {
                if (OriginalCell == null) OriginalCell = value;
                base.CorrespondingCell = value;
            }
        }
        
        public override void UpdateView(bool updateHighlighting)
        {
            if (IsLinked)
                CorrespondingCell = LinkedSlot.CorrespondingCell;
            base.UpdateView(updateHighlighting);
            CorrespondingCell = OriginalCell;
        }

        public bool IsLinked { get { return LinkedSlot != null; } }

        /// <summary>
        /// This points to the source cell, if any, that this hotbar slot has been linked to.
        /// </summary>
        public CellModel LinkedCell
        {
            get { return LinkedSlot == null ? null : LinkedSlot.CorrespondingCell; }
        }


        protected override void Awake()
        {
            base.Awake();
            SkipAutoEquip = true;
            OnStoreItem.AddListener(HandleMove);
            OnCanStoreItem.AddListener(CanHandleMove);
        }

        void CanHandleMove(UnityAction onFailed, PGISlotItem item, PGISlot dest)
        {
            //don't allow linking through directly setting. Must have a source slot
            if(dest == null || dest.CorrespondingCell == null)
            {
                onFailed();
                return;
            }
        }
        
        /// <summary>
        /// Overrides the base implementation and feeds the item back to the source if any.
        /// This allows us to keep the item in both places at once.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="source"></param>
        void HandleMove(PGISlotItem item, PGISlot dest, PGISlot src)
        {
            if (dest == this)
            {
                var srcModel = src.CorrespondingCell;
                if (srcModel != null)
                {
                    //place the item back into the source it came from
                    PGIModel.RemoveItem(item, false);
                    PGIModel.StoreItem(item, srcModel, false);

                    //link ourself to the source
                    OriginalCell.Item = null;
                    LinkedSlot = src;
                    src.OnRemoveItem.AddListener(HandleItemDisappeared);

                    OnLink.Invoke(item, this);
                }
            }
            else CheckForDisconnect(item);
        }

        void HandleItemDisappeared(PGISlotItem item, PGISlot dest)
        {
            if (dest == LinkedSlot)
                CheckForDisconnect(item);
        }

        public void CheckForDisconnect(PGISlotItem item)
        {
            if (LinkedSlot != null)
            {
                //reset if dest not us and item is no longer in our reference spot
                LinkedSlot.OnRemoveItem.RemoveListener(HandleItemDisappeared);
                LinkedSlot = null;
                OnUnlink.Invoke(item, this);
            }
        }

    }
}