/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
//#define PGI_LITE
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PowerGridInventory.Utility;
using System;
using Toolbox;
using Toolbox.Common;
using Toolbox.Graphics;
using UnityEngine.Assertions;
using UnityEngine.Events;
using System.Linq;

namespace PowerGridInventory
{
    /// <summary>
    /// Provies the corresponding UI view for a PGIModel.
    /// This particulatr view allows pointer manipulation with 
    /// click-and-hold Drag n' Drop funcitonality.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Power Grid Inventory/PGI View", 12)]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteInEditMode]
    public class PGIView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        #region Fields and Properties
        [SerializeField]
        private PGIModel _Model;
        /// <summary>
        /// The <see cref="PGIModel"/> whose data is displayed and manipulated by this view.
        /// </summary>
        public PGIModel Model
        {
            get { return _Model; }
            set
            {
                ClearList.Remove(this);
                var newModel = value;
                if (_Model != newModel)
                {
                    if(newModel == null) DisableView();

                    //out with the old
                    if (_Model != null)
                         _Model.OnEndGridResize.RemoveListener(UpdateView);

                    //in with the new
                    _Model = newModel;
                    if (_Model != null)
                    {
                        _Model.OnEndGridResize.AddListener(UpdateView);
                        LinkEquipmentSlots();
                    }

                    OnModelChanged.Invoke(this);
                    UpdateView();
                }

            }
        }
        
        public PGISlot[] EquipmentSlots
        {
            get { return Model != null ? Model.EquipmentSlots : null; }
        }

        [Tooltip("The PGISlot prefab GameObject that is cloned for use in the view's grid.")]
        public PGISlot SlotPrefab;

        [HideInInspector]
        [SerializeField]
        bool _Interactable = true;
        /// <summary>
        /// Can be used to disable interactivity with all slots of this view.
        /// </summary>
        public bool Interactable
        {
            get { return _Interactable; }
            set
            {
                _Interactable = value;
                var allSlots = EquipmentSlots == null ? Slots : Slots.Concat(EquipmentSlots).Where((s) => s != null);
                foreach (var slot in allSlots)
                    slot.Interactable = value;
            }
        }


        [SerializeField]
        private bool _DisableRendering;
        /// <summary>
        /// Used to disable all UI elements within the view's grid.
        /// </summary>
        public bool DisableRendering
        {
            get { return _DisableRendering; }
            set
            {
                RequireFullUpdate = true;
                if (_DisableRendering != value)
                {
                    if (value) DisableView();

                    if (Model != null)
                    {
                        UIUtility.SetUIElementsState(gameObject, true, !value);

                        //handle all equipment slots too
                        PGISlot slot;
                        PGISlot[] slots = EquipmentSlots;
                        if (slots != null)
                        {
                            for (int i = 0; i < slots.Length; i++)
                            {
                                slot = slots[i];
                                UIUtility.SetUIElementsState(slot.gameObject, true, !value);
                            }
                        }

                        _DisableRendering = value; //need to do this before updating view or we'll accidentally turn slot icons back on!
                        UpdateView();//we do this so that icons get restored to proper state by the item re-assignment
                    }
                    else _DisableRendering = value;
                }
            }
        }

        public enum DragMode
        {
            Hold = 1,
            Select = 2,
            Both = Hold | Select,
        }
        /// <summary>
        /// If set, this view will ignore input from keyboards and gamepads.
        /// </summary>
        [Header("Drag & Drop Toggles")]
        [Tooltip("If set, this view will ignore submit input from keyboards and gamepads.")]
        public bool IgnoreNonPointerInput;

        /// <summary>
        /// When set, this allows one to begin dragging items by clicking on them once without needing to hold down the button. Clicking again in a valid location will drop the item. Clicking any invalid location will cancel the drag.
        /// </summary>
        [Tooltip("Decides if items can be moved by clicking and holding the pointer like a normal drag 'n' drop operation, clicking and releasing to select the item and clicking again to drop, or both.")]
        public DragMode ItemDragMode = DragMode.Both;

        [Tooltip("The id of the pointer that dragging operations will occur with.")]
        public PointerButton DragButton;

        /// <summary>
        /// Disables the ability to begin dragging items from this PGIView. Hover and click functionalities are not affected by this.
        /// </summary>
        [Tooltip("Disables the ability to begin dragging items from this PGIView. Hover and click functionalities are not affected by this.")]
        public bool DisableDragging;

        /// <summary>
        /// Disables the ability to drop items from this inventory or others into this PGIView's associated model.
        /// </summary>
        [Tooltip("Disables the ability to drop items from this inventory or others into this PGIView's associated model.")]
        public bool DisableDropping;

        /// <summary>
        /// Disables the ability to remove items from the model by dragging them outside of the view's confines. Note that world dropping only works with 'DragMode.Hold'.
        /// </summary>
        [Tooltip("Disables the ability to remove items from the model by dragging them outside of the view's confines. Note that world dropping only works with 'DragMode.Hold'.")]
        public bool DisableWorldDropping;

        /// <summary>
        /// Items that are dragged out of the UI are normally dropped back into the world. If this is set, they are instead destroyed. Note that world dropping only works with 'DragMode.Hold'.
        /// </summary>
        [Tooltip("Items that are dragged out of the UI are normally dropped back into the world. If this is set, they are instead destroyed. Note that world dropping only works with 'DragMode.Hold'.")]
        public bool DestroyOnWorldDrop;

        /// <summary>
        /// Disables the ability for items to be swapped when one item is dropped into an otherwise valid lcoation that another item is inhabiting.
        /// </summary>
        [Tooltip("Disables the ability for items to be swapped when one item is dropped into an otherwise valid lcoation that another item is inhabiting.")]
        public bool DisableSwapping;
        
        /// <summary>
        /// The column order items will be inserted into the grid view.
        /// </summary>
        [Tooltip("The column order the grid will be created.")]
        public HorizontalOrdering HorizontalOrder = HorizontalOrdering.LeftToRight;

        /// <summary>
        /// The row order items will be inserted into the grid view.
        /// </summary>
        [Tooltip("The row order the grid will be created.")]
        public VerticalOrdering VerticalOrder = VerticalOrdering.TopToBottom;

        /// <summary>
        /// If true, model grid cells that are disabled will appear to be blocked (grey-out) in this view, otherwise them will be disabled entirely.
        /// </summary>
        [Tooltip("If true, model grid cells that are disabled will appear to be blocked (grey-out) in this view, otherwise them will be disabled entirely.")]
        public bool BlockDisabledCells;

        /// <summary>
        /// The color used in the 'highlight' section of grid and equipment slots when no action is being taken.
        /// </summary>
        [Header("Slot Colors")]
        [Tooltip("The color used in the 'highlight' section of grid and equipment slots when no action is being taken.")]
        public Color NormalColor = Color.clear;

        /// <summary>
        /// The color used in the 'highlight' section of grid and equipment slots when a valid action is about to occur.
        /// </summary>
        [Tooltip("The color used in the 'highlight' section of grid and equipment slots when a valid action is about to occur.")]
        public Color HighlightColor = Color.green;

        /// <summary>
        /// The color used in the 'hilight' section of a grid and equipment slots when a valid socket action is about to occur.
        /// </summary>
        [Tooltip("The color used in the 'hilight' section of a grid and equipment slots when a valid socket action is about to occur.")]
        public Color SocketValidColor = Color.green;

        /// <summary>
        /// The color used in the 'highlight' section of grid and equipment slots when an invalid action is being taken.
        /// </summary>
        [Tooltip("The color used in the 'highlight' section of grid and equipment slots when an invalid action is being taken.")]
        public Color InvalidColor = Color.red;

        /// <summary>
        /// The color used in the 'highlight' section of grid and equipment slots when it has been flagged as bocked with the <see cref="PGISlot.Blocked"/> value.
        /// </summary>
        [Tooltip("The color used in the 'highlight' section of grid and equipment slots when it has been flagged as bocked with the PGISlot.Blocked value.")]
        public Color BlockedColor = Color.grey;

        /// <summary>
        /// Returns true if this view is currently in a drag operational state.
        /// </summary>
        public bool IsDragging { get { return (DraggedItem != null); } }

        /// <summary>
        /// Helper used to determine if we can drag by clicking and releasing.
        /// </summary>
        bool SelectToDragEnabled
        {
            get { return ((int)ItemDragMode & (int)DragMode.Select) != 0; }
        }

