/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using PowerGridInventory.Utility;
using System.Xml.Serialization;
using Toolbox.Common;
using UnityEngine.Events;

namespace PowerGridInventory
{
    /// <summary>
    /// Provides functionality for allowing a GameObject
    /// to interact with the Power Grid Inventory system
    /// as an item that can be stored in a <see cref="PGIModel"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Power Grid Inventory/PGI Item", 14)]
    [Serializable]
    public class PGISlotItem : MonoBehaviour, IEventSystemHandler, IComparable
    {
        /// <summary>
        /// Defines the types of icons that can be displayed in a view to represent an item.
        /// </summary>
        public enum IconAssetType
        {
            Sprite,
            Mesh,
        }

        /// <summary>
        /// Defines an abosulute rotation orientation for the slot item.
        /// </summary>
        public enum RotateDirection
        {
            None,
            CW,
            CCW,
        }

        [Serializable]
        public class ItemEvent : UnityEvent<PGISlotItem> { }

        #region Members
        /// <summary>
        /// A toggle that determines what kind of asset to display in the view as this item's icon.
        /// </summary>
        [Header("Icon")]
        [HideInInspector]
        public IconAssetType IconType = IconAssetType.Sprite;

        /// <summary>
        /// The sprite that represents this item in an inventory view.
        /// </summary>
        [Tooltip("The sprite that represents this item in an inventory view.")]
        [HideInInspector]
        public Sprite Icon;

        /// <summary>
        /// 3D mesh that represents this item in an inventory view.
        /// </summary>
        [Tooltip("The 3D mesh that represents ths item in an inventory view.")]
        [HideInInspector]
        public Mesh Icon3D;

        /// <summary>
        /// The material used by the 3D mesh icon.
        /// </summary>
        [Tooltip("The material used by the 3D mesh icon.")]
        [HideInInspector]
        public Material IconMaterial;

        /// <summary>
        /// The angle that this 3D icon will have within an inventory slot.
        /// </summary>
        [XmlIgnore]
        public Vector3 IconOrientation
        {
            get { return _IconOrientation; }
            set {_IconOrientation = value; }
        }
        [SerializeField]
        [HideInInspector]
        public Vector3 _IconOrientation = Vector3.zero;

        
        /// <summary>
        /// The model that is currently containing this item.
        /// </summary>
        public CellModel Cell { get; set; }
                        
        /// <summary>
        /// Returns the index of the equipment slot this item is equipped to or -1 if it is not equipped.
        /// </summary>
        public int Equipped { get { return Cell == null ? -1 : Cell.EquipIndex; } }

        /// <summary>
        /// Read only. The x position of the inventory grid this item is stored at or -1 if it is not in an inventory or equipped to an equipment slot.
        /// </summary>
        public int xInvPos { get { return Cell == null ? -1 : Cell.xPos; } }

        /// <summary>
        /// Read only. The y position of the inventory grid this item is stored at or -1 if it is not in an inventory or equipped to an equipment slot.
        /// </summary>
        public int yInvPos { get { return Cell == null ? -1 : Cell.yPos; } }

        /// <summary>
        /// The number of grid cells this item takes up on the horizontal axis. It must be at least 1.
        /// </summary>
        [Header("Inventory Stats")]
        [Tooltip("The number of grid cells this item takes up on the horizontal axis. It must be at least 1.")]
        [FormerlySerializedAs("InvWidth")]
        public int CellWidth = 1;

        /// <summary>
        /// The number of grid cells this item takes up on the vertical axis. It must be at least 1.
        /// </summary>
        [Tooltip("The number of grid cells this item takes up on the vertical axis. It must be at least 1.")]
        [FormerlySerializedAs("InvHeight")]
        public int CellHeight = 1;

        /// <summary>
        /// The color to display in a slot's highlight when this item is stored.
        /// </summary>
        [Tooltip("The color to display in a slot's highlight when this item is stored.")]
        public Color Highlight = Color.clear;

        /// <summary>
        /// A special identifier used to determine if two items are stackable.
        /// </summary>
        [Tooltip("A special identifier used to determine if two items are stackable.")]
        [FormerlySerializedAs("StackID")]
        public HashedString ItemTypeId = new HashedString("");

        /// <summary>
        /// The number of items sharing the same <see cref="PGISlotItem.ItemTypeId"/> that can be stacked on top of each other in a single location.
        /// </summary>
        [Header("Stacking")]
        [Tooltip("The maximum number of items sharing the same StackID that can be stacked on top of each other in a single location.")]
        public int MaxStack = 1;

