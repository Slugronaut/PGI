using PowerGridInventory.Extensions.ItemFilter;
using Toolbox.Common;
using UnityEngine;
using UnityEngine.Events;

namespace PowerGridInventory
{
    
    /// <summary>
    /// Applies filters to a single CellModel of a PGIModel.
    /// </summary>
    public class ModelItemFilter : AbstractBaseModelEventHandler
    {
        [Tooltip("The required ids of an item for it to be storable in this slot.")]
        public HashedString[] AllowedIds;
        
        public override void CanStore(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (!TestFilter(item))
                onFailed();
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
    

    /// <summary>
    /// Base class for deriving custom model event handlers. This version provides
    /// UnityEvents in the inspector view.
    /// </summary>
    [RequireComponent(typeof(PGIModel))]
    public class AbstractModelEventHandler : AbstractBaseModelEventHandler, IModelEventHandler
    {
        public ModelFailableEvent OnCanStore = new ModelFailableEvent();
        public ModelFailableEvent OnCanRemove = new ModelFailableEvent();
        public ModelUpdateEvent OnStore = new ModelUpdateEvent();
        public ModelUpdateEvent OnRemove = new ModelUpdateEvent();
        public ModelUpdateEvent OnStoreFailed = new ModelUpdateEvent();

        protected override void CanStoreInternal(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
            {
                CanStore(onFailed, item, dest);
                OnCanStore.Invoke(onFailed, item, dest);
            }
        }

        protected override void CanRemoveInternal(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
            {
                CanRemove(onFailed, item, dest);
                OnCanRemove.Invoke(onFailed, item, dest);
            }
        }

        protected override void StoreInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
            {
                Store(item, dest);
                OnStore.Invoke(item, dest);
            }
        }

        protected override void StoreFailedInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
            {
                StoreFailed(item, dest);
                OnStoreFailed.Invoke(item, dest);
            }
        }

        protected override void RemoveInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
            {
                Remove(item, dest);
                OnRemove.Invoke(item, dest);
            }
        }
    }


    /// <summary>
    /// Base class for creating model event handlers. Simply derive from this class
    /// and then override the CanStore() and/or CanRemove() methods.
    /// 
    /// NOTE: Can't actually make this abstract or Unity won't allow us to use a default inspector.
    /// </summary>
    [RequireComponent(typeof(PGIModel))]
    public class AbstractBaseModelEventHandler : MonoBehaviour, IModelEventHandler
    {
        public enum CellType
        {
            Equipment,
            Grid,
        }

        [HideInInspector]
        [SerializeField]
        CellType _Type;

        [HideInInspector]
        [SerializeField]
        protected int Index0;

        [HideInInspector]
        [SerializeField]
        protected int Index1;

        public CellType Type { get { return _Type; } set { _Type = value; } }
        public int EquipmentIndex { get { return Index0; } set { Index0 = value; } }
        public int GridX { get { return Index0; } set { Index0 = value; } }
        public int GridY { get { return Index1; } set { Index1 = value; } }
        

        public CellModel Cell { get; private set; }
        
        /// <summary>
        /// Callback used by the model to register event handlers.
        /// </summary>
        /// <param name="cell"></param>
        public void Register(CellModel cell)
        {
            Cell = cell;
            if (cell == null)
                return;
            cell.Model.OnCanStoreItem.AddListener(CanStoreInternal);
            cell.Model.OnCanRemoveItem.AddListener(CanRemoveInternal);
            cell.Model.OnStoreItem.AddListener(StoreInternal);
            cell.Model.OnStoreItemFailed.AddListener(StoreFailedInternal);
            cell.Model.OnRemoveItem.AddListener(RemoveInternal);
        }

        /// <summary>
        /// Callback used by the mode to unregister event handlers.
        /// </summary>
        public void Unregister()
        {
            if (Cell == null || Cell.Model == null) return;
            Cell.Model.OnCanStoreItem.RemoveListener(CanStoreInternal);
            Cell.Model.OnCanRemoveItem.RemoveListener(CanRemoveInternal);
            Cell.Model.OnStoreItemFailed.RemoveListener(StoreFailedInternal);
            Cell.Model.OnRemoveItem.RemoveListener(RemoveInternal);
            Cell = null;
        }

        protected virtual void CanStoreInternal(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
                CanStore(onFailed, item, dest);
        }

        protected virtual void CanRemoveInternal(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {
            if(dest == Cell && dest != null)
                CanRemove(onFailed, item, dest);
        }

        protected virtual void StoreInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
                Store(item, dest);
        }

        protected virtual void StoreFailedInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
                StoreFailed(item, dest);
        }

        protected virtual void RemoveInternal(PGISlotItem item, CellModel dest)
        {
            if (dest == Cell && dest != null)
                Remove(item, dest);
        }

        public virtual void CanStore(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {

        }

        public virtual void CanRemove(UnityAction onFailed, PGISlotItem item, CellModel dest)
        {

        }

        public virtual void Store(PGISlotItem item, CellModel dest)
        {

        }

        public virtual void StoreFailed(PGISlotItem item, CellModel dest)
        {

        }

        public virtual void Remove(PGISlotItem item, CellModel src)
        {

        }

    }


    /// <summary>
    /// Interfaces that is used by all event handlers that are attached to a <see cref="PGIModel"/>.
    /// Any component that exposes this interface will automatically be registered and invoked by the model
    /// when the corresponding event happens.
    /// </summary>
    public interface IModelEventHandler
    {
        void Register(CellModel cell);
        void Unregister();

        AbstractBaseModelEventHandler.CellType Type { get; }
        int EquipmentIndex { get; }
        int GridX { get; }
        int GridY { get; }
    }
}
