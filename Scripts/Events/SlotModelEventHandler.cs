/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using UnityEngine.Events;


namespace PowerGridInventory.Extensions.ItemFilter
{
    /// <summary>
    /// Attach this to a slot to allow it to react ti model events.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Slot Model Event Handler")]
    [RequireComponent(typeof(PGISlot))]
    public class SlotModelEventHandler : AbstractSlotEventHandler
    {
        public ModelFailableEvent OnCanStore = new ModelFailableEvent();
        public ModelFailableEvent OnCanRemove = new ModelFailableEvent();
        public ModelUpdateEvent OnStore = new ModelUpdateEvent();
        public ModelUpdateEvent OnRemove = new ModelUpdateEvent();
        public ModelUpdateEvent OnStoreFailed = new ModelUpdateEvent();


        public override void ModelCanStore(UnityAction onFailed, PGISlotItem item, CellModel cell)
        {
            OnCanStore.Invoke(onFailed, item, cell);
        }

        public override void ModelCanRemove(UnityAction onFailed, PGISlotItem item, CellModel cell)
        {
            OnCanRemove.Invoke(onFailed, item, cell);
        }

        public override void ModelStore(PGISlotItem item, CellModel cell)
        {
            OnStore.Invoke(item, cell);
        }

        public override void ModelRemove(PGISlotItem item, CellModel cell)
        {
            OnRemove.Invoke(item, cell);
        }

        public override void ModelStoreFailed(PGISlotItem item, CellModel cell)
        {
            OnStoreFailed.Invoke(item, cell);
        }

    }


    /// <summary>
    /// Base class for deriving event handlers that can be attached directly to <see cref="PGISlot"/>s.
    /// </summary>
    [RequireComponent(typeof(PGISlot))]
    public abstract class AbstractSlotEventHandler : MonoBehaviour, IModelEventHandler
    {
        public enum EventType
        {
            View = 1,
            Model = 2,
            Both = View | Model,
        }

        public EventType EventsToHandle = EventType.View;
        CellModel CachedCell;

        public AbstractBaseModelEventHandler.CellType Type { get { return CachedCell.IsEquipmentCell ? AbstractBaseModelEventHandler.CellType.Equipment : AbstractBaseModelEventHandler.CellType.Grid; } }

        public int EquipmentIndex { get { return CachedCell.EquipIndex; } }

        public int GridX { get { return CachedCell.xPos; } }

        public int GridY { get { return CachedCell.yPos; } }

        public void Register(CellModel cell)
        {
            if (cell != null && cell.Model != null)
            {
                CachedCell = cell;
                cell.Model.OnCanStoreItem.AddListener(InnerModelCanStore);
                cell.Model.OnCanRemoveItem.AddListener(InnerModelCanRemove);
                cell.Model.OnStoreItem.AddListener(InnerModelStore);
                cell.Model.OnStoreItemFailed.AddListener(InnerModelStoreFailed);
                cell.Model.OnRemoveItem.AddListener(InnerModelRemove);
            }
        }

        public void Unregister()
        {
            var cell = CachedCell;
            if (cell != null && cell.Model != null)
            {
                cell.Model.OnCanStoreItem.RemoveListener(InnerModelCanStore);
                cell.Model.OnCanRemoveItem.RemoveListener(InnerModelCanRemove);
                cell.Model.OnStoreItem.RemoveListener(InnerModelStore);
                cell.Model.OnStoreItemFailed.RemoveListener(InnerModelStoreFailed);
                cell.Model.OnRemoveItem.RemoveListener(InnerModelRemove);
            }
            CachedCell = null;
        }

        void RegisterSlot(PGISlot slot)
        {
            slot.OnCanStoreItem.AddListener(InnerViewCanStore);
            slot.OnCanRemoveItem.AddListener(InnerViewCanRemove);
            slot.OnStoreItem.AddListener(InnerViewStore);
            slot.OnRemoveItem.AddListener(InnerViewRemove);
            slot.OnStoreItemFailed.AddListener(InnerViewStoreFailed);

            Register(slot.CorrespondingCell);
        }

        void UnRegisterSlot(PGISlot slot)
        {
            slot.OnCanStoreItem.RemoveListener(InnerViewCanStore);
            slot.OnCanRemoveItem.RemoveListener(InnerViewCanRemove);
            slot.OnStoreItem.RemoveListener(InnerViewStore);
            slot.OnRemoveItem.RemoveListener(InnerViewRemove);
            slot.OnStoreItemFailed.RemoveListener(InnerViewStoreFailed);

            Unregister();
        }

        void OnEnable()
        {
            RegisterSlot(GetComponent<PGISlot>());
        }

        void OnDisable()
        {
            UnRegisterSlot(GetComponent<PGISlot>());
        }

        #region View Event Handlers
        void InnerViewCanStore(UnityAction onFailed, PGISlotItem item, PGISlot slot)
        {
            if ((EventsToHandle & EventType.View) != 0)
                ViewCanStore(onFailed, item, slot);

        }

        void InnerViewCanRemove(UnityAction onFailed, PGISlotItem item, PGISlot slot)
        {
            if ((EventsToHandle & EventType.View) != 0)
                ViewCanRemove(onFailed, item, slot);
        }

        void InnerViewStore(PGISlotItem item, PGISlot dest, PGISlot src)
        {
            if ((EventsToHandle & EventType.View) != 0)
                ViewStore(item, dest, src);
        }

        void InnerViewRemove(PGISlotItem item, PGISlot current)
        {
            if ((EventsToHandle & EventType.View) != 0)
                ViewRemove(item, current);
        }

        void InnerViewStoreFailed(PGISlotItem item, PGISlot dest, PGISlot src)
        {
            if ((EventsToHandle & EventType.View) != 0)
                ViewStoreFailed(item, dest, src);
        }
        #endregion


        #region model event handlers
        protected virtual void InnerModelCanStore(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest == CachedCell && dest != null)
                ModelCanStore(onFailed, item, dest);
        }

        protected virtual void InnerModelCanRemove(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest == CachedCell && dest != null)
                ModelCanRemove(onFailed, item, dest);
        }

        protected virtual void InnerModelStore(PGISlotItem item, CellModel dest)
        {
            if (dest == CachedCell && dest != null)
                ModelStore(item, dest);
        }

        protected virtual void InnerModelStoreFailed(PGISlotItem item, CellModel dest)
        {
            if (dest == CachedCell && dest != null)
                ModelStoreFailed(item, dest);
        }

        protected virtual void InnerModelRemove(PGISlotItem item, CellModel dest)
        {
            if (dest == CachedCell && dest != null)
                ModelRemove(item, dest);
        }
        #endregion

        public virtual void ViewCanStore(UnityAction onFailed, PGISlotItem item, PGISlot slot) { }
        public virtual void ViewCanRemove(UnityAction onFailed, PGISlotItem item, PGISlot slot) { }
        public virtual void ViewStore(PGISlotItem item, PGISlot dest, PGISlot src) { }
        public virtual void ViewRemove(PGISlotItem item, PGISlot current) { }
        public virtual void ViewStoreFailed(PGISlotItem item, PGISlot dest, PGISlot src) { }

        public virtual void ModelCanStore(UnityAction onFailed, PGISlotItem item, CellModel cell) { }
        public virtual void ModelCanRemove(UnityAction onFailed, PGISlotItem item, CellModel cell) { }
        public virtual void ModelStore(PGISlotItem item, CellModel cell) { }
        public virtual void ModelRemove(PGISlotItem item, CellModel cell) { }
        public virtual void ModelStoreFailed(PGISlotItem item, CellModel cell) { }

    }
}