        /// <summary>
        /// The current stack size for this item. This number is used by the model's stacking system
        /// to treat this item as a collection even though, physically, there is only one instance of the item.
        /// Changing this value will set the 'dirty' flag for any slots used by it in an inventory.
        [Tooltip("The current stack size for this item. There is only one real instance of the item and all stacked instances are destroyed and cause this number to increment.")]
        public int StackCount = 1;

        /// <summary>
        /// Storage for commonly accessed MonoBehaviours attached to this item.
        /// </summary>
        [Space(10)]
        [Tooltip("Storage for commonly accessed MonoBehaviours attached to this item.")]
        public MonoBehaviour[] References;

        /// <summary>
        /// The <see cref="PGIModel"/> that this item is currently stored in, or null if it is not in an inventory.
        /// </summary>
        public PGIModel Model
        {
            get { return Cell == null ? null : Cell.Model; }
        }

        /// <summary>
        /// Returns true if this item is currently equipped to an equipment slot.
        /// </summary>
        public bool IsEquipped
        {
            get { return Cell == null ? false : Cell.IsEquipmentCell; }
        }

        /// <summary>
        /// Returns true if this item is currently in a grid location.
        /// </summary>
        public bool IsStored
        {
            get { return (Model != null && xInvPos >= 0 && yInvPos >= 0); }
        }

        /// <summary>
        /// Returns true if this item is oriented in a rotated fashion.
        /// </summary>
        public bool Rotated
        {
            get { return (_RotatedDir != RotateDirection.None); }
        }

        public RotateDirection RotatedDir
        {
            get { return _RotatedDir; }
        }
        
        //private data
        Transform PreviousParent;
        [SerializeField]
        [HideInInspector]
        RotateDirection _RotatedDir = RotateDirection.None;


        #endregion

        #region PGI Event Fields
        /// <summary>
        /// Invoked when this item is about to be removed from a cell location in a <see cref="PGIModel"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ModelUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelFailableEvent OnCanRemoveItemModel = new ModelFailableEvent();

        /// <summary>
        /// Invoked when this item is about to be stored in cell location in a <see cref="PGIModel"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ModelUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelFailableEvent OnCanStoreItemModel = new ModelFailableEvent();

        /// <summary>
        /// Invoked after this item has removed from a <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnRemoveItemModel = new ModelUpdateEvent();

        /// <summary>
        /// Invoked after this item has been stored in a new <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnStoreItemModel = new ModelUpdateEvent();

        /// <summary>
        /// Invoked after an item has failed to be stored in an inventory. Usually this is
        /// the result of a 'Can...' method disallowing the item to be stored or simply
        /// the fact that there is not enough room for the item.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnStoreItemModelFailed = new ModelUpdateEvent();
        
        /// <summary>
        /// Invoked after this item has first entered into a <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelEvent OnEnterModel = new ModelEvent();

        /// <summary>
        /// Invoked after this item has been dropped from a <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelEvent OnExitModel = new ModelEvent();



        /// <summary>
        /// Invoked when this item is about to be removed from the slot of a <see cref="PGIView"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ViewUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewFailableEvent OnCanRemoveItemView = new ViewFailableEvent();

        /// <summary>
        /// Invoked when this item is about to be stored in the slot of a <see cref="PGIView"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ViewUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewFailableEvent OnCanStoreItemView = new ViewFailableEvent();
        
        /// <summary>
        /// Invoked after this item has been removed from the slot of a <see cref="PGIView"/>.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewUpdateEvent OnRemoveItemView = new ViewUpdateEvent();

        /// <summary>
        /// Invoked after this item has been stored in the slot of a <see cref="PGIView"/>.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewTransferEvent OnStoreItemView = new ViewTransferEvent();

        /// <summary>
        /// Invoked after an item has failed to be stored in an inventory. Usually this is
        /// the result of a 'Can...' method disallowing the item to be stored or simply
        /// the fact that there is not enough room for the item.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewTransferEvent OnStoreItemViewFailed = new ViewTransferEvent();

        /// <summary>
        /// Invoked when the pointer is clicked on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnClick = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is pressed down on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnPointerPressed = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is released on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnPointerReleased = new PGISlot.SlotTrigger();



        /// <summary>
        /// Invoked by a view when this item has been grabbed and a drag operation has started.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewUpdateEvent OnDragBegin = new ViewUpdateEvent();