        /// <summary>
        /// Used to tell the view to perform a full slot re-sizing
        /// the next time it updates. If false, the view will attempt
        /// to save on processing by skipping that step.
        /// </summary>
        [HideInInspector]
        [NonSerialized]
        public bool RequireFullUpdate = true;
        
        
        
        
        //cached fields
        private Canvas _UICanvas;
        protected Canvas UICanvas
        {
            get
            {
                if (_UICanvas == null) _UICanvas = GetComponentInParent<Canvas>();
                if (_UICanvas == null) Debug.LogError("PGIView must be a child of a UI canvas.");
                return _UICanvas;
            }

        }
        private RectTransform ParentRect;
        private float CellScaleX = -1;
        private float CellScaleY = -1;
        public static CachedItem DraggedItem;
        static List<PGIView> ClearList = new List<PGIView>(5);
        static List<PGISlot> TempSlots = new List<PGISlot>(5);

        //We need this stored in case we change the model size, so we know if we need
        //to rebuild the whole grid or simply resize it.
        int CachedSlotsX;
        int CachedSlotsY;
        //TODO: Needs testing! This is made static so that DragMode.Select works across inventories. Will this cause issues?
        static bool DragLock;
        public static DragIcon DragIcon;
        static float UpdateFreq = 2;
        float lastUpdateTime;
        List<PGISlot> Slots = new List<PGISlot>(20);

        #endregion


        #region Inner Definitions
        public enum HorizontalOrdering
        {
            LeftToRight,
            RightToLeft,
        }
        public enum VerticalOrdering
        {
            TopToBottom,
            BottomToTop,
        }

        /// <summary>
        /// This is used primarily in the editor to determine if playmode is being exited
        /// so that we can skip potential updates that should happen. They seem to have a
        /// tendency to cause problems if they occurr between the time we try to exit and
        /// the time it actually occurs.
        /// </summary>
        protected static bool ApplicationQuitting = false;


        /// <summary>
        /// Helper class for managing items being moved internally due to drag n' drop actions.
        /// </summary>
        public class CachedItem
        {
            public PGISlotItem Item;
            public int xPos, yPos, Width, Height, EquipIndex;
            public PGISlot SourceSlot;
            public PGIModel Model;
            public PGIView View;
            public PGISlotItem.RotateDirection OriginalRot;
            public bool WasEquipped { get { return (EquipIndex >= 0); } }
            public bool WasStored { get { return (xPos >= 0 && yPos >= 0); } }
            public CellModel Cell { get {return Item == null ? null : Item.Cell;} }

            public CachedItem(PGISlotItem item, PGISlot sourceSlot, PGIModel model, PGIView view)
            {
                Item = item;
                xPos = item.xInvPos;
                yPos = item.yInvPos;
                Width = item.CellWidth;
                Height = item.CellHeight;
                EquipIndex = item.Equipped;
                SourceSlot = sourceSlot;
                Model = model;
                View = view;
                OriginalRot = item.RotatedDir;
            }

            public void RestoreOriginalRotation()
            {
                Item.Rotate(OriginalRot);
            }
        }
        #endregion


        #region PGI Events
        /// <summary>
        /// Triggered when the model of this view changes.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewEvent OnModelChanged = new ViewEvent();

        /// <summary>
        /// Invoked when this view become disabled, deactivated, or detached from a model while a drag operation was in progress.
        /// It is imporant to handle this event in such cases or items may appear to become lost, inactive, or invalid.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelEvent OnDanglingItem = new ModelEvent();

        /// <summary>
        /// Invoked when this item is about to be removed from the slot of a <see cref="PGIView"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ViewUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewFailableEvent OnCanRemoveItem = new ViewFailableEvent();

        /// <summary>
        /// Invoked when this item is about to be stored in the slot of a <see cref="PGIView"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ViewUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewFailableEvent OnCanStoreItem = new ViewFailableEvent();

        /// <summary>
        /// Invoked after this item has been removed from the slot of a <see cref="PGIView"/>.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewUpdateEvent OnRemoveItem = new ViewUpdateEvent();

        /// <summary>
        /// Invoked after this item has been stored in the slot of a <see cref="PGIView"/>.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewTransferEvent OnStoreItem = new ViewTransferEvent();
        
        /// <summary>
        /// Invoked after an item has failed to be stored in an inventory. Usually this is
        /// the result of a 'Can...' method disallowing the item to be stored or simply
        /// the fact that there is not enough room for the item.
        /// <seealso cref="ViewUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewTransferEvent OnStoreItemFailed = new ViewTransferEvent();

        /// <summary>
        /// Invoked when a previous started drag operation ends on an invalid destination.
        /// Invalid destinations are anything that is not a <see cref="PGISlot">PGISlot</see>.
        /// The PGISlot supplied will be the slot that the item was dragged from and is returning to.
        /// The GameObject is whatever object the drag ended on that was an invalid target.
        /// <seealso cref="PGISlot.OnDragBegin"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public InvalidDropEvent OnDragEndInvalid = new InvalidDropEvent();

        /// <summary>
        /// Invoked when the pointer first enters equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnHover"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnHoverSlot = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer leaves an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnEndHover"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnEndHoverSlot = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is clicked on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnClickSlot = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is first pressed down on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnPressedSlot = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is released on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnClick"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotTrigger OnReleasedSlot = new PGISlot.SlotTrigger();

        /// <summary>
        /// Invoked when a drag operation begins on an equipment slot or grid location with an item in it.
        /// <seealso cref="PGISlot.OnDragEnd"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotItemTrigger OnSlotDragBegin = new PGISlot.SlotItemTrigger();

        /// <summary>
        /// Invoked when a previous started drag operation ends.
        /// <seealso cref="PGISlot.OnDragBegin"/> 
        /// <seealso cref="PGISlotItem.OnClick"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public PGISlot.SlotItemTrigger OnSlotDragEnd = new PGISlot.SlotItemTrigger();

        [SerializeField]
        [PGIFoldedEvent]
        public PointerEvent PointerClickEvent = new PointerEvent();
        

        /// <summary>
        /// Posted just after the view updates.
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ViewEvent OnViewUpdated = new ViewEvent();


        [SerializeField]
        [FoldFlag("Events")]
        public bool FoldedEvents = false; //used by the inspector
        
        #endregion


        #region Unity Events
        void Awake()
        {
            //dragicon will be set to HideAndDontSave
            if (DragIcon == null && Application.isPlaying) DragIcon = CreateDragIcon(UICanvas.transform);
        }

        void Start()
        {
            //this will cause the view to completely refresh
            var old = _Model;
            Model = null;
            Model = old;
        }

        void OnDestroy()
        {
            if(!ApplicationQuitting && !Lazarus.AppIsQuitting)
                RelenquishAllSlots();
        }

        void OnEnable()
        {
            //doing this ensures our view is completely refreshed
            RequireFullUpdate = true;
            UpdateView();
            #if UNITY_EDITOR
            if (!Application.isPlaying) ApplicationQuitting = false;
            #endif
        }

        void OnDisable()
        {
            //We add a small delay so that if a triggered event attached to the view was the one that disabled it, they have time to
            //handle everything before we actually disable the view. This is particularly important to the
            //InventoryItem class. When the item is dropped, the nested inventory view should close at that point,
            //but doing so causes the item to be returned to its own inventory due to the shared nature of the DraggedItem.
            //This little delay helps avoid that scenario.
            if (Application.isPlaying) Invoke("DisableView", 0.1f);
            else DisableView();
        }

        private void OnApplicationQuit()
        {
            ApplicationQuitting = true;
        }
        #endregion


        #region Methods
        static DragIcon CreateDragIcon(Transform dragParent)
        {
            GameObject di = new GameObject("Drag Icon");
            di.AddComponent<RectTransform>();
            DragIcon dragIcon = di.AddComponent<DragIcon>();
            di.transform.SetParent(dragParent);
            di.transform.SetSiblingIndex(dragParent.childCount - 1);
            di.transform.localScale = Vector3.one;

            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(di.transform);
            var rect = icon.AddComponent<RectTransform>();
            icon.AddComponent<CanvasRenderer>();
            icon.AddComponent<Image>().preserveAspect = true;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            GameObject icon3d = new GameObject("Icon3D");
            icon3d.transform.SetParent(di.transform);
            rect = icon3d.AddComponent<RectTransform>();
            icon3d.AddComponent<CanvasRenderer>();
            icon3d.AddComponent<Image3D>().PreserveAspect = true;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            dragIcon.Icon = icon.GetComponent<Image>();
            dragIcon.Icon3D = icon3d.GetComponent<Image3D>();
            dragIcon.gameObject.SetActive(false);
            return dragIcon;

        }

        void DisableView()
        {
            #if UNITY_EDITOR
            if (ApplicationQuitting) return;
            #endif

            //Usually happens when we drag an empty slot
            if (DraggedItem == null)
            {
                OnSlotDragEnd.Invoke(null, null, null);
            }
            else
            {
                OnSlotDragEnd.Invoke(null, DraggedItem.SourceSlot, DraggedItem.Item);
                OnDragEnd(null);
                
                OnDanglingItem.Invoke(DraggedItem.Item, DraggedItem.Model);
            }

            //ReturnDraggedItemToSlot();
            DeselectAllViews();
            DraggedItem = null;
            if (DragIcon != null)
            {
                DragIcon.gameObject.SetActive(false);
                ResetDragIcon(DragIcon);
            }

        }


        #if UNITY_EDITOR
        int FrameSkip = 0;
        /// <summary>
        /// Used to render frequent changes in-editor.
        /// </summary>
        protected void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                if (FrameSkip > 24 || (ParentRect != null && ParentRect.hasChanged))
                {
                    UpdateView();
                    FrameSkip = 0;
                }
                else FrameSkip++;
            }
        }
        #endif

        /// <summary>
        /// Used to render frequent changes in playmode.
        /// </summary>
        void Update()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif

            
            var t = Time.realtimeSinceStartup;
            if(t - lastUpdateTime > PGIView.UpdateFreq || (ParentRect != null && ParentRect.hasChanged))
            {
                lastUpdateTime = t;
                UpdateView();
            }

            //need to update dragging *after* view otherwise it will override highlights.
            //if (SelectToDragEnabled && DragLock) //this was causing hilights to not work when in Hold mode?
            if(DragLock)
            {
                if (GamepadInput)
                {
                    //discrete controller input
                    var p = MakePointerEvent(null, GamepadHoveredSlot);
                    OnDrag(p);
                }
                //regular pointer input
                else OnDrag(StandaloneInputModuleEx.Instance.LastPointerEvent(0));
            }

        }

        /// <summary>
        /// Completely refreshes the view's grid and all equipment slots of the view's model.
        /// Internally, the grid and all slots may be re-sized and have their highlighting
        /// and icons set apporpriately according to slot contents.
        /// </summary>
        public void UpdateView()
        {
            #if UNITY_EDITOR
            if (ApplicationQuitting) return;
            #endif

            //check for early-outs
            //NOTE: If we try to update view while dragging an item, bad things will happen to the render state
            if (_DisableRendering) return;
            if (SlotPrefab == null)
            {
                //TODO: disable rendering here if prefab becomes null
                RelenquishAllSlots();
                //Debug.LogWarning("Can't update a view that doesn't have a slot prefab assigned.");
                return;
            }
            #if UNITY_EDITOR
            if (Model != null)
            {
                if(Model.Invalid && Application.isPlaying)
                    return;
            }
            #else
            if (Model != null && Model.Invalid) return;
            #endif
            if (Model == null || CachedSlotsX != Model.GridCellsX || CachedSlotsY != Model.GridCellsY || ParentRect == null)
                CreateGrid();
            else if (RequireFullUpdate)
            {
                ResizeGrid();
                RequireFullUpdate = false;
            }
            RequireFullUpdate = SyncViewToModel();
            OnViewUpdated.Invoke(this);
        }

        /// <summary>
        /// Given a slot that is currently in use by this view, returns its xy-coordinates.
        /// </summary>
        /// <param name="slot">The slot whose location is to be found.</param>
        /// <returns>A struct containing the grid location corresponding to the slot.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if there is no model or the slot supplied does not belong to this view.</exception>
        public PGIModel.ImmutablePos GetSlotLocation(PGISlot slot)
        {
            if (Model == null)
                throw new ArgumentOutOfRangeException("There is no model associated with the view '" + name + "'.");

            var i = Slots.IndexOf(slot);
            if (i < 0)
                throw new ArgumentOutOfRangeException("The supplied slot does not exist within the view '" + name + "'.", "slot");
            
            int x = i % (_Model.GridCellsX);
            int y = (i - x) / _Model.GridCellsX;
            return new PGIModel.ImmutablePos(x, y);
        }
        
        /// <summary>
        /// Helper method to resize all slots to match the new size of the RectTransform.
        /// </summary>
        void ResizeGrid()
        {
            if (Model == null || Slots == null || Slots.Count < 0 || ParentRect == null || !ParentRect.hasChanged) return;
            
            //resize slots
            CellScaleX = ParentRect.rect.width / Model.GridCellsX;
            CellScaleY = ParentRect.rect.height / Model.GridCellsY;

            for (int y = 0; y < Model.GridCellsY; y++)
            {
                for (int x = 0; x < Model.GridCellsX; x++)
                {
                    //initialize slot
                    PGISlot slot = GetSlotCell(x, y);
                    if (slot == null)
                    {
                        //The slot was deleted but we are still referencing it. Uh-oh!
                        break;
                    }
                    GameObject slotGO = slot.gameObject;
                    slotGO.transform.position = Vector3.zero;
                    slotGO.transform.SetParent(this.transform, false);
                    slotGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    //position and size
                    RectTransform childRect = slotGO.transform as RectTransform;
                    var pos = CalculateCellPos(x, y);
                    childRect.anchoredPosition = pos;
                    childRect.anchoredPosition3D = new Vector3(pos.x, pos.y, 0.0f); //CalculateCellPos(x, y);
                    childRect.sizeDelta = new Vector2(CellScaleX, CellScaleY);
                }
            }
        }

        PGISlot MakeSlot(int x, int y, SlotPrefabOverride[] overrides)
        {
            //determine our prefab to use
            GameObject prefab = SlotPrefab.gameObject;
            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].OverridesLocation(x, y))
                {
                    prefab = overrides[i].PrefabOverride.gameObject;
                    break;
                }
            }

            //get a slot from the pool
            PGISlot slot = Lazarus.Summon(prefab, false).GetComponent<PGISlot>();
#if UNITY_EDITOR
            if (Application.isPlaying) slot.gameObject.name = "Slot " + x + "," + y;
            else slot.gameObject.name = "Edit-time Slot" + x + "," + y;
#else
            slot.gameObject.name = "Slot " + x + "," + y;