        /// <summary>
        /// Invoked by a view when this item has been dropped after a drag operation ended.
        /// This will be invoked both when a valid slot is the drop target and when it is invalid
        /// and the item is returned to its original position.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewUpdateEvent OnDragEnd = new ViewUpdateEvent();

        [SerializeField]
        [FoldFlag("Events")]
        public bool FoldedEvents = false; //used by the inspector
        
        #endregion


        #region Methods
        /// <summary>
        /// Marks this object as being in one of three states that represent rotation
        /// in grid-axis space. Effectively all it does is swap the item's
        /// height and width, store a 'rotated' state flag, and rotate the UI image.
        /// </summary>
        /// <remarks>
        /// If the item is currently stored within a model then it may not be possible to
        /// rotate the item and have it remain within that model. The object will attempt
        /// to automatically adjust its position so that it can fit while still maintaining
        /// some vailence with its original position, however if it is not possible the operation
        /// will fail and the item will remain unrotated. The item will always succeed at being
        /// rotated if it is not currently within a model.
        /// </remarks>
        /// <returns><c>true</c> if the item was rotated, <c>false</c> otherwise.</returns>
        /// <param name="dir">The absolute direction to have the item's top rotated towards. </param>
        public bool Rotate(RotateDirection dir)
        {
            if(_RotatedDir == dir) return false;
            if (CellWidth == CellHeight) return false;
            
            PGIModel.Pos newPos = null;
            PGIModel model = this.Model;
            if(model != null && this.IsStored)
            {
                //If in a grid we'll have to check for viable 'vailence' positions for the rotated object.
                //i.e. positions that are still overlapping where the item was before it was rotated.
                //If we can't make the object fit roughly in the same place as before when rotated
                //then we'll have to fail.
                newPos = Model.FindVailencePosition(this, dir);
                if (newPos == null) return false;
            }

            if (newPos != null && model != null)
                PGIModel.RemoveItem(this, false);

            //regarldess of where it is stored, the item needs some basic
            //stats changes and its image rotated.
            int temp = CellHeight;
            CellHeight = CellWidth;
            CellWidth = temp;
            _RotatedDir = dir;
            
            //This should only be called when the 'this.Model' is non-null and
            //the item is stored in the grid but we can safely assume for now 
            //that if 'newPos' is non-null then this is the case.
            if(newPos != null && model != null)
                PGIModel.StoreItem(this, model.GetCellModel(newPos.X, newPos.Y), false);

            return true;
        }
        
        /// <summary>
        /// Used to mark this item as being stored within a grid
        /// at the given location. This usually gets called internally
        /// by a <see cref="PGIModel"/> when storing an item.
        /// </summary>
        /// <param name="storage">The <see cref="PGIModel"/> that this item is being stored in.</param>
        /// <param name="x">The x coordinate in the gird or -1.</param>
        /// <param name="y">The y coordinate in the grid or -1.</param>
        public void ProcessStorage(CellModel cell)
        {
            Cell = cell;
            SetNewParentTransform(Model);
        }
        
        /// <summary>
        /// Marks this item as no longer
        /// being stored within a grid or equipment slot.
        /// It also returns this item to the ownership of
        /// the transform heirarchy it was before it was stored.
        /// This usually gets called internally
        /// by a <see cref="PGIModel"/> when dropping an item.
        /// </summary>
        public void ProcessRemoval()
        {
            RestoreOldTransform();
            Cell = null;
        }

        /// <summary>
        /// Helper method for moving an item into a model's hierarchy and caching important data.
        /// </summary>
        /// <param name="newModel"></param>
        protected void SetNewParentTransform(PGIModel newModel)
        {
            if (newModel.ModifyTransforms)
            {
                if (transform.parent != newModel.transform)
                {
                    PreviousParent = transform.parent;
                }
                transform.SetParent(newModel.transform, false);
            }
        }

        /// <summary>
        /// Helper method for resroring previously cached parent info of this item.
        /// </summary>
        protected void RestoreOldTransform()
        {
            if (Model != null && Model.ModifyTransforms)
            {
                transform.SetParent(PreviousParent, false);
            }
        }
        
        /// <summary>
        /// Used to globally define what metrics to use for sorting (width or height).
        /// </summary>
        public static bool SortByWidth = false;

        /// <summary>
        /// Compares this slot item's size to another slot item's size.
        /// Items with bigger sizes come before items with smaller sizes.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            PGISlotItem other = obj as PGISlotItem;
            if (other == null) return 1;
            if (SortByWidth) return other.CellWidth - this.CellWidth;
            else return other.CellHeight - this.CellHeight;
        }
        #endregion


    }

}