#endif

            slot.View = this;
            slot.CorrespondingCell = Model.GetCellModel(x, y);
            slot.HighlightColor = NormalColor;

            //Just to be extra careful when re-using pooled slots,
            //we're going to make sure all previous listeners are disengaged.
            slot.OnBeginDragEvent.RemoveAllListeners();
            slot.OnEndDragEvent.RemoveAllListeners();
            slot.OnDragEvent.RemoveAllListeners();
            slot.OnHover.RemoveAllListeners();
            slot.OnEndHover.RemoveAllListeners();
            slot.OnClick.RemoveAllListeners();
            slot.OnPointerPressed.RemoveAllListeners();
            slot.OnPointerReleased.RemoveAllListeners();
            slot.OnSubmitInput.RemoveAllListeners();
            slot.OnCancelInput.RemoveAllListeners();
            slot.OnSelected.RemoveAllListeners();

            //rig slot events to this view
            slot.OnBeginDragEvent.AddListener(HandleDragBegin);
            slot.OnEndDragEvent.AddListener(HandleDragEnd);
            slot.OnDragEvent.AddListener(HandleDrag);
            slot.OnHover.AddListener(OnHoverEvent);
            slot.OnEndHover.AddListener(OnEndHoverEvent);
            slot.OnClick.AddListener(HandleClick);
            slot.OnPointerPressed.AddListener(HandlePointerPressed);
            slot.OnPointerReleased.AddListener(HandlePointerReleased);
            slot.OnSubmitInput.AddListener(HandleSubmit);
            slot.OnCancelInput.AddListener(HandleCancel);
            slot.OnSelected.AddListener(HandleSelected);
            
            Slots.Add(slot);
            slot.gameObject.SetActive(true);
            return slot;
        }

        void RelenquishAllSlots()
        {
            //get a list of all child objects that are slots
            var list = new List<PGISlot>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                var slot = transform.GetChild(i).GetComponent<PGISlot>();
                if (slot != null)
                {
                    list.Add(slot);
                    slot.CorrespondingCell = null; //this is important or memory leaks and other bad stuff might happen when sharing views for models
                }
            }

            //relenquish/destroy all found slots
            for (int i = 0; i < list.Count; i++)
                Lazarus.RelenquishToPool(list[i].gameObject);


            Slots.Clear();
            CachedSlotsX = -1;
            CachedSlotsY = -1;
        }

        /// <summary>
        /// Creates a grid of <see cref="PGISlot"/>s and sets up
        /// all events and references used by the view and model.
        /// </summary>
        void CreateGrid()
        {
            RelenquishAllSlots();

            //if no model, return
            if (Model == null || SlotPrefab == null) return;

            //resize to adjust for new model
            ParentRect = this.GetComponent<RectTransform>();
            CellScaleX = ParentRect.rect.width / Model.GridCellsX;
            CellScaleY = ParentRect.rect.height / Model.GridCellsY;

            //re-activate all old slots (repurposing them in the process)
            //and then create any additional ones we may need
            var overrides = GetComponents<SlotPrefabOverride>();
            for (int y = 0; y < Model.GridCellsY; y++)
            {
                for (int x = 0; x < Model.GridCellsX; x++)
                    MakeSlot(x, y, overrides);
            }
            CachedSlotsX = Model.GridCellsX;
            CachedSlotsY = Model.GridCellsY;

            ResizeGrid();
        }

        /// <summary>
        /// Sets up all references and triggered events for this view's model's Equipment slots.
        /// </summary>
        public void LinkEquipmentSlots()
        {
            if (Model == null) return;
            if (EquipmentSlots != null)
            {
                for (int i = 0; i < EquipmentSlots.Length; i++)
                {
                    var slot = EquipmentSlots[i];
                    if (slot != null)
                    {
                        if (slot.View != null)
                        {
                            //remove old listeners if they existed
                            slot.OnBeginDragEvent.RemoveAllListeners();
                            slot.OnEndDragEvent.RemoveAllListeners();
                            slot.OnDragEvent.RemoveAllListeners();
                            slot.OnHover.RemoveAllListeners();
                            slot.OnEndHover.RemoveAllListeners();
                            slot.OnClick.RemoveAllListeners();
                            slot.OnPointerPressed.RemoveAllListeners();
                            slot.OnPointerReleased.RemoveAllListeners();
                            slot.OnSubmitInput.RemoveAllListeners();
                            slot.OnCancelInput.RemoveAllListeners();
                            slot.OnSelected.RemoveAllListeners();
                        }

                        slot.View = this;
                        slot.OnBeginDragEvent.AddListener(HandleDragBegin);
                        slot.OnEndDragEvent.AddListener(HandleDragEnd);
                        slot.OnDragEvent.AddListener(HandleDrag);
                        slot.OnHover.AddListener(OnHoverEvent);
                        slot.OnEndHover.AddListener(OnEndHoverEvent);
                        slot.OnClick.AddListener(HandleClick);
                        slot.OnPointerPressed.AddListener(HandlePointerPressed);
                        slot.OnPointerReleased.AddListener(HandlePointerReleased);
                        slot.OnSubmitInput.AddListener(HandleSubmit);
                        slot.OnCancelInput.AddListener(HandleCancel);
                        slot.OnSelected.AddListener(HandleSelected);

                        slot.HighlightColor = NormalColor;
                        slot.CorrespondingCell = Model.GetEquipmentModel(i);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void ModelActionFailed()
        {
            Model.CanPerformAction = false;
        }

        /// <summary>
        /// Helper function for trigger all relevant calls when performing an event action.
        /// </summary>
        /// <param name="ec"></param>
        /// <param name="item"></param>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public bool TriggerEvents(EventClass ec, PGISlotItem item, PGISlot targetSlot, PGISlot alternateSlot)
        {
            if (Model == null) return false;
            Model.ResetAction();

            switch(ec)
            {
                case EventClass.CanRemove:
                    {
                        OnCanRemoveItem.Invoke(ModelActionFailed, item, targetSlot);
                        if (item != null) item.OnCanRemoveItemView.Invoke(ModelActionFailed, item, targetSlot);
                        if (targetSlot != null) targetSlot.OnCanRemoveItem.Invoke(ModelActionFailed, item, targetSlot);
                        break;
                    }
                case EventClass.CanStore:
                    {
                        OnCanStoreItem.Invoke(ModelActionFailed, item, targetSlot);
                        if (item != null) item.OnCanStoreItemView.Invoke(ModelActionFailed, item, targetSlot);
                        if (targetSlot != null) targetSlot.OnCanStoreItem.Invoke(ModelActionFailed, item, targetSlot);
                        break;
                    }
                case EventClass.Remove:
                    {
                        OnRemoveItem.Invoke(item, targetSlot);
                        if (item != null) item.OnRemoveItemView.Invoke(item, targetSlot);
                        if (targetSlot != null) targetSlot.OnRemoveItem.Invoke(item, targetSlot);
                        break;
                    }
                case EventClass.Store:
                    {
                        Assert.IsNotNull(alternateSlot);
                        OnStoreItem.Invoke(item, alternateSlot, targetSlot);
                        if (item != null) item.OnStoreItemView.Invoke(item, targetSlot, alternateSlot);
                        if (targetSlot != null) targetSlot.OnStoreItem.Invoke(item, targetSlot, alternateSlot);
                        break;
                    }
                case EventClass.Failed:
                    {
                        OnStoreItemFailed.Invoke(item, targetSlot, alternateSlot);
                        if (item != null) item.OnStoreItemViewFailed.Invoke(item, targetSlot, alternateSlot);
                        if (targetSlot != null) targetSlot.OnStoreItemFailed.Invoke(item, targetSlot, alternateSlot);
                        break;
                    }
                default:
                    {
                        throw new UnityException("Unsupported event class " + ec.ToString());
                    }
            }//end switch

            return Model.CanPerformAction;
        }


        /// <summary>
        /// Returns all slots that an item covers while stored.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        List<PGISlot> GetAssociatedSlots(PGISlot slot)
        {
            TempSlots.Clear();
            if(slot.IsEquipmentSlot)
                TempSlots.Add(slot);

            for(int y = 0; y < slot.GridHeight; y++)
            {
                for (int x = 0; x < slot.GridWidth; x++)
                {
                    var cell = GetSlotCell(slot.xPos + x, slot.yPos + y);
                    if(cell != null)
                        TempSlots.Add(cell);
                }
            }

            return TempSlots;
        }

        /// <summary>
        /// Handles the BeginDrag event trigger from a slot. This method
        /// will cache the item being manipulated before removing it from
        /// its storage location.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        void OnDragBegin(PointerEventData eventData)
        {
            if (DisableDragging) return;
            if (DraggedItem != null) return; //Usually happens when we drag an empty slot
            
            //get the contents of the slot we started dragging,
            //cache it, and then make the cell it is currently in appear to be empty
            PGISlot slot = eventData.pointerDrag.GetComponent<PGISlot>();
            var item = slot.Item;
            if (item == null) return;


            //check the view's can methods first, that way if they fail, we don't have to put anything back.
            if (TriggerEvents(EventClass.CanRemove, slot.Item, slot, null))
            { 
                if (!PGIModel.RemoveItem(slot.Item, true))
                    return;
            }
            else return;

            RequireFullUpdate = true;
            DragLock = true;
            DraggedItem = new CachedItem(item, slot, slot.Model, slot.View);
            
            //display the icon that follows the mouse cursor
            SetDragIcon(DragIcon, DraggedItem, slot);
            TriggerEvents(EventClass.Remove, item, slot, null);
            DraggedItem.Item.OnDragBegin.Invoke(item, slot);
            OnSlotDragBegin.Invoke(eventData, slot, item);
        }

        /// <summary>
        /// Called internally when starting a new drag operation manually due to a swap operation.
        /// </summary>
        void BeginSwapDrag(PGISlotItem newItem, PGISlot slot, PointerEventData eventData)
        {
            if (!TriggerEvents(EventClass.CanRemove, slot.Item, slot, null))
                return;

            RequireFullUpdate = true;
            DragLock = true;

            //get the contents of the slot we started dragging,
            //cache it, and then make the cell it is currently in appear to be empty
            DraggedItem = new CachedItem(newItem, slot, slot.Model, slot.View);
            
            TriggerEvents(EventClass.Remove, DraggedItem.Item, slot, null);
            DraggedItem.Item.OnDragBegin.Invoke(DraggedItem.Item, slot);

            //display the icon that follows the mouse cursor
            SetDragIcon(DragIcon, DraggedItem, slot);
            OnSlotDragBegin.Invoke(eventData, DraggedItem.SourceSlot, newItem);
        }

        /// <summary>
        /// Called internally when starting a new drag operation manually due to a failed drop operation.
        /// </summary>
        void RestartFailedDrag(PGISlotItem newItem, PGISlot slot, PointerEventData eventData)
        {
            RequireFullUpdate = true;
            DragLock = true;

            //get the contents of the slot we started dragging,
            //cache it, and then make the cell it is currently in appear to be empty
            DraggedItem = new CachedItem(newItem, slot, slot.Model, slot.View);

            //display the icon that follows the mouse cursor
            SetDragIcon(DragIcon, DraggedItem, slot);
            OnSlotDragBegin.Invoke(eventData, DraggedItem.SourceSlot, newItem);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="evenData"></param>
        void OnDragEnd(PointerEventData eventData)
        {
            //happens when the drag state is out of sync - usually when unity first starts >:(
            if (DraggedItem == null)
                return;

            RequireFullUpdate = true;
            var srcSlot = DraggedItem.SourceSlot;
            var item = DraggedItem.Item;

            if (ItemDragMode == DragMode.Hold && (eventData == null || eventData.pointerEnter == null))
            {
                //TODO: DROP ITEM IN WORLD
                
                //Was dropped in empty space (no UI elements at all)

                if (DraggedItem.View.DisableWorldDropping)
                {
                    //NOTE: We can't do a simple 'revert' since this may be the tail end of
                    //a series of swaps. We'll just have to try to store the item as best we can.
                    //not allowed by view, return item to source slot
                    if (!srcSlot.Model.Pickup(item))
                    {
                        item.ProcessRemoval();
                        if (DestroyOnWorldDrop)
                            Destroy(item.gameObject);
                    }
                }
                else
                {
                    //This is where we drop items from inventories entirely.
                    //The location that was chosen to end the drag was
                    //completely empty (including UI elements). So we
                    //will remove item from the inventory completely.

                    //TODO: process dropping the item here
                    item.ProcessRemoval();
                    if (DestroyOnWorldDrop)
                        Destroy(item.gameObject);

                    //make sure we return this item to normal orientation when removing it
                    DraggedItem.Item.Rotate(PGISlotItem.RotateDirection.None);
                    
                }
                DeselectAllViews();
                DragIcon.SetIconActive(DragIcon.ActiveIcon.None);
                ResetDragIcon(DragIcon);
                OnSlotDragEnd.Invoke(eventData, null, item);
                DraggedItem = null;
                return;
            }

            if (eventData != null && eventData.pointerEnter != null)
            {
                var destSlot = eventData.pointerEnter.GetComponent<PGISlot>();
                destSlot = destSlot.View.GetOffsetSlot(item, destSlot);
                ProcessDrop(eventData, item, destSlot, srcSlot);
            }
        }

        /// <summary>
        /// Helper for calling common functions after a drag operation
        /// </summary>
        /// <param name="ptrData">The pointer data that triggered the event.</param>
        /// <param name="srcSlot">The slot that the drag event originated from.</param>
        /// <param name="resultSlot">The slot that the drag attempted to end on.</param>
        static void PublishDragResults(PointerEventData ptrData, PGISlot srcSlot, PGISlot destSlot)
        {
            //NOTE: Not ussingthis anymore since canceling drag si no longer an option!
            //ReturnDraggedItemToSlot();

            PGIView.DeselectAllViews();
            PGIView.ResetDragIcon(DragIcon);
            srcSlot.View.OnSlotDragEnd.Invoke(ptrData, srcSlot, DraggedItem.Item);
            destSlot.Select();
            PGIView.DragLock = false;
            PGIView.DraggedItem = null;

            //TODO: at this point if we still have an item 
            // being dragged (i.e. DraggedItem.Item isn't null
            // and it also isn't stored anywhere) then we need to drop
            // that item out of the inventory entirely!
        }

        /// <summary>
        /// Helper for checking if an item can be stored in a slot. It account
        /// for both equipment slots and grid slots and their neighbors.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <returns></returns>
        static bool CanStoreInSlotRange(PGISlotItem item, PGISlot dest, PGISlot src)
        {
            if (item == null || dest == null || dest.View == null || dest.Model == null)
                return false;
            var destView = dest.View;

            PGISlot[] slots;
            if(dest.IsEquipmentSlot)
            {
                slots = new PGISlot[1];
                slots[0] = dest;
            }
            else slots = destView.GetSlotRange(dest.xPos, dest.yPos, item.CellWidth, item.CellHeight);
            if (slots == null || slots.Length == 0)
                return false;

            for(int i = 0; i < slots.Length; i++)
            {
                if (!destView.TriggerEvents(EventClass.CanStore, item, slots[i], src))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Internally handle the drop action.
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="item"></param>
        /// <param name="srcSlot"></param>
        /// <param name="destSlot"></param>
        static bool ProcessDrop(PointerEventData eventData, PGISlotItem item, PGISlot destSlot, PGISlot srcSlot)
        {
            if (destSlot == null || !CanStoreInSlotRange(item, destSlot, srcSlot))
                return false;

            var destModel = destSlot.Model;
            var destCell = destSlot.CorrespondingCell;
            var destView = destSlot.View;
            var srcView = srcSlot.View;

            if(destView == null)

            if (destSlot.IsEquipmentSlot)
            destSlot = destSlot.View.GetOffsetSlot(DraggedItem.Item, destSlot);

            //make sure we have a valid grid size for our item
            if (!destView.DisableDropping && destModel.CanStore(item, destCell))
            {
                //store it!
                PGIModel.StoreItem(item, destCell, false);
                destSlot.View.TriggerEvents(EventClass.Store, item, destSlot, srcSlot);
                item.OnDragEnd.Invoke(item, destSlot);
                PGIView.PublishDragResults(eventData, srcSlot, destSlot);
                return true;
            }
            else if(!destView.DisableDropping && destModel.CanSocket(item, destCell.Item))
            {
                //socket it
                PGIModel.StoreItem(item, destCell, true);
                destSlot.View.TriggerEvents(EventClass.Store, item, destSlot, srcSlot);
                item.OnDragEnd.Invoke(item, destSlot);
                PGIView.PublishDragResults(eventData, srcSlot, destSlot);
                return true;
            }
            else if (!destView.DisableDropping && destModel.CanStack(item, destCell))
            {
                //stack it
                PGIModel.StoreItem(item, destCell, true);
                destSlot.View.TriggerEvents(EventClass.Store, item, destSlot, srcSlot);
                item.OnDragEnd.Invoke(item, destSlot);
                PGIView.PublishDragResults(eventData, srcSlot, destSlot);
                return true;
            }
            else if (!destView.DisableDropping && !destView.DisableSwapping && destModel.CanSwap(item, destCell))
            {
                //swap it!

                //NOTE: Had to move this event outside of the BeginSwapDrag() method to allow it to access the item
                //that is about to be dragged.
                srcView.OnSlotDragEnd.Invoke(eventData, srcSlot, item);
                var newItem = PGIModel.SwapItem(item, destCell, false);
                destSlot.View.BeginSwapDrag(newItem, destSlot, eventData);
                return true;
            }
            else
            {
                //result is invalid, keep dragging
                destSlot.View.TriggerEvents(EventClass.Failed, item, destSlot, srcSlot);
                if (destSlot.View != srcSlot.View)
                    srcSlot.View.TriggerEvents(EventClass.Failed, item, destSlot, srcSlot);
                
                item.OnDragEnd.Invoke(item, srcSlot);
                PGIView.PublishDragResults(eventData, srcSlot, destSlot);

                //keep dragging!
                //Debug.Log("<color=green>This is failing miserably! We don't have a proper source slot or sour slot item!</color>");

                srcSlot.OnResetDrag();
                srcSlot.View.RestartFailedDrag(item, srcSlot, eventData);
                return false;
            }
        }

        /// <summary>
        /// Handles the updating drag event. Provides highlighting and cell
        /// offsetting (to ensure the item is placed in a reasonable way on the grid).
        /// </summary>
        /// <param name="eventData">Event data.</param>
        void OnDrag(PointerEventData eventData)
        {
            if (eventData == null) return;

            if(GamepadInput)

            //this can happen if we attempt to drag and empty slot
            if (DraggedItem == null || DraggedItem.Item == null) return;
            AppendClearList(this);


            //figure out highlighting and cell offsets.
            PGISlot dropSlot = null;
            if (eventData.pointerEnter != null)
            {
                dropSlot = eventData.pointerEnter.GetComponent<PGISlot>();

                //clear all grids involved
                //kinda slow but I'm lazy right now
                DeselectAllViews();

                if (dropSlot != null)
                {
                    //Make sure the view is added to the dirty highlighting list, then highlight the dragged item
                    AppendClearList(dropSlot.View);
                    //for some inexplicable reason DraggedItem can be null by the time we get here - not sure how :p
                    if (dropSlot.View != null && DraggedItem != null)
                        dropSlot.View.SelectSlot(dropSlot, DraggedItem.Item);
                }
            }

            if (GamepadInput)
                DragIcon.transform.position = GamepadHoveredSlot.transform.position;
            else DragIcon.transform.position = PGICanvasMouseFollower.GetPointerPosOnCanvas(UICanvas, eventData.position);
        }

        /// <summary>
        /// Helper method that can be used to rotate the drag icon while it is still active.
        /// </summary>
        /// <param name="dir"></param>
        public void RotateDragIcon(PGISlotItem.RotateDirection dir)
        {
            if (!IsDragging) return;
            SetDragIcon(DragIcon, DraggedItem, DraggedItem.SourceSlot);
        }

        /// <summary>
        /// Helper method used to initialize the DragIcon when it becomes visible.
        /// </summary>
        void SetDragIcon(DragIcon icon, CachedItem draggedItem, PGISlot slot)
        {
            switch (draggedItem.Item.RotatedDir)
            {
                case PGISlotItem.RotateDirection.None:
                    {
                        icon.Icon.transform.eulerAngles = Vector3.zero;
                        icon.Icon3D.transform.eulerAngles = Vector3.zero;
                        break;
                    }
                case PGISlotItem.RotateDirection.CW:
                    {
                        icon.Icon.transform.eulerAngles = new Vector3(0.0f, 0.0f, 270.0f);
                        icon.Icon3D.transform.eulerAngles = new Vector3(0.0f, 0.0f, 270.0f);
                        break;
                    }
                case PGISlotItem.RotateDirection.CCW:
                    {
                        icon.Icon.transform.eulerAngles = new Vector3(0.0f, 0.0f, 90.0f);
                        icon.Icon3D.transform.eulerAngles = new Vector3(0.0f, 0.0f, 90.0f);
                        break;
                    }
            }

            if (float.IsInfinity(CellScaleX) || float.IsInfinity(CellScaleY) ||
               CellScaleX <= 0.01f || CellScaleY <= 0.01f)
            {
                //the slot sizes are probably too small to see.
                //This is mostly likely due to using an
                //inventory with a grid size of 0,0.
                //Use the slot size in this case.
                icon.GetComponent<RectTransform>().sizeDelta = slot.GetComponent<RectTransform>().sizeDelta * 0.9f;
            }
            else
            {
                //use a size roughly corresponding to the grid display's size
                //NOTE: we need to reverse them if the item has been rotated
                //Apparently the sizeDelta is absolute and does not take rotation into account.
                if(draggedItem.Item.Rotated)
                    icon.GetComponent<RectTransform>().sizeDelta = new Vector2((CellScaleX * 0.9f) * draggedItem.Height, (CellScaleX * 0.9f) * draggedItem.Width);
                else icon.GetComponent<RectTransform>().sizeDelta = new Vector2((CellScaleX * 0.9f) * draggedItem.Width, (CellScaleX * 0.9f) * draggedItem.Height);
            }

            //Display our icon image, either a sprite or a mesh.
            //icon.gameObject.SetActive(true); //activate first or GetComponent will fail
            if (draggedItem.Item.IconType == PGISlotItem.IconAssetType.Sprite)
            {
                icon.SetIconActive(DragIcon.ActiveIcon.Icon2D);
                icon.Icon.sprite = draggedItem.Item.Icon;
                icon.Icon3D.material = null;
                icon.Icon3D.Mesh = null;
                
            }
            else
            {
                icon.SetIconActive(DragIcon.ActiveIcon.Icon3D);
                icon.Icon.sprite = null;
                icon.Icon3D.material = draggedItem.Item.IconMaterial;
                icon.Icon3D.Rotation = draggedItem.Item.IconOrientation;
                icon.Icon3D.Mesh = draggedItem.Item.Icon3D;
                
            }

           
        }

        /// <summary>
        /// Helper method used to reset the drag icon. This should be
        /// called anytime a drag ends for any reason otherwise 3D mesh
        /// icons might not update correctly next time due to cached values
        /// within the CanvasMesh.
        /// </summary>
        static void ResetDragIcon(DragIcon icon)
        {
            icon.Icon.transform.eulerAngles = Vector3.zero;
            icon.Icon3D.transform.eulerAngles = Vector3.zero;
            icon.SetIconActive(DragIcon.ActiveIcon.None);
            icon.gameObject.SetActive(false);
        }
        
        /*
        /// <summary>
        /// Helper method used to return a previously cached and dragged item to the
        /// location it came form when the drag started.
        /// </summary>
        /// <returns><c>true</c>, if dragged item was returned, <c>false</c> otherwise.</returns>
        bool ReturnDraggedItemToSlot(bool restoreRotation = true)
        {
            if (DraggedItem == null) return false;

            //End the drag like normal. Return it to it's origial position
            //either in the grid on in an equipment slot.

            //we must not forget to restore the rotation or we might
            //try to return the item in an orientation that won't fit
            //into its original location!
            
            if (DraggedItem.Item != null)
            {
                if (restoreRotation)
                    DraggedItem.RestoreOriginalRotation();
                if(DraggedItem.SourceSlot != null)
                    PGIModel.StoreItem(DraggedItem.Item, DraggedItem.SourceSlot.CorrespondingCell);
                return true;
            }
            return false;
        }
        */

        /// <summary>
        /// Updates the entire grid UI to match the state of the model.
        /// </summary>
        /// <returns><c>True</c> if any slots were resized, marking this view as dirty and requiring a grid resize. <c>False</c> if no slots were resized and nothing needs redrawn.</returns>
        bool SyncViewToModel()
        {
            if (Model == null) return false;
            if (_DisableRendering) return false;
            bool resizedSomething = false;

            //Reset all slots to default size.
            ResetSlotRange(0, 0, Model.GridCellsX, Model.GridCellsY);
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
            #endif
            if (Model.IsInitialized)
            {
                resizedSomething = SyncSlots(Slots);
                SyncSlots(EquipmentSlots);
                
                //we'll have to check for disabled cells in the model and
                //update them accordingly too
                var disabled = Model.DisabledCellsUnsafe;
                for(int i = 0; i < disabled.Count; i++)
                {
                    var slot = GetSlotCell(disabled[i].X, disabled[i].Y);
                    //have to check for null here due to the possibility of resizing grids,
                    //especially when in edit-mode since there is no pool.
                    if (slot != null)
                    {
                        if (BlockDisabledCells) slot.HighlightColor = BlockedColor;
                        else slot.Activate(false);
                    }
                }
            }

            return resizedSomething;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        bool SyncSlots(List<PGISlot> slots)
        {
            bool resizedSomething = false;

            int count = slots.Count;
            for (int i = 0; i < count; i++)
            {
                var slot = slots[i];
                if (slot != null)
                {
                   
                    //need to reset these here in case they were re-endabled in the model
                    slot.Activate(true);
                    if (slot.Blocked) slot.HighlightColor = BlockedColor;
                    else slot.HighlightColor = NormalColor;
                    var item = slot.Item;
                    if (item != null)
                    {
                        if(!slot.IsEquipmentSlot)
                            resizedSomething = ResizeSlot(item.xInvPos, item.yInvPos, item.CellWidth, item.CellHeight);
                        slot.UpdateView(true);
                    }
                    else slot.UpdateView(true);
                }
            }

            return resizedSomething;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slots"></param>
        /// <returns></returns>
        bool SyncSlots(PGISlot[] slots)
        {
            bool resizedSomething = false;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null)
                {

                    //need to reset these here in case they were re-endabled in the model
                    slot.Activate(true);
                    if (slot.Blocked) slot.HighlightColor = BlockedColor;
                    else slot.HighlightColor = NormalColor;
                    var item = slot.Item;
                    if (item != null)
                    {
                        if (!slot.IsEquipmentSlot)
                            resizedSomething = ResizeSlot(item.xInvPos, item.yInvPos, item.CellWidth, item.CellHeight);
                        slot.UpdateView(true);
                    }
                    else slot.UpdateView(true);
                }
            }

            return resizedSomething;
        }

        /// <summary>
        /// Calculates the grid cell position given the
        /// number of cells and the size of the parenting object.
        /// </summary>
        /// <returns>The cell position.</returns>
        /// <param name="cellX">X position on grid.</param>
        /// <param name="cellY">Y Position on the grid.</param>
        /// <param name="slotWidth">Slot width.</param>
        /// <param name="slotHeight">Slot height.</param>
        public Vector2 CalculateCellPos(int cellX, int cellY, int slotWidth = 1, int slotHeight = 1)
        {
            float yDir = (VerticalOrder == VerticalOrdering.TopToBottom) ? -1.0f : 1.0f;
            float xDir = (HorizontalOrder == HorizontalOrdering.LeftToRight) ? 1.0f : -1.0f;
            float cellPosX = (float)(cellX * CellScaleX) * xDir;
            float cellPosY = (float)(cellY * CellScaleY) * yDir;
            float cellHalfWidth = ((CellScaleX * slotWidth) * 0.5f) * xDir;
            float cellHalfHeight = ((CellScaleY * slotHeight) * 0.5f) * yDir;

            float parentOffsetX = (ParentRect.rect.width * 0.5f) * xDir;
            float parentOffsetY = (ParentRect.rect.height * 0.5f) * yDir;

            return new Vector2(cellPosX + cellHalfWidth - parentOffsetX,
                                   cellPosY + cellHalfHeight - parentOffsetY);

        }

        /// <summary>
        /// Calculates the size of the cell given the slot's cell width and height and the
        /// size of the grid itself. This method takes rotation into account when calculating size.
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public Vector2 CalculateSize(PGISlot slot)
        {
            float i = ParentRect.rect.width / Model.GridCellsX;
            float j = ParentRect.rect.height / Model.GridCellsY;

            float w = i * slot.GridWidth;
            float h = j * slot.GridHeight;

            return new Vector2(w, h);
        }

        /// <summary>
        /// Calculates the size of the cell given the slot's cell width and height and the
        /// size of the grid itself. This method does not consider if the item is rotated but simply
        /// bases it off the raw item's original width and height.
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public Vector2 CalculateNonRotatedSize(PGISlot slot)
        {
            float i = ParentRect.rect.width / Model.GridCellsX;
            float j = ParentRect.rect.height / Model.GridCellsY;

            float w, h;
            if (slot.Item != null && slot.Item.Rotated)
            {
                w = i * slot.GridHeight;
                h = j * slot.GridWidth;
            }
            else
            {
                w = i * slot.GridWidth;
                h = j * slot.GridHeight;
            }

            return new Vector2(w, h);
        }

        /// <summary>
        /// Returns the <see cref="PGISlot"/> found in the given grid coordinates. This represents
        /// a <see cref="PGIView"/> grid, not the internal grid of the model.
        /// </summary>
        /// <returns>The slot cell of this view.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public PGISlot GetSlotCell(int x, int y)
        {
            if (x < Model.GridCellsX && y < Model.GridCellsY && x >= 0 && y >= 0)
                return Slots[(y * Model.GridCellsX) + x];

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public PGISlot[] GetSlotRange(int x, int y, int width, int height)
        {
            if (Model == null || !Model.ConfirmValidCellRange(x, y, width, height))
                return null;
            

            PGISlot[] slots = new PGISlot[width * height];
            int index = 0;
            for(int t = 0; t < height; t++)
            {
                for(int s = 0; s < width; s++)
                {
                    slots[index] = GetSlotCell(s + x, t + y);
                    index++;
                }
            }

            return slots;
        }

        /// <summary>
        /// Finds the slot associated with the cell, if it is part of this view.
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        public PGISlot FindAssociatedSlot(CellModel cell)
        {
            if (cell.IsEquipmentCell && EquipmentSlots != null && 
                EquipmentSlots.Length > cell.EquipIndex && 
                EquipmentSlots[cell.EquipIndex].CorrespondingCell == cell)
                return EquipmentSlots[cell.EquipIndex];
            else
            {
                var slot = GetSlotCell(cell.xPos, cell.yPos);
                if (slot.CorrespondingCell == cell)
                    return slot;
            }

            return null;
        }

        /// <summary>
        /// Resizes the slot at the given location to the given grid-cell size.
        /// </summary>
        /// <returns><c>true</c>, if slot was resized, <c>false</c> otherwise.</returns>
        /// <param name="slot">Slot.</param>
        /// <param name="slotWidth">Slot width.</param>
        /// <param name="slotHeight">Slot height.</param>
        bool ResizeSlot(int x, int y, int width, int height)
        {
            //check for items that aren't actually in a slot (this is a defense against resizing a slot for an item that
            //was recently removed from a grid slot but the model isn't in sync just yet).
            if (x < 0 || y < 0) return false;

            PGISlot initial = this.GetSlotCell(x, y);

            //now, disable any slots that we will be stretching this slot overtop of
            for (int t = y; t < y + height; t++)
            {
                for (int s = x; s < x + width; s++)
                {
                    PGISlot slot = this.GetSlotCell(s, t);
                    if (s == x && t == y)
                    {
                        //this is the cell we are resizing. Set the new size
                        RectTransform rect = slot.GetComponent<RectTransform>();
                        rect.sizeDelta = CalculateSize(slot);
                        rect.anchoredPosition = CalculateCellPos(s, t, width, height);
                    }
                    else
                    {
                        //this is a cell that we are disabling because
                        //the resized cell will be covering it
                        slot.Activate(false);
                        slot.OverridingSlot = initial;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Helper method used to reset a range of cell-slots to 
        /// a normal condition. Used mostly to restore slots that
        /// were previously disabled and covered up when another
        /// slot had to grow in size.
        /// </summary>
        /// <param name="xPos">X position.</param>
        /// <param name="yPos">Y position.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="active">Optional active flag to pass to the <see cref="PGISlot.SetActive"/> method.</param>
        void ResetSlotRange(int xPos, int yPos, int width, int height, bool active = true)
        {
            if (ParentRect == null || Model == null) return;

            float i = ParentRect.rect.width / Model.GridCellsX;
            float j = ParentRect.rect.height / Model.GridCellsY;

            for (int y = yPos; y < yPos + height; y++)
            {
                for (int x = xPos; x < xPos + width; x++)
                {
                    PGISlot slot = this.GetSlotCell(x, y);
                    if (slot != null)
                    {
                        RectTransform rect = slot.GetComponent<RectTransform>();
                        rect.sizeDelta = new Vector2(i, j);
                        rect.anchoredPosition = CalculateCellPos(x, y, 1, 1);

                        slot.Activate(active);
                        slot.OverridingSlot = null;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="slot"></param>
        void ResetSlot(PGISlot slot)
        {
            ResetSlotRange(slot.xPos, slot.yPos, slot.GridWidth, slot.GridHeight);
        }

        /// <summary>
        /// Using the currently hovered slot and inventory size of the selected item,
        /// this method determines the final offset location to place the item so
        /// that it will fit the grid in the nearest selected set of grid slots
        /// and fits within the grid itself.
        /// <remarks>
        /// This aids in a refinement nicety for the user as the placement of items
        /// will seem more natural when the item snaps to the closest set of grid
        /// cells when they are placing larger inventory items.
        /// 
        /// TODO: This doesn't currently take ino account disable model cells.
        ///       Should be implemented to allow better user interaction.
        ///       
        /// 
        /// </remarks>
        /// </summary>
        /// <returns>The slot to actually highlight or store an item in based on item-size offsets.</returns>
        /// <param name="item">The item whose size will be used for offset calculations.</param>
        /// <param name="slot">The original slot being targedt for a drop or hilight.</param>
        PGISlot GetOffsetSlot(PGISlotItem item, PGISlot slot)
        {
            if (slot.IsEquipmentSlot) return slot;
            int offsetX = slot.xPos;
            int offsetY = slot.yPos;

            int w = item.CellWidth;
            int h = item.CellHeight;
            

            Vector2 quad = GamepadInput ? (Vector2)slot.transform.position : slot.GetLocalMouseCoords();
            if (item == null) return slot;
            if (w > 1)
            {
                if (HorizontalOrder == HorizontalOrdering.LeftToRight)
                {
                    //offset based on the quadrant of the selected cell
                    if ((w & 0x1) == 1) offsetX -= ((int)(w / 2)); //odd width
                    else if (quad.x < 0.0f) offsetX -= ((int)(w / 2)); //even width
                    else offsetX -= ((int)(w / 2) - 1);//even width
                }
                else
                {
                    //offset based on the quadrant of the selected cell
                    if ((w & 0x1) == 1) offsetX -= ((int)(w / 2)); //odd width
                    else if (quad.x > 0.0f) offsetX -= ((int)(w / 2)); //even width
                    else offsetX -= ((int)(w / 2) - 1);//even width
                }
            }

            if (h > 1)
            {
                if (VerticalOrder == VerticalOrdering.TopToBottom)
                {
                    //offset based on the quadrant of the selected cell
                    if ((h & 0x1) == 1) offsetY -= ((int)(h / 2)); //odd height
                    else if (quad.y > 0.0f) offsetY -= ((int)(h / 2)); //even height
                    else offsetY -= ((int)(h / 2) - 1);//event height
                }
                else
                {
                    //offset based on the quadrant of the selected cell
                    if ((h & 0x1) == 1) offsetY -= ((int)(h / 2)); //odd height
                    else if (quad.y < 0.0f) offsetY -= ((int)(h / 2)); //even height
                    else offsetY -= ((int)(h / 2) - 1);//even height
                }
            }

            //keep the final location within the grid
            int gridWidth = slot.Model.GridCellsX;
            int gridHeight = slot.Model.GridCellsY;

            if (offsetX < 0)
                offsetX = 0;
            if (offsetX > gridWidth || offsetX + w > gridWidth)
                offsetX = gridWidth - w;
            if (offsetY < 0)
                offsetY = 0;
            if (offsetY > gridHeight || offsetY + h > gridHeight)
                offsetY = gridHeight - h;

            
            //TODO: is there a clever way we can accomodate for disabled model cells?


            return slot.View.GetSlotCell(offsetX, offsetY);
        }

        /// <summary>
        /// Handles highlighting effects when hovering over a grid slot
        /// while performing a drag. This method calculates
        /// the nearest central location for placing an item within
        /// the grid and highlights all cells that will be
        /// used for storage.
        /// </summary>
        /// <param name="slot">The slot that the pointer is currently over.</param>
        /// <param name="item">The item being dragged or dropped.</param>
        void SelectSlot(PGISlot slot, PGISlotItem item)
        {
            if (slot == null || item == null) return;
            
            //if the item is too big, just highlight everything in the grid as invalid and be done with it.
            if (!slot.IsEquipmentSlot && (item.CellHeight > this.Model.GridCellsY || item.CellWidth > this.Model.GridCellsX))
            {
                List<PGISlot> slots = slot.View.Slots;
                for (int i = 0; i < slots.Count; i++) slots[i].HighlightColor = InvalidColor;
                return;
            }

            var offset = slot.IsEquipmentSlot ? slot : slot.View.GetOffsetSlot(item, slot);

            Color color = HighlightColor;
            if (slot.Blocked) slot.HighlightColor = BlockedColor;
            else if (slot.Model.CanSocket(item, slot.Item))
                color = SocketValidColor;
            else if (!DisableSwapping && slot.Model.CanSwap(item, offset.CorrespondingCell))
                color = HighlightColor;
            else if (slot.Model.CanStack(item, offset.CorrespondingCell) || slot.Model.CanStore(item, offset.CorrespondingCell))
                color = HighlightColor;
            else color = InvalidColor;

            if (!CanStoreInSlotRange(item, offset, null))
                color = InvalidColor;

            if (offset.IsEquipmentSlot)
            {
                offset.HighlightColor = color;
                return;
            }

            //find out which slots to highlight based on current hover location
            //and the neighboring slots
            for (int y = offset.yPos; y < offset.yPos + item.CellHeight; y++)
            {
                for (int x = offset.xPos; x < offset.xPos + item.CellWidth; x++)
                {
                    PGISlot start = offset.View.GetSlotCell(x, y);
                    if (start != null)
                    {
                        start.HighlightColor = color;
                        if (start.OverridingSlot != null)
                            start.OverridingSlot.HighlightColor = color;
                    }
                }
            }
        }

        /// <summary>
        /// Helper method used to append grid views to a list
        /// that can later be cleared. Used for processing what
        /// view's slots need to be de-highlighted.
        /// </summary>
        /// <param name="view">The view to add to the list.</param>
        static void AppendClearList(PGIView view)
        {
            if (!ClearList.Contains(view)) ClearList.Add(view);
        }

        /// <summary>
        /// Removes all drag-related highlighting from all grid cells
        /// and equipment slots in this <see cref="PGIView"/>.
        /// </summary>
        public void DeselectAllSlots()
        {
            for (int i = 0; i < Slots.Count; i++ )
            {
                Slots[i].AssignHighlighting(this);
            }
            if (EquipmentSlots != null)
            {
                PGISlot slot;
                for (int i = 0; i < EquipmentSlots.Length; i++ )
                {
                    slot = EquipmentSlots[i];
                    if (slot.Blocked) slot.HighlightColor = BlockedColor;
                    else if(BlockDisabledCells)
                    {
                        var pos = this.GetSlotLocation(slot);
                        if (Model.IsCellDisabled(pos.X, pos.Y))
                            slot.HighlightColor = BlockedColor;
                        else slot.AssignHighlighting(this);
                    }
                    else slot.AssignHighlighting(this);
                }
            }
        }

        /// <summary>
        /// Helper method used to remove selection from all previously stored grid views.
        /// This can be kinda slow since it will inevitably cycle through all slots
        /// in all inventories that were dragged over during a drag operation.
        /// <seealso cref="PGIView.AppendClearList"/>
        /// <seealso cref="PGIView.DeselectAllSlots"/>
        /// 
        /// <remarks>
        /// TODO: This could use a good amount of optimizing. Likely, the
        /// OnDeselectAll method could use a dirty list to only clear
        /// slots that have changed recently.
        /// </remarks>
        /// </summary>
        static void DeselectAllViews()
        {
            for (int i = 0; i < ClearList.Count; i++)
            {
                if (ClearList[i] != null) ClearList[i].DeselectAllSlots();
            }
            ClearList.Clear();
        }
#endregion


        #region Event Handlers
        /// <summary>
        /// Helper method for transforming baseEventData into PointerEventData.
        /// Needed so that Gamepad input and Pointer input can flow through
        /// the same code path.
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        /// <returns></returns>
        public static PointerEventData MakePointerEvent(BaseEventData eventData, PGISlot slot)
        {
            Vector2 pos = slot.transform.position;
            GameObject slotGo = slot.gameObject;

            PointerEventData p = new PointerEventData(FindObjectOfType<EventSystem>());
            p.button = PointerEventData.InputButton.Left;
            p.clickCount = 1;
            p.clickTime = 10;
            p.dragging = true;
            p.pointerDrag = slotGo;
            p.pointerEnter = slotGo;
            p.pointerId = 0;
            p.pointerPress = slotGo;
            p.position = pos;
            p.pressPosition = pos;
            p.rawPointerPress = slotGo;
            p.selectedObject = eventData == null ? slotGo : eventData.selectedObject;

            return p;
        }

        /// <summary>
        /// Handles the previously registered <see cref="PGISlot.OnHover"/> event 
        /// when the pointer first enters a <see cref="PGISlot"/> and invokes
        /// this view's <see cref="PGIView.OnHoverSlot"/> event.
        /// </summary>
        /// <param name="eventData">The pointer event data that triggered the event.</param>
        /// <param name="slot">The slot that the pointer entered.</param>
        void OnHoverEvent(PointerEventData eventData, PGISlot slot)
        {
            if (DraggedItem == null && slot.Item != null)
            {
                OnHoverSlot.Invoke(eventData, slot);
            }
        }

        /// <summary>
        /// Handles the previously registered <see cref="PGISlot.OnEndHover"/> event 
        /// when the pointer leaves a <see cref="PGISlot"/> and invokes
        /// this view's <see cref="PGIView.OnEndHoverSlot"/> event.
        /// </summary>
        /// <param name="eventData">The pointer event data that triggered the event.</param>
        /// <param name="slot">The slot that the pointer exited.</param>
        void OnEndHoverEvent(PointerEventData eventData, PGISlot slot)
        {
            OnEndHoverSlot.Invoke(eventData, slot);
        }
        
        /// <summary>
        /// Handles the previously registered <see cref="PGISlot.OnClick"/> event 
        /// when the pointer clicks on a <see cref="PGISlot"/> and invokes
        /// this view's <see cref="PGIView.OnClickSlot"/> event.
        /// </summary>
        /// <param name="eventData">The pointer event data that triggered the event.</param>
        /// <param name="slot">The slot that was clicked.</param>
        void HandleClick(PointerEventData eventData, PGISlot slot)
        {
            OnClickSlot.Invoke(eventData, slot);
            
            if (SelectToDragEnabled  && (DragButton == PointerButton.Any || (int)DragButton == (int)eventData.button))
            {
                if (!DragLock && slot.Item != null)
                    OnDragBegin(eventData);
                else OnDragEnd(eventData);
            }
        }

        void HandlePointerReleased(PointerEventData eventData, PGISlot slot)
        {
            OnReleasedSlot.Invoke(eventData, slot);
        }

        void HandlePointerPressed(PointerEventData eventData, PGISlot slot)
        {
            OnPressedSlot.Invoke(eventData, slot);
        }

        void HandleDragBegin(PointerEventData eventData)
        {
            if (ItemDragMode == DragMode.Select) return;

            if(DragButton == PointerButton.Any || (int)DragButton == (int)eventData.button)
                OnDragBegin(eventData);
        }

        void HandleDragEnd(PointerEventData eventData)
        {
            if (ItemDragMode == DragMode.Select) return;
            OnDragEnd(eventData);
        }

        void HandleDrag(PointerEventData eventData)
        {
            if (ItemDragMode == DragMode.Select) return;
            OnDrag(eventData);
        }

        static PGISlot GamepadHoveredSlot;
        static bool GamepadInput;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        void HandleSubmit(BaseEventData eventData, PGISlot slot)
        {
            if(IgnoreNonPointerInput)
                return;

            var p = MakePointerEvent(eventData, slot);
            if (!DragLock && slot.Item != null)
            {
                if (IgnoreNonPointerInput)
                    return;
                GamepadInput = true;
                GamepadHoveredSlot = slot;
                OnDragBegin(p);
            }
            else
            {
                OnDragEnd(p);
                GamepadInput = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        void HandleCancel(BaseEventData eventData, PGISlot slot)
        {
            /*
            if (IgnoreNonPointerInput)
                return;
            if (DragLock)
            {
                var p = MakePointerEvent(eventData, slot);
                p.pointerEnter = null; //setting this to null will make OnDragEnd() cancel the drag operation
                OnDragEnd(p);
                GamepadInput = false;
            }
            */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="slot"></param>
        void HandleSelected(BaseEventData eventData, PGISlot slot)
        {
            if (IgnoreNonPointerInput)
                return;
            GamepadHoveredSlot = slot;
        }

        /// <summary>
        /// Implements a pointer-click handler for the entire view.
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClickEvent.Invoke(eventData);

            //this can happen if we restarted a drag due to a failed condition
            if(DragLock)
            {
                OnDragEnd(eventData);
                GamepadInput = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        public void OnPointerDown(PointerEventData eventData)
        {
            //this can happen if we restarted a drag due to a failed condition
            if (DragLock)
            {
                OnDragEnd(eventData);
                GamepadInput = false;
            }
        }

        #endregion

    }
}