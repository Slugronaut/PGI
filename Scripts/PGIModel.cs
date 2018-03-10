/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
//#define PGI_LITE
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.Serialization;
using PowerGridInventory.Utility;
using System.Xml;
using Toolbox.Common;
using System.Linq;
using Toolbox;
using UnityEngine.Assertions;
#if !PGI_LITE
using Pantagruel.Serializer;
#endif

namespace PowerGridInventory
{
    /// <summary>
    /// Performs all backend item-position management
    /// for a grid inventory. Meant to work in tandem with
    /// a <see cref="PGIView"/> to render the model's data
    /// and provide user interactions. Items are expected to
    /// be represented as GameObjects with the <see cref="PGISlotItem"/>
    /// component attached to their root.
    /// </summary>
    [DisallowMultipleComponent]
    //[ExecuteInEditMode]
    [AddComponentMenu("Power Grid Inventory/PGI Model", 10)]
    [Serializable]
    public partial class PGIModel : MonoBehaviour
    {
        #region Local Classes
        /// <summary>
        /// Helper class for storing grid regions.
        /// </summary>
        [Serializable]
        public struct Area
        {
            public int x, y, width, height;

            public Area(int x, int y, int width, int height)
            {
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }
        }

        /// <summary>
        /// Helper class for storing grid locations.
        /// </summary>
        [Serializable]
        public class Pos
        {
            public int X;
            public int Y;

            public Pos(int x, int y)
            {
                X = x;
                Y = y;
            }

            public void Change(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Helper class for storing grid locations.
        /// </summary>
        public struct ImmutablePos
        {
            public int X;
            public int Y;

            public ImmutablePos(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Used in model queries for paring item ids with the required counts.
        /// </summary>
        public struct Query
        {
            public int ItemTypeId;
            public int Count;
            public GatherMode Mode;
            public GatherSource Source;

            public Query(int itemTypeIdHash, int count, GatherMode mode = GatherMode.IncompleteStacksFirst, GatherSource source = GatherSource.GridAndEquipment)
            {
                ItemTypeId = itemTypeIdHash;
                Count = count;
                Mode = mode;
                Source = source;
            }

            public Query(int itemTypeId, GatherMode mode = GatherMode.IncompleteStacksFirst, GatherSource source = GatherSource.GridAndEquipment)
            {
                ItemTypeId = itemTypeId;
                Count = -1;
                Mode = mode;
                Source = source;
            }
        }

        public enum GatherMode
        {
            IncompleteStacksFirst,
            CompleteStacksFirst,
        }

        public enum GatherSource
        {
            GridAndEquipment,
            GridOnly,
            EquipmentOnly,
        }

        enum ExistsIn
        {
            NoModel,
            AnotherModel,
            ThisModelComplete,
            ThisModelIncomplete,
            ThisModelInvalid,
        }

        public enum ArrangeCorner
        {
            TopLeft,
            BottomLeft,
            TopRight,
            BottomRight,
        }

        public enum ArrangeDirection
        {
            HorizontalFirst,
            VerticalFirst,
        }
        

        #endregion


        #region Members
        /// <summary>
        /// If set, this model will automatically scan for items that have entered or left its transform
        /// hierarchy and add or remove them from the model as needed.
        /// </summary>
        [HideInInspector]
        public bool AutoDetectItems
        {
            get { return _AutoDetectItems; }
            set
            {
                _AutoDetectItems = value;
                if (Application.isPlaying && value && AutoDetector == null)
                {
                    AutoDetector = StartCoroutine(AutoDetectFunc());
                }
                if(Application.isPlaying && !value && AutoDetector != null)
                {
                    StopCoroutine(AutoDetector);
                }
            }
        }
        [Tooltip("If set, this model will automatically scan for items that have entered or left its transform hierarchy and add or remove them from the model as needed.")]
        [HideInInspector]
        [SerializeField]
        private bool _AutoDetectItems = false;

        /// <summary>
        /// The number of seconds between each attempt at automatically
        /// detecting any new items found in this model's hierarchy. If set
        /// to a negative value, no detection is used.
        /// </summary>
        [Tooltip("The number of seconds between each attempt at automatcially detecting any new items found in, or lost from, this model's hierarchy.")]
        [HideInInspector]
        [SerializeField]
        public float AutoDetectRate = 1.0f;
        private Coroutine AutoDetector;

        /// <summary>
        /// Cached list used by the auto item detector.
        /// </summary>
        private List<Transform> CachedChildren = new List<Transform>(10);


        /// <summary>
        /// Determines if this PGI model can change an item's parent transform
        /// in the heirarchy when moving items to and from inventories.
        /// </summary>
        [Tooltip("Determines if this PGI model can change an item's parent transform in the hierarchy when moving items to and from inventories.\n\nIMPORTANT: This should be left on in most cases since the save/load functionality requires an inventory's items to be children of it in order for them to be saved.")]
        [FormerlySerializedAs("MessWithItemTransforms")]
        public bool ModifyTransforms = true;

        /// <summary>
        /// The location to dump items if they are automatically detected in
        /// this model but cannot be stored and must be dropped from the inventory.
        /// </summary>
        [Tooltip("The location to dump items if they are automatically detected in this model but cannot be stored and must be dropped from the inventory.")]
        [HideInInspector]
        public Transform DefaultDumpLocation = null;

        /// <summary>
        /// Determines if this model can place items in any valid equipment slots
        /// when inserting an item into an inventory using <see cref="PGIModel.Pickup"/>. 
        /// It is also used when calling <see cref="PGIModel.FindFirstFreeSpace"/>. 
        /// </summary>
        /// <seealso cref="PGIModel.EquipmentSlots"/>
        /// <seealso cref="PGISlot"/>
        [Tooltip("Determines if this model can place items in any valid equipment slots when inserting an item into an inventory.")]
        public bool AutoEquip = true;

        /// <summary>
        /// Determines if this model should attempt to store items in valid equipment
        /// slots before using the grid when inserting an item into the inventory using <see cref="PGIModel.Pickup"/>.
        /// </summary>
        /// <seealso cref="PGIModel.EquipmentSlots"/>
        /// <seealso cref="PGISlot"/>
        [Tooltip("Determines if this model should attempt to store items in valid equipment slots before using the grid when inserting an item into the inventory.")]
        public bool AutoEquipFirst = true;

        /// <summary>
        /// Determines if this model should attempt to stack like items as they enter the inventory using <see cref="PGIModel.Pickup"/>.
        /// </summary>
        [Tooltip("Determines if this model should attempt to stack like items as they enter the inventory.")]
        public bool AutoStack = true;

        /// <summary>
        /// If set, this model allows socketable items to be stored in grid locations containing socketed items and thus activate socketing functionality between the two.
        /// </summary>
        [Tooltip("If set, this model allows socketable items to be stored in grid locations containing socketed items and thus activate socketing functionality between the two.")]
        public bool AllowSocketing = true;
        

        /// <summary>
        /// The number of cell-columns this model will provide for the grid.
        /// It may be zero, in which case there will be no grid.
        /// </summary>
        public int GridCellsX
        {
            get { return _GridCellsX; }
            set
            {
                if (value != _GridCellsX) RefreshGridSize(value, _GridCellsY);
            }
        }
        [Tooltip("The number of cell-columns this model will provide for the grid. It may be zero, in which case there will be no grid.")]
        [SerializeField]
        [Range(0, 1000)]
        private int _GridCellsX = 10;

        /// <summary>
        /// The number of cell-rows this model will provide for the grid.
        /// It may be zero, in which case there will be no grid.
        /// </summary>
        public int GridCellsY
        {
            get { return _GridCellsY; }
            set
            {
                if (value != _GridCellsY) RefreshGridSize(_GridCellsX, value);

            }
        }
        [Tooltip("The number of cell-rows this model will provide for the grid. It may be zero, in which case there will be no grid.")]
        [SerializeField]
        [Range(0, 1000)]
        private int _GridCellsY = 4;
        
        public int EquipmentCellsCount { get { return ECells.Count; } }
        public PGISlot[] EquipmentSlots;


        bool ModelReady = false;
        bool EquipCellsReady = false;

        /// <summary>
        /// Returns false if the model isn't currentlt in a valid state.
        /// </summary>
        public bool Invalid
        {
            get
            {
                //TODO: check grid size to ensure it matches actual array
                return Cells == null;
            }
        }
        
        /// <summary>
        /// The internal list of equipped items.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        protected List<CellModel> ECells = new List<CellModel>();
        

        /// <summary>
        /// Returns a list of all items within this model's grid.
        /// </summary>
        public List<PGISlotItem> GridItems
        {
            get
            {
                return GetRangeContents(0, 0, GridCellsX, GridCellsY);
            }
        }

        /// <summary>
        /// Returns a list of all items equipped in this model.
        /// </summary>
        public List<PGISlotItem> EquipmentItems
        {
            get { return ECells.Select((m)=>m.Item).Where((x) => x != null).ToList(); }
        }

        /// <summary>
        /// Returns a list of all <see cref="PGISlotItem"/>s in this model, including both the grid and equipment slots.
        /// </summary>
        public List<PGISlotItem> AllItems
        {
            get
            {
                List<PGISlotItem> items = new List<PGISlotItem>(GetRangeContents(0, 0, GridCellsX, GridCellsY));
                items.AddRange(EquipmentItems);
                return items;
            }
        }

        /// <summary>
        /// Used to determine if this model's internal grid has been setup. This is
        /// mostly used internally to determine if we are running in edit mode where
        /// the grid hasn't been initialized or play-mode where it has.
        /// </summary>
        public bool IsInitialized
        {
            get { return Cells != null; }
        }
        
        
        private CellModel[,] Cells;

        protected bool _CanPerformAction = true;
        public bool CanPerformAction
        {
            get { return _CanPerformAction; }
            set
            {
                if (!value) _CanPerformAction = false;
            }
        }
        
        /// <summary>
        /// Used to manually reset the CanPerformAction flag within the model.
        /// </summary>
        public void ResetAction()
        {
            _CanPerformAction = true;
        }

        /// <summary>
        /// Helper used to simplfy the process of allow 'Can' actions
        /// </summary>
        /// <returns></returns>
        bool DidSucceed(bool state)
        {
            _CanPerformAction = state;
            CanPerformAction = state;
            return state;
        }

        /// <summary>
        /// Since we will likely have far fewer cells disabled and enabled, rather than keep a flag
        /// for each cell, we are simply going to keep a list of the coordinates currently disabled.
        /// </summary>
        [HideInInspector]
        [SerializeField]
        List<Pos> _DisabledCells = new List<Pos>();

        /// <summary>
        /// Returns a copy of the list of cells coordinates in this model that are currently disabled.
        /// </summary>
        public Pos[] DisabledCells
        {
            get { return _DisabledCells.ToArray(); }
        }

        /// <summary>
        /// This is an un-safe accessor to the model's internal list of
        /// disabled cells. It is provided so that the view can access the
        /// list without out incuring the cost of garbage. Do not modify the
        /// contents of this list! It is much safer to use the 
        /// <see cref="=DisabledCells"/> property instead.
        /// </summary>
        public List<Pos> DisabledCellsUnsafe
        {
            get { return _DisabledCells; }
        }

        //cached and shared for better memory footprint
        static List<Pos> TempPoses = new List<Pos>();
        static List<PGISlotItem> TempItems = new List<PGISlotItem>();
        #endregion


        #region PGI Event Fields
        /// <summary>
        /// Invoked after an item has first entered into this <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelEvent OnItemEnteredModel = new ModelEvent();

        /// <summary>
        /// Invoked after an item has been dropped from this <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelEvent OnItemExitedModel = new ModelEvent();

        /// <summary>
        /// Invoked when this item is about to be removed from a cell location in a <see cref="PGIModel"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ModelUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelFailableEvent OnCanRemoveItem = new ModelFailableEvent();

        /// <summary>
        /// Invoked when this item is about to be stored in cell location in a <see cref="PGIModel"/>.
        /// You can disallow this action by setting the the provided model's
        /// <see cref="PGIModel.CanPerformAction"/> to <c>false</c>.
        /// <seealso cref="ModelUpdateEvent"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelFailableEvent OnCanStoreItem = new ModelFailableEvent();

        /// <summary>
        /// Invoked after this item has removed from a <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnRemoveItem = new ModelUpdateEvent();

        /// <summary>
        /// Invoked after this item has been stored in a new <see cref="PGIModel"/>'s inventory.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnStoreItem = new ModelUpdateEvent();

        /// <summary>
        /// Invoked after an item has failed to be stored in an inventory. Usually this is
        /// the result of a 'Can...' method disallowing the item to be stored or simply
        /// the fact that there is not enough room for the item.
        /// <seealso cref="ModelUpdateEvent"/>
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public ModelUpdateEvent OnStoreItemFailed = new ModelUpdateEvent();

        [SerializeField]
        [FoldFlag("Events")]
        public bool FoldedEvents = false; //used by the inspector

        /// <summary>
        /// Invoked when the model's grid is about to change size.
        /// </summary>
        /// <seealso cref="PGIModel.RefreshGridSize"/>
        [SerializeField]
        [PGIFoldedEvent]
        public UnityEvent OnBeginGridResize = new UnityEvent();

        /// <summary>
        /// Invoked just after a model's grid has finished resizing.
        /// </summary>
        /// <seealso cref="PGIModel.RefreshGridSize"/>
        [SerializeField]
        [PGIFoldedEvent]
        public UnityEvent OnEndGridResize = new UnityEvent();
        #endregion


        #region Methods
        void Awake()
        {
            InitGrid();
            InitEquipment();
        }

        void OnEnable()
        {
            AutoDetectItems = _AutoDetectItems;//this is used to get the ball rolling with the coroutines
            InitGrid();
            InitEquipment();
            //StartCoroutine(DelayAction(InitEquipment));
        }
        
        IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }

        /// <summary>
        /// Helper used to insitialize the model's internal storage.
        /// </summary>
        void InitGrid()
        {
            if (ModelReady) return;
            RefreshGridSize(_GridCellsX, _GridCellsY);
            ModelReady = true;
        }

        /// <summary>
        /// Much of the data within the CellModel is actually not serializable so we will set it here at startup.
        /// </summary>
        void InitEquipment()
        {
            if (EquipCellsReady && EquipmentSlots.Length == ECells.Count) return;

            //TODO: make sure all equipment slots have PGICellModel components
            ECells = new List<CellModel>(EquipmentSlots.Length);
            for (int i = 0; i < EquipmentSlots.Length; i++)
            {
                var cell = EquipmentSlots[i].GetComponent<PGICellModel>();
                if (cell == null)
                    cell = EquipmentSlots[i].gameObject.AddComponent<PGICellModel>();
                cell.InitCell(i, this);
                ECells.Add(cell.Cell);
                InformEventHandlers(cell.gameObject, true, cell.Cell);
            }

            EquipCellsReady = true;
            InformEventHandlers(gameObject, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="register"></param>
        void InformEventHandlers(GameObject root, bool register)
        {
            var handlers = root.GetComponents<IModelEventHandler>();
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var handler = handlers[i];
                    if (register)
                    {
                        CellModel cell = null;
                        if (handler.Type == AbstractBaseModelEventHandler.CellType.Equipment)
                            cell = GetEquipmentModel(handler.EquipmentIndex);
                        else cell = GetCellModel(handler.GridX, handler.GridY);
                        //have to check for null model because ResizeGrid handler calls this and we may not have updated equipment yet
                        if (cell != null && cell.Model != null) handler.Register(cell);
                        else handler.Unregister();
                    }
                    else handler.Unregister();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="register"></param>
        void InformEventHandlers(GameObject root, bool register, CellModel forcedModel)
        {
            var handlers = root.GetComponents<IModelEventHandler>();
            if (handlers != null)
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    var handler = handlers[i];
                    if (register)
                    {
                        CellModel cell = forcedModel;
                        //have to check for null model because ResizeGrid handler calls this and we may not have updated equipment yet
                        if (cell != null && cell.Model != null) handler.Register(cell);
                        else handler.Unregister();
                    }
                    else handler.Unregister();
                }
            }
        }

        /// <summary>
        /// Used to periodically discover new items entering the model in the hierarchy
        /// without having registed in the model itself.
        /// </summary>
        /// <returns></returns>
        IEnumerator AutoDetectFunc()
        {
            //skip first frame so that we can ensure all view objects have initialized properly
            yield return null;
            while (AutoDetectItems)
            {
                yield return new WaitForSeconds(AutoDetectRate);
                UpdateModelList(DefaultDumpLocation, Cells, _GridCellsX, _GridCellsY);
            }

            AutoDetector = null;
        }

        /// <summary>
        /// Helper method used to determine if and how an item may belong to this model.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private ExistsIn ItemExistsInGridArray(PGISlotItem item, CellModel[,] grid, int gridWidth, int gridHeight)
        {
            if (item.Model == null) return ExistsIn.NoModel;
            if (item.Model != this) return ExistsIn.AnotherModel;
            if (item != null)
            {
                //Does this item exist in our equipment list?
                if (item.IsEquipped && item.Equipped < ECells.Count)
                {
                    if (ECells[item.Equipped].Item == item)
                        return ExistsIn.ThisModelComplete;
                    else return ExistsIn.ThisModelIncomplete;
                }

                //check for invalid equipment slot
                if (item.IsEquipped && item.Equipped >= ECells.Count)
                    return ExistsIn.ThisModelInvalid;

                //confirm our location is valid
                if (item.IsStored)
                {
                    if (item.xInvPos < 0 || item.xInvPos + item.CellWidth > gridWidth ||
                        item.yInvPos < 0 || item.yInvPos + item.CellHeight > gridHeight)
                    {
                        return ExistsIn.ThisModelInvalid;
                    }
                }

                //Ok then, does this item exist somewhere in our model already?
                for (int j = 0; j < gridHeight; j++)
                {
                    for (int i = 0; i < gridWidth; i++)
                    {
                        if (grid[i, j].Item == item) return ExistsIn.ThisModelComplete;
                    }
                }

            }

            return ExistsIn.ThisModelIncomplete;
        }

        /// <summary>
        /// Scans the GameObject for any <see cref="PGISlotItem"/>s and confirms that they
        /// already exists in this model. If they do not, it attempts to add them to this model.
        /// If there is no room left, the remaining items they are moved in the transform hierarchy
        /// so that their parent is set to the DumpRoot transform.
        /// </summary>
        /// <param name="DumpRoot">The root transform to child object to if they are found to not
        /// belong and have no information about where they came from.</param>
        /// <param name="gridList">An option array list that represents the grid's contents. This
        /// is intended for internal use when resizing the grid and should not be needed by most users.</param>
        private void UpdateModelList(Transform DumpRoot, CellModel[,] gridList, int gridWidth, int gridHeight)
        {
            //Check for items registed with the model that are not part of the heirarchy
            List<PGISlotItem> items = GetGridContents(gridList, 0, 0, gridWidth, gridHeight);// GetRangeContents(0, 0, GridCellsX, GridCellsY);


            foreach (PGISlotItem item in items)
            {
                if (item.transform.parent != this.transform)
                    Drop(item);
            }
            items = EquipmentItems; //is this safe now?
            if (items != null)
            {
                foreach (PGISlotItem item in items)
                {
                    if (item.transform.parent != this.transform) Drop(item);
                }
            }

            //Now check for items that are in the model hierarchy but aren't registed with the model.
            CachedChildren.Clear();
            //we need to cache a list of children so that if we dump any
            //we still process everything correctly.
            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                CachedChildren.Add(transform.GetChild(childIndex));
            }

            //check for 
            foreach (Transform child in CachedChildren)
            {
                GameObject go = child.gameObject;
                PGISlotItem item = go.GetComponent<PGISlotItem>();
                if (item != null)
                {
                    bool autoEquip = AutoEquip;
                    ExistsIn state = ItemExistsInGridArray(item, gridList, gridWidth, gridHeight);
                    switch (state)
                    {
                        case ExistsIn.AnotherModel:
                            {
                                //This item was added to this model's hierarchy but
                                //is still registed with another model. Remove it
                                //from that model and then add it to this one now.
                                //If we succeed then the item will be a valid child
                                //of this model, otherwise it will be properly dumped.
                                AutoEquip = false;
                                item.Model.Drop(item);
                                Pickup(item);
                                AutoEquip = autoEquip;
                                break;
                            }
                        case ExistsIn.NoModel:
                            {
                                //This item was added to this model's hierarchy but
                                //does not current have internal data pointing to this model.
                                //Simply add it to this model now. If we succeed then the
                                //item will be a valid child of this model, otherwise it
                                //will be properly dumped.
                                //AutoEquip = false;
                                item.transform.SetParent(DumpRoot, false);
                                Pickup(item);
                                //AutoEquip = autoEquip;
                                break;
                            }
                        case ExistsIn.ThisModelComplete:
                            {
                                //Everything is as it should be. Do nothing.
                                break;
                            }

                        case ExistsIn.ThisModelIncomplete:
                            {
                                //This item is referencing this model but its location
                                //information is invalid. Remove it from this model and
                                //then attempt to pick it back up. If we succeed then the
                                //item will be a valid child of this model, otherwise it
                                //will be properly dumped.
                                //AutoEquip = false;
                                Drop(item);
                                Pickup(item);
                                //AutoEquip = autoEquip;
                                break;
                            }
                        case ExistsIn.ThisModelInvalid:
                            {
                                //Our item is registered with this model and it's located
                                //in the model's hierarchy. However, the location within the
                                //model is not valid anymore (most likely due to a grid resizing)
                                //so now we need to see if we can fit it back in, and drop it if we can't.
                                //We do this by actually dropping it first, then attempting to pick it up again.
                                this.Drop(item);
                                this.Pickup(item);
                                break;
                            }
                    }//end switch
                }//endif PGISlotItem
            }
        }

        /// <summary>
        /// Utility for gathering all items stored within a region of grid space.
        /// Intended for internal use when swapping items around during a grid-resize.
        /// </summary>
        /// <param name="gridList">A 2D array representing a grid of slot items.</param>
        /// <param name="x">The x corrdinate to start at.</param>
        /// <param name="y">The y coordinate to start at.</param>
        /// <param name="width">The number of horizontal cells.</param>
        /// <param name="height">The number of vertical cells.</param>
        /// <returns></returns>
        private List<PGISlotItem> GetGridContents(CellModel[,] gridList, int x, int y, int width, int height)
        {
            List<PGISlotItem> list = new List<PGISlotItem>(10);
            for (int t = y; t < y + height; t++)
            {
                for (int s = x; s < x + width; s++)
                {
                    var i = gridList[s, t].Item;
                    if (i != null && !list.Contains(i))
                        list.Add(i);
                }
            }

            return list;
        }

        /// <summary>
        /// Updates the grid to match any changes to the
        /// horizontal or vertical grid count.
        /// </summary>
        /// <remarks>
        /// If this method is used in real-time during gameplay some items
        /// may be automatically re-arranged or even dropped if there is not
        /// enough room for them in the new grid.
        /// </remarks>
        private void RefreshGridSize(int newWidth, int newHeight)
        {
            if (newWidth < 0) newWidth = 0;
            if (newHeight < 0) newHeight = 0;

            OnBeginGridResize.Invoke();

            if (Cells == null || !Application.isPlaying)
            {
                _GridCellsX = newWidth;
                _GridCellsY = newHeight;

                Cells = new CellModel[GridCellsX, GridCellsY];
                for(int y = 0; y < _GridCellsY; y++)
                {
                    for (int x = 0; x < _GridCellsX; x++)
                        Cells[x, y] = new CellModel(x, y, this);
                }
                    

                //let everyone know we are done
                OnEndGridResize.Invoke();
                InformEventHandlers(gameObject, true);

                //this is likely the first time we've initialized the grid. Don't worry about the other stuff.
                //We can infer this because the 'Slots' field was null.
                return;
            }

            //copy old contents before we dump anything and resize the internal grid.

            //dump everything from the grid.
            //WARNING: This will trigger all events so we may get
            //weird things like sound effect playing, graphical glitches, etc.
            var droppedItems = GetGridContents(Cells, 0, 0, _GridCellsX, _GridCellsY);
            foreach (PGISlotItem item in droppedItems) Drop(item);

            //resize the grid
            _GridCellsX = newWidth;
            _GridCellsY = newHeight;
            Cells = new CellModel[GridCellsX, GridCellsY];
            for (int y = 0; y < _GridCellsY; y++)
            {
                for (int x = 0; x < _GridCellsX; x++)
                    Cells[x, y] = new CellModel(x, y, this);
            }

            //try to pickup eveything we just dropped.
            //BUG: This currently causes some slots to not reset properly. Not sure what is happening here.
            bool autoEquip = AutoEquip;
            AutoEquip = false;
            foreach (PGISlotItem item in droppedItems) Pickup(item);
            AutoEquip = autoEquip;

            //let everyone know we are done
            OnEndGridResize.Invoke();
            InformEventHandlers(gameObject, true);
        }

        /// <summary>
        /// Confirms that the given position is within
        /// the confines of the grid.
        /// </summary>
        /// <returns><c>true</c>, if valid cell position was confirmed, <c>false</c> otherwise.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public bool ConfirmValidCellPos(int x, int y)
        {
            if (Cells == null) return false; //need this due to some odd race condition
            if (x >= GridCellsX) return false;
            if (y >= GridCellsY) return false;
            if (x < 0) return false;
            if (y < 0) return false;
            return true;
        }

        /// <summary>
        /// Confirms that the given position and dimension are within
        /// the confines of the grid.
        /// </summary>
        /// <returns><c>true</c>, if valid cell range was confirmed, <c>false</c> otherwise.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public bool ConfirmValidCellRange(int x, int y, int width, int height)
        {
            if (ConfirmValidCellPos(x, y))
            {
                if (x + width > GridCellsX) return false;
                if (y + height > GridCellsY) return false;
                return true;
            }
            return false;
        }
        #endregion


        #region Can... Methods
        /// <summary>
        /// Determines if an item can be stored at the location (x,y).
        /// The location and required surrounding cells must be empty.
        /// </summary>
        /// <returns><c>true</c> if this model's grid can store the specified item at x y; otherwise, <c>false</c>.</returns>
        /// <param name="item">The item to be stored.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="filter">An optional list of items to filter out when checking for available space
        /// Useful when you want to swap specific items.</param>
        bool InternalCanStoreAt(PGISlotItem item, int x, int y, List<PGISlotItem> filter = null)
        {
            if (item == null)
                return DidSucceed(false);
            if (DisabledCellsInRange(x, y, item.CellWidth, item.CellHeight).Count > 0)
                return DidSucceed(false);
            var items = this.GetRangeContents(x, y, item.CellWidth, item.CellHeight, filter);
            if (items == null || items.Count > 0)
                return DidSucceed(false);

            var cell = GetCellModel(x, y);
            if (cell == null || cell.Blocked)
                DidSucceed(false);
            
            return TriggerEvents(EventClass.CanStore, item, GetCellModel(x, y));
        }

        /// <summary>
        /// Determines if the item can be stacked with the contents of the given slot.
        /// </summary>
        /// <param name="item">The item to stack.</param>
        /// <param name="slot">The slot that is being checked for stack compatibility.</param>
        /// <returns></returns>
        bool InternalCanStackEquipment(PGISlotItem item, CellModel cell)
        {
            if (cell == null || cell.Blocked)
                return DidSucceed(false);
            var cellItem = cell.Item;

            if (item == null || cellItem == null)
                return DidSucceed(false);

            //there is exactly 1 item here, check to see if we can stack
            if (item.MaxStack <= 1 || cellItem.MaxStack <= 1 ||
                cellItem.ItemTypeId.Hash != item.ItemTypeId.Hash ||
                item.ItemTypeId.Hash == 0 ||
                cellItem.StackCount >= cellItem.MaxStack)
                return DidSucceed(false);

            //looks like we can stack
            return TriggerEvents(EventClass.CanStore, item, cell);
        }

        /// <summary>
        /// Determines if an item can be stacked with the item specifically at the given area.
        /// </summary>
        /// <param name="ite"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        bool InternalCanStack(PGISlotItem item, CellModel cell)
        {

            if (item == null || cell.Item == null || cell.Blocked)
                return DidSucceed(false);

            var destItem = cell.Item;
            if (item.MaxStack <= 1 || destItem.MaxStack <= 1 ||
                destItem.ItemTypeId.Hash != item.ItemTypeId.Hash ||
                item.ItemTypeId.Hash == 0 ||
                destItem.StackCount >= destItem.MaxStack)
                return DidSucceed(false);

            //looks like we can stack
            return TriggerEvents(EventClass.CanStore, item, cell);
        }
        
        /// <summary>
        /// Determines if an item can be stacked with any item potentially located on the grid at the specified region.
        /// </summary>
        /// <param name="item">The item to be stacked.</param>
        /// <param name="x">The left corner of the region to check for stack-compatible items.</param>
        /// <param name="y">The top corner of the region to check for stack-compatible items.</param>
        /// <returns></returns>
        bool InternalCanStackAt(PGISlotItem item, int x, int y)
        {
            if (item == null)
                return DidSucceed(false);
            var items = this.GetRangeContents(x, y, item.CellWidth, item.CellHeight);
            if (items == null)
                return DidSucceed(false);
            else if (items.Count > 1)
                return DidSucceed(false);
            //TODO: it might be better to check all items for a valid stack target
            else if (items.Count == 1)
            {
                //there is exactly 1 item here, check to see if we can stack
                if (item.MaxStack <= 1 || items[0].MaxStack <= 1 ||
                    items[0].ItemTypeId.Hash != item.ItemTypeId.Hash ||
                    item.ItemTypeId.Hash == 0 ||
                    items[0].StackCount >= items[0].MaxStack)
                    return DidSucceed(false);

                //looks like we can stack
                return TriggerEvents(EventClass.CanStore, item, GetCellModel(x, y));
            }

            //there is nothing to stack
            return DidSucceed(false);
        }

        /// <summary>
        /// Determines if this instance can equip the specified item.
        /// </summary>
        /// <returns><c>true</c> if this model can equip the specified item in the given slot; otherwise, <c>false</c>.</returns>
        /// <param name="item">The item to equip.</param>
        /// <param name="equipSlot">The slot to equip the item to.</param>
        public bool InternalCanEquip(PGISlotItem item, CellModel cell, List<PGISlotItem> filter = null)
        {
            if (cell.Blocked || (cell.Item != null && (filter == null || !filter.Contains(cell.Item))))
                return DidSucceed(false);

            return TriggerEvents(EventClass.CanStore, item, cell);
        }

        /// <summary>
        /// Can the give item be stored within the given cell model?
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        public bool CanStore(PGISlotItem item, CellModel dest, List<PGISlotItem> filter = null)
        {
            if (item == null || dest == null)
                return DidSucceed(false);
            if (dest.IsEquipmentCell)
                return InternalCanEquip(item, dest, filter);
            else return InternalCanStoreAt(item, dest.xPos, dest.yPos, filter);
        }

        /// <summary>
        /// Determines if the item can be dropped from this inventory.
        /// </summary>
        /// <returns><c>true</c> if this model can drop the specified item; otherwise, <c>false</c>.</returns>
        /// <param name="item">The item in question.</param>
        public bool CanDrop(PGISlotItem item)
        {
            if (item == null)
                return DidSucceed(false);
            return TriggerEvents(EventClass.CanRemove, item, item.Cell);
        }

        /// <summary>
        /// Determines if the item can be stacked with the contents of a given cell.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        public bool CanStack(PGISlotItem item, CellModel cell)
        {
            if (item == null || cell == null)
                return DidSucceed(false);
            if (cell.IsEquipmentCell)
                return InternalCanStackEquipment(item, cell);
            else return InternalCanStackAt(item, cell.xPos, cell.yPos);
        }

        /// <summary>
        /// Determines if the dest item can be swapped out with the source item.
        /// It is assumed that the dest item will no longer exist within the model.
        /// </summary>
        /// <param name="srcItem"></param>
        /// <param name="destItem"></param>
        /// <returns></returns>
        public bool CanSwap(PGISlotItem item, CellModel dest)
        {
            if (item == null || dest == null)
                return DidSucceed(false);

            TempItems.Clear();

            if (dest.IsEquipmentCell && dest.Item != null)
                TempItems.Add(dest.Item);
            else
            {
                var contents = GetRangeContents(dest.xPos, dest.yPos, item.CellWidth, item.CellHeight);
                if (contents != null && contents.Count == 1)
                    TempItems.Add(contents[0]);
            }

            return CanStore(item, dest, TempItems);
        }

        #if !PGI_LITE
        /// <summary>
        /// Checks to see if a socketable item can be placed into a socketed item while contained within this inventory.
        /// </summary>
        /// <param name="socketable">The item that is being placed into another socketed item.</param>
        /// <param name="socketed">The item receiving the socketable.</param>
        /// <returns><c>true</c> if the items are compatible, <c>false</c> otherwise.</returns>
        public bool CanSocket(PGISlotItem socketable, PGISlotItem socketed)
        {
            if (!AllowSocketing)
                return DidSucceed(false);
            if (socketable == null || socketed == null)
                return DidSucceed(false);

            var item = socketable.GetComponent<Socketable>();
            var receiver = socketed.GetComponent<Socketed>();
            if (item == null || receiver == null)
                return DidSucceed(false);

            var socket = receiver.GetFirstEmptySocket();
            if (socket != null)
            {
                _CanPerformAction = true;
                receiver.OnCanSocket.Invoke(ActionFailed, item, socket);
                receiver.OnCanSocket.Invoke(ActionFailed, item, socket);
                return CanPerformAction;
            }
            else return DidSucceed(false);
        }
        #endif

        /// <summary>
        /// Returns the number of other items contending for the same space in the model.
        /// For equipment, this will always be 0 or 1. For grid locations this can be 0
        /// or greater depending on the size of the item and how many other items are stored.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public List<PGISlotItem> SpaceConflicts(PGISlotItem item, CellModel dest)
        {
            if (item == null || dest == null) return null;

            if (dest.IsEquipmentCell)
            {
                List<PGISlotItem> list = new List<PGISlotItem>(1);
                list.Add(dest.Item);
                return list;
            }

            return GetRangeContents(dest.xPos, dest.yPos, item.CellWidth, item.CellHeight);
        }

        /// <summary>
        /// Returns the first valid cell the item can be stored at that has at least one overlapping cell with the original destination
        /// when taking the item's dimensions into account.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public CellModel FirstValidOverlappingCell(PGISlotItem item, CellModel dest, params PGISlotItem[] filter)
        {
            if (item == null || dest == null || dest.IsEquipmentCell) return null;

            for (int y = 0; y < item.CellHeight; y++)
            {
                for (int x = 0; x < item.CellWidth; x++)
                {
                    if (InternalCanStoreAt(item, dest.xPos + x, dest.yPos + y, new List<PGISlotItem>(filter)))
                        return GetCellModel(dest.xPos, dest.yPos);
                }
            }

            return null;
        }
        #endregion


        #region Public Methods
        /// <summary>
        /// Invoked as a callback in 'Can' events.
        /// </summary>
        void ActionFailed()
        {
            CanPerformAction = false;
        }

        /// <summary>
        /// Helper function for trigger all relevant calls when performing an event action.
        /// </summary>
        /// <param name="ec"></param>
        /// <param name="item"></param>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public bool TriggerEvents(EventClass ec, PGISlotItem item, CellModel cell)
        {
            _CanPerformAction = true;

            switch (ec)
            {
                case EventClass.CanRemove:
                    {
                        OnCanRemoveItem.Invoke(ActionFailed, item, cell);
                        if (item != null) item.OnCanRemoveItemModel.Invoke(ActionFailed, item, cell);
                        break;
                    }
                case EventClass.CanStore:
                    {
                        OnCanStoreItem.Invoke(ActionFailed, item, cell);
                        if (item != null) item.OnCanStoreItemModel.Invoke(ActionFailed, item, cell);
                        break;
                    }
                case EventClass.Remove:
                    {
                        OnRemoveItem.Invoke(item, cell);
                        if (item != null) item.OnRemoveItemModel.Invoke(item, cell);
                        break;
                    }
                case EventClass.Store:
                    {
                        OnStoreItem.Invoke(item, cell);
                        if (item != null) item.OnStoreItemModel.Invoke(item, cell);
                        break;
                    }
                case EventClass.Failed:
                    {
                        OnStoreItemFailed.Invoke(item, cell);
                        if (item != null) item.OnStoreItemModelFailed.Invoke(item, cell);
                        break;
                    }
                default:
                    {
                        throw new UnityException("Unsupported event class " + ec.ToString());
                    }
            }//end switch

            return CanPerformAction;
        }

        /// <summary>
        /// Arranges item in inventory with an attempt to minimize space used.
        /// </summary>
        /// <param name="allowRotation">If <c>true</c> items can be rotated to make them fit better.</param>
        public void ArrangeItems(bool allowRotation)
        {
            ArrangeItems(allowRotation, PGISlotItem.RotateDirection.CW);
        }

        /// <summary>
        /// Arranges item in inventory with an attempt to minimize space used.
        /// </summary>
        /// <param name="allowRotation">If <c>true</c> items can be rotated to make them fit better.</param>
        /// <returns></returns>
        public void ArrangeItems(bool allowRotation, PGISlotItem.RotateDirection rotateDir)
        {
            //We're going prioritize what we put back first and base it on the dimensions
            //of the item, dimensions of the grid, and wether or not rotation an be used.
            //There might also be some magic fairy dust in here too.
            PGISlotItem[] sortedItems = new PGISlotItem[GridItems.Count];
            GridItems.CopyTo(sortedItems, 0);
            PGISlotItem.SortByWidth = false; //this may change if we are rotating items below
            Dictionary<PGISlotItem, PGIModel.Pos> origLocations = new Dictionary<PGISlotItem, Pos>(_GridCellsX * _GridCellsY);
            bool smallerWidth = (this._GridCellsX < this._GridCellsY) ? true : false;
            

            //take em all out
            foreach (PGISlotItem item in GridItems)
            {
                origLocations.Add(item, new Pos(item.xInvPos, item.yInvPos));
                //store the location of each item before removing it.
                //That way if our new arrangement doesn't have enough space
                //we can put it all back
                InternalRemove(item, false);
            }


           //first of all, can we rotate?
            if(allowRotation)
            {
                
                //We can rotate. Let's try to rotate all items
                //that are 'closer' to the smallest dimension
                //of the grid itself.
                foreach(var item in sortedItems)
                {
                    //if our inventory is taller than it is wide and the item will have a closer fit by rotating then we'll rotate
                    if(smallerWidth)
                    {
                        if (item.CellWidth < item.CellHeight && item.CellWidth <= _GridCellsX)
                        {
                            if (item.Rotated) item.Rotate(PGISlotItem.RotateDirection.None);
                            else item.Rotate(rotateDir);
                        }
                        //we want to store the widest items first
                        PGISlotItem.SortByWidth = true;
                    }
                    //otherwise, our inventory is wider than tall, and if the item fits better rotated we'll do it that way.
                    else
                    {
                        if (item.CellHeight < item.CellWidth && item.CellHeight <= _GridCellsY)
                        {
                            if (item.Rotated) item.Rotate(PGISlotItem.RotateDirection.None);
                            else item.Rotate(rotateDir);
                        }
                        //we want to store the tallest items first
                        PGISlotItem.SortByWidth = false;
                    }
                }
            }

            //sort items by size
            Array.Sort(sortedItems);
            
            //put em all back
            foreach(PGISlotItem item in sortedItems)
            {
                if(!this.Pickup(item))
                {
                    //If we can't fit something back in, let's just revert to the original positions and be done with it.
                    ReturnItemsToOriginalPosition(origLocations);
                    return;
                }
            }
        }

        /// <summary>
        /// Helper used to return items to their original locations if a re-arrangement can't fit them all in.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="orig"></param>
        void ReturnItemsToOriginalPosition(Dictionary<PGISlotItem, PGIModel.Pos> originalItemLocations)
        {
            foreach (PGISlotItem item in originalItemLocations.Keys)
            {
                if(item.IsStored) InternalRemove(item, false);
            }

            foreach (KeyValuePair<PGISlotItem, PGIModel.Pos> pair in originalItemLocations)
            { 
                PGIModel.StoreItem(pair.Key, GetCellModel(pair.Value.X, pair.Value.Y), false);
            }
        }

        /// <summary>
        /// Given an item, this method will attempt to find the nearest position
        /// that it can fit into when rotated. This new position will have at least
        /// one overlapping cell with the original unrotated position of the item.
        /// </summary>
        /// <param name="preRotatedItem">The item for which to find a rotated vailence position. Take note that the dimension for this item should be the pre-rotated ones.</param>
        /// <returns>The new root cell position that shares rotated vailence or null if none could be found.</returns>
        public Pos FindVailencePosition(PGISlotItem preRotatedItem, PGISlotItem.RotateDirection dir)
        {
            int width = preRotatedItem.CellHeight;
            int height = preRotatedItem.CellWidth;

            //We will start by rotating the object around it's current cell location.
            //If that doesn't work we will shift the object so that each cell slot it takes
            //has an opportunity to be used as a rotation point until we find enough space to
            //perform a successfull rotation. If none are found then this item will not fit
            //within the space it was located when rotated and we must return failure.

            if(dir == PGISlotItem.RotateDirection.CW || (dir == PGISlotItem.RotateDirection.None && preRotatedItem.RotatedDir == PGISlotItem.RotateDirection.CCW))
            {
                //clockwise rotation
                for (int y = preRotatedItem.yInvPos; y < preRotatedItem.yInvPos + height; y++)
                {
                    for (int x = preRotatedItem.xInvPos; x < preRotatedItem.xInvPos + width; x++)
                    {
                        var list = GetRangeContents(x, y, width, height, new List<PGISlotItem>(new PGISlotItem[] { preRotatedItem }));
                        if (list == null) continue;
                        else if (list.Count != 0) continue;
                        return new Pos(x, y);
                    }
                }

                for (int y = preRotatedItem.yInvPos; y > preRotatedItem.yInvPos - height; y--)
                {
                    for (int x = preRotatedItem.xInvPos; x > preRotatedItem.xInvPos - width; x--)
                    {
                        var list = GetRangeContents(x, y, width, height, new List<PGISlotItem>(new PGISlotItem[] { preRotatedItem }));
                        if (list == null) continue;
                        else if (list.Count != 0) continue;
                        return new Pos(x, y);
                    }
                }
            }
            else
            {
                //counter-clockwise rotation
                for (int y = preRotatedItem.yInvPos; y > preRotatedItem.yInvPos - height; y--)
                {
                    for (int x = preRotatedItem.xInvPos; x > preRotatedItem.xInvPos - width; x--)
                    {
                        var list = GetRangeContents(x, y, width, height, new List<PGISlotItem>(new PGISlotItem[] { preRotatedItem }));
                        if (list == null) continue;
                        else if (list.Count != 0) continue;
                        return new Pos(x, y);
                    }
                }

                for (int y = preRotatedItem.yInvPos; y < preRotatedItem.yInvPos + height; y++)
                {
                    for (int x = preRotatedItem.xInvPos; x < preRotatedItem.xInvPos + width; x++)
                    {
                        var list = GetRangeContents(x, y, width, height, new List<PGISlotItem>(new PGISlotItem[] { preRotatedItem }));
                        if (list == null) continue;
                        else if (list.Count != 0) continue;
                        return new Pos(x, y);
                    }
                }
            }

            
           
            return null;
            
        }
        
        /// <summary>
        /// Gets the contents of the given (x,y) location.
        /// If the location is empty or invalid then
        /// 'null' is returned instead.
        /// </summary>
        /// <returns>The <see cref="PGISlotItem"/> stored at the location or null if there isn't one.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public PGISlotItem GetCellContents(int x, int y)
        {
            if (!ConfirmValidCellPos(x, y)) return null;
            return Cells[x, y].Item;
        }

        /// <summary>
        /// Gets the internal cell model at the given (x,y) location.
        /// If the location is emoty or invalid the null is returned instead.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public CellModel GetCellModel(int x, int y)
        {
            if (!ConfirmValidCellPos(x, y)) return null;
            return Cells[x, y];
        }

        /// <summary>
        /// Returns the cell in the equipment list at the given index.
        /// If the index is empty or invalid null is returned instead.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public CellModel GetEquipmentModel(int index)
        {
            if (index < 0 || index >= ECells.Count)
                return null;
            return ECells[index];
        }

        /// <summary>
        /// Returns the item stored at the equipment index on this model.
        /// If the index is invalid or the location is emty, null is
        /// returned instead.
        /// </summary>
        /// <param name="equipmentIndex"></param>
        /// <returns>The item equipped at the given index, or null if there is no item at that index.</returns>
        public PGISlotItem GetEquipmentContents(int equipmentIndex)
        {
            if (equipmentIndex < 0 || equipmentIndex >= ECells.Count)
                return null;
            else return ECells[equipmentIndex].Item;
        }

        /// <summary>
        /// Determines if the slot at the given location is empty.
        /// </summary>
        /// <returns><c>true</c>, if there is no item, <c>false</c> otherwise.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public bool IsCellEmpty(int x, int y)
        {
            return (GetCellContents(x, y) == null);
        }

        /// <summary>
        /// Gets all <see cref="PGISlotItem"/>s  within the given area.
        /// If the area is empty then an empty list is returned.
        /// If the area is invalid then null is returned. 
        /// </summary>
        /// <returns>The range contents as ab array of <see cref="PGISlotItem"/>s.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="filter">An optional list of items to filter out of the returned array.</param> 
        public List<PGISlotItem> GetRangeContents(int x, int y, int width, int height, List<PGISlotItem> filter = null)
        {
            if (!ConfirmValidCellRange(x, y, width, height)) return null;
            List<PGISlotItem> list = new List<PGISlotItem>();
            for (int t = y; t < y + height; t++)
            {
                for (int s = x; s < x + width; s++)
                {
                    var i = GetCellContents(s, t);
                    if (i != null && !list.Contains(i))
                    {
                        if (filter == null || !filter.Contains(i)) list.Add(i);
                    }
                }
            }

            return list;
        }
        
        /// <summary>
        /// Returns a list of cell locations that are currently disabled.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width of range.</param>
        /// <param name="height">height of range.</param>
        /// <returns>A list of Pos objects containing the corrdinates of each disabled location within the range. If non are found, and empty list is returned.
        /// IMPORTANT: The list returned is re-used internally. It should not be cached and should be treated as volitle and not thread safe.
        /// </returns>
        public List<Pos> DisabledCellsInRange(int x, int y, int width, int height)
        {
            if (!ConfirmValidCellRange(x, y, width, height)) return new List<Pos>();

            TempPoses.Clear();
            for (int t = y; t < y + height; t++)
            {
                for (int s = x; s < x + width; s++)
                {
                    var pos = GetDisabledCellPos(s, t);
                    if (pos != null && !TempPoses.Contains(pos))
                        TempPoses.Add(pos);
                }
            }

            return TempPoses;
        }

        /// <summary>
        /// Returns true if there are any disabled cells within the given range of the model grid.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width of range.</param>
        /// <param name="height">height of range.</param>
        /// <returns><c>true</c> if any disabled cells are found, <c>false</c> otherwise.</returns>
        public bool AreDisabledCellsInRange(int x, int y, int width, int height)
        {
            if (!ConfirmValidCellRange(x, y, width, height)) return false;
            
            for (int t = y; t < y + height; t++)
            {
                for (int s = x; s < x + width; s++)
                {
                    if (IsCellDisabled(s, t))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Flags the given location in the model as being enabled or disabled. Disable cells
        /// will be shown as 'blocked' within the view and will not allow items to be moved
        /// to/from them.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="state"></param>
        public void EnableCell(int x, int y, bool state)
        {
            Assert.IsTrue(x >= 0);
            Assert.IsTrue(x < _GridCellsX);
            Assert.IsTrue(y >= 0);
            Assert.IsTrue(y < _GridCellsY);
            

            //first check to see if we already have this cell stored
            var pos = GetDisabledCellPos(x, y);
            if (pos != null)
            {
                if (state) _DisabledCells.Remove(pos);
                return;
            }
            //we can presume we didn't already find our state, do we add it to the list?
            else if (!state) _DisabledCells.Add(new Pos(x, y));
        }

        /// <summary>
        /// Is the cell at the given location flagged as disabled?
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool IsCellDisabled(int x, int y)
        {
            Assert.IsTrue(x >= 0, "Cell location ("+x+","+y+") isn't valid.");
            Assert.IsTrue(x < _GridCellsX, "Cell location (" + x + "," + y + ") isn't valid.");
            Assert.IsTrue(y >= 0, "Cell location (" + x + "," + y + ") isn't valid.");
            Assert.IsTrue(y < _GridCellsY, "Cell location (" + x + "," + y + ") isn't valid.");
            
            for (int i = 0; i < _DisabledCells.Count; i++)
            {
                var cell = _DisabledCells[i];
                if (cell.X == x && cell.Y == y)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the cell at the given grid location is blocked.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool IsCellBlocked(int x, int y)
        {
            Assert.IsTrue(x >= 0, "Cell location (" + x + "," + y + ") isn't valid.");
            Assert.IsTrue(x < _GridCellsX, "Cell location (" + x + "," + y + ") isn't valid.");
            Assert.IsTrue(y >= 0, "Cell location (" + x + "," + y + ") isn't valid.");
            Assert.IsTrue(y < _GridCellsY, "Cell location (" + x + "," + y + ") isn't valid.");
            return GetCellModel(x, y).Blocked;
        }

        /// <summary>
        /// Returns true if the cell at the given equipment index is blocked.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool IsCellBlocked(int index)
        {
            Assert.IsTrue(index > 0, "Equipment index " + index + " is invalid.");
            Assert.IsTrue(index < ECells.Count, "Equipment index " + index + " is invalid.");
            return ECells[index].Blocked;
        }

        /// <summary>
        /// Helper for getting the Pos object for a disabled cell.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        Pos GetDisabledCellPos(int x, int y)
        {
            Assert.IsTrue(x >= 0);
            Assert.IsTrue(x < _GridCellsX);
            Assert.IsTrue(y >= 0);
            Assert.IsTrue(y < _GridCellsY);

            for (int i = 0; i < _DisabledCells.Count; i++)
            {
                var cell = _DisabledCells[i];
                if (cell.X == x && cell.Y == y)
                    return _DisabledCells[i];
            }

            return null;
        }

        /// <summary>
        /// Determines if the given grid-area is empty.
        /// </summary>
        /// <returns><c>true</c>, if is empty was ranged, <c>false</c> otherwise.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="filter">An optional list of items to filter out of searched area.</param>
        public bool RangeIsEmpty(int x, int y, int width, int height, List<PGISlotItem> filter = null)
        {
            var contents = GetRangeContents(x, y, width, height, filter);
            if (contents != null && contents.Count < 1) return true;
            return false;
        }
        
        /// <summary>
        /// Checks to see if the model can store the item anywhere. Optionally can include
        /// equipment and stacks when looking for space.
        /// </summary>
        /// <param name="item">The item to check for room.</param>
        /// <returns><c>true</c> if this model has room for the item, <c>false</c> otherwise.</returns>
        public bool CanStoreAnywhere(PGISlotItem item, bool checkEquipSlot, bool checkStacks, List<PGISlotItem> filter)
        {
            if (item == null) return false;

            for (int t = 0; t < GridCellsY; t++)
            {
                for (int s = 0; s < GridCellsX; s++)
                {
                    var cell = GetCellModel(s, t);
                    if (CanStore(item, cell))
                        return true;
                    if (checkStacks && InternalCanStack(item, cell))
                        return true;
                }
            }

            if (checkEquipSlot)
            {
                int len = ECells.Count;
                for(int i = 0; i < len; i++)
                {
                    var cell = ECells[i];
                    if (InternalCanEquip(item, cell))
                        return true;
                    if (checkStacks && InternalCanStackEquipment(item, cell))
                        return true;
                }
            }

            return false;
        }

        #endregion


        #region Storage Commands
        /// <summary>
        /// Specialized method that is used internally by the model when storing like-items.
        /// It is called within the <see cref="PGIModel.InternalStoreAre"/> and <see cref="PGIModel.Equip"/> methods.
        /// </summary>
        /// <remarks>
        /// This method should be considered highly volitile as it will usually destroy the object
        /// being stacked. Stacking works by destroying the object to be stacked and incrementing
        /// the stack counter stored internally in the item that it was stacked with. Later. they can
        /// be unstacked by de-incrementing the counter and instantiating a copy of the item.
        /// <para>
        /// As a concequence of stacking you cannot expect unique items with different internal data
        /// to be properly stored and retreived and should only supply the same stack ID to items
        /// that are exactly alike.
        /// </para>
        /// </remarks>
        /// <returns><c>null</c> if the item was fully stacked and destroyed with no remainder.
        /// Otherwise a reference to the item with the remainder will be returned.</returns>
        /// <param name="item">The item to stack. This item may be destroyed by the end of this method.</param>
        /// <param name="stack">The item that the item will be stacked with.</param>
        /// <param name="stackOccurred">A flag that signifies if the item was stacked in any way.</param>
        PGISlotItem StackStore(PGISlotItem item, PGISlotItem stack, out bool stackOccurred)
        {
            if (item == null) throw new UnityException("Invalid item passed to PGIModel.StackStore()");
            if (stack == null) throw new UnityException("Invalid stack passed to PGIModel.StackStore()");

            stackOccurred = false;
            if (item.ItemTypeId.Hash == stack.ItemTypeId.Hash && stack.StackCount < stack.MaxStack)
            {
                stack.StackCount += item.StackCount;
                int remainder = stack.StackCount - stack.MaxStack;
                if (remainder <= 0)
                {
                    //nothing left
                    GameObject.Destroy(item.gameObject);
                    stackOccurred = true;
                    TriggerEvents(EventClass.Store, item, stack.Cell);
                    return null;
                }
                else
                {
                    //there was a remainder
                    stack.StackCount = stack.MaxStack;
                    item.StackCount = remainder;
                    stackOccurred = true;
                }
            }

            TriggerEvents(EventClass.Store, item, stack.Cell);
            return item;
        }

        #if !PGI_LITE
        /// <summary>
        /// Attempts to attach one socketable item to another socketed one.
        /// </summary>
        /// <param name="receiver">The socketed item that will receive the other.</param>
        /// <param name="thing">The socketable item that will be attached.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        bool Socket(Socketed receiver, Socketable thing)
        {
            var socket = receiver.AttachSocketable(thing);
            if (socket != null)
            {
                thing.transform.SetParent(receiver.transform, false);
                receiver.OnSocketed.Invoke(thing, socket);
                thing.OnSocketed.Invoke(thing, socket);
                return true;
            }

            return false;
        }
        #endif

        /// <summary>
        /// Swaps the item with exactly one item from the desired destination after accounting for the item size.
        /// If there is no item or there is more than one, no swap will occur.
        /// </summary>
        /// <param name="item">The item to swap in.</param>
        /// <param name="dest">The intitial destination cell. neighboring cells may be check for items to swap during th operation depending on the incoming item's dimensions.</param>
        /// <param name="verify"></param>
        /// <returns>The item that was swapped out of the destination or null if a swap failed.</returns>
        public static PGISlotItem SwapItem(PGISlotItem item, CellModel dest, bool verify = true)
        {
            if (item == null || dest == null) return null;
            var destModel = dest.Model;
            var destItem = dest.Item;


            var conflicts = destModel.SpaceConflicts(item, dest);
            if (conflicts == null || conflicts.Count != 1)
                return null;

            destItem = conflicts[0];
            if (!PGIModel.RemoveItem(destItem, true))
                return null;

            if(!PGIModel.StoreItem(item, dest, true))
            {
                PGIModel.StoreItem(destItem, dest, false);
                return null;
            }

            return destItem;
        }
                
        /// <summary>
        /// Attempts to store the item within the given cell model.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="dest"></param>
        /// <param name="verify">Should we verify that this operation is allowed first? Invokes relevant 'can' methods when true.</param>
        /// <returns>The item that was stored, or null if it wasn't.</returns>
        public static bool StoreItem(PGISlotItem item, CellModel dest, bool verify = true)
        {
            PGISlotItem destItem;
            return StoreItem(item, dest, out destItem, verify);
        }

        /// <summary>
        /// Helper for posting events when an item first enters or exits any models.
        /// </summary>
        /// <param name="item">The item that is entering and exiting any relevant models.</param>
        /// <param name="newModel">The model the item is entering if any. Can be null.</param>
        /// <param name="oldModel">The model the item is leaving if any. Can be null.</param>
        public static void PostMovementEvents(PGISlotItem item, PGIModel newModel, PGIModel oldModel)
        {
            Assert.IsNotNull(item);
            if(newModel != null && oldModel != newModel)
            {
                item.OnEnterModel.Invoke(item, newModel);
                newModel.OnItemEnteredModel.Invoke(item, newModel);
            }

            if (oldModel != null && oldModel != newModel)
            {
                item.OnExitModel.Invoke(item, oldModel);
                oldModel.OnItemExitedModel.Invoke(item, oldModel);
            }
        }

        /// <summary>
        /// Attempts to store the item within the given cell model. This function will fail if the item is currently stored elsewhere.
        /// </summary>
        /// <param name="item">The item to store.</param>
        /// <param name="dest">The destination cell to store the item.</param>
        /// <param name="destItem">The item, if any, that was stored in the destination location.</param>
        /// <param name="verify">Should we verify that this operation is allowed first? Invokes relevant 'can' methods when true.</param>
        /// <returns>The item that was stored, or null if it wasn't.</returns>
        public static bool StoreItem(PGISlotItem item, CellModel dest, out PGISlotItem destItem, bool verify = true)
        {
            Assert.IsNotNull(item);
            Assert.IsNotNull(dest);
            var model = dest.Model;
            Assert.IsNotNull(model);

            var previousModel = item.Model;
            var previousCell = item.Cell;
            destItem = dest.Item;

            //is the item that is to be stored currently stored elsewhere? If so we
            //need to remove it from that location first if we can.
            if (previousCell != null) return false;


            #if !PGI_LITE
            //check socketing before stacking or storing
            if (verify && model.CanSocket(item, destItem))
            {
                //THINGS TO CONSIDER:
                // 1) We need to consider socketing stacked items. We should only socket one at a time
                //  so we'll need to split the stack.
                // 2) When we socket an item we need to remove it from the inventory and make it belong
                //  to the item it is being attached to.
                if (model.Socket(destItem.GetComponent<Socketed>(), item.GetComponent<Socketable>()))
                    return true;
            }
            #endif


            if (!dest.IsEquipmentCell)
            {
                int x = dest.xPos;
                int y = dest.yPos;
                

                //check for stacking next
                if (verify && model.InternalCanStackAt(item, x, y))
                {
                    try
                    {
                        bool success;
                        //techincally, this might succeed but we still need to return remainders
                        //so we'll return false to let the handler do what it needs to with the original item/stack.
                        if (model.StackStore(item, model.GetCellContents(x, y), out success) != null)
                        {
                            if (success)
                            {
                                PostMovementEvents(item, model, previousModel);
                                return false;
                            }
                        }
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError(e.Message);
                        model.TriggerEvents(EventClass.Failed, item, dest);
                        return false;
                    }

                    PostMovementEvents(item, model, previousModel);
                    return true;
                }
                //if not stacking or socketing, check slot for availablility
                else if (verify && !model.CanStore(item, dest))
                {
                    model.TriggerEvents(EventClass.Failed, item, dest);
                    return false;
                }

                //we need to assign the item to
                //the full range of slots being used by it
                for (int t = y; t < y + item.CellHeight; t++)
                {
                    for (int s = x; s < x + item.CellWidth; s++)
                    {
                        model.Cells[s, t].Item = item;
                    }
                }


                item.ProcessStorage(model.Cells[x, y]);
                PostMovementEvents(item, model, previousModel);
                model.TriggerEvents(EventClass.Store, item, dest);
                return true;
            }//end grid-storage
            else //begin equipment storage
            {
                if (verify)
                {
                    if (!model.CanStore(item, dest))
                    {
                        model.TriggerEvents(EventClass.Failed, item, dest);
                        return false;
                    }
                }

                if (dest != null)
                {
                    dest.Item = item;
                    item.ProcessStorage(dest);
                    PostMovementEvents(item, model, previousModel);
                    model.TriggerEvents(EventClass.Store, item, dest);
                    return true;
                }

                model.TriggerEvents(EventClass.Failed, item, dest);
                return false;
            }//end requipment storage
            
        }
        
        /// <summary>
        /// Stores the item in the inventory at the first available space if possible.
        /// <para>
        ///  If this model's <see cref="PGIModel.AutoEquip"/> flag is set then equipment
        ///  slots will be considered when searching for space. Equipment slots will be
        ///  considered only after the grid has been determined to have insufficent space
        ///  unless the <see cref=" PGIModel.AutoEquipFirst"/> flags is also set.
        /// </para>
        /// <para>
        /// If this model's <see cref="PGIModel.AutoStack"/> flag is set and the item is
        /// stackable, the inventory will be searched for like items to stack it with first.
        /// </para>
        /// </summary>
        /// <returns><c>true</c>, if the item was stored, <c>false</c> otherwise.</returns>
        /// <param name="item">The item to store.</param>
        public bool Pickup(PGISlotItem item)
        {
            if (item == null || item.Model != null) return false;

            InitGrid();
            InitEquipment();
            
            //Check for stacking first, if anything is left over 
            //we'll drop down to the 'single-item' section.
            if (item.MaxStack > 1 && AutoStack)
            {
                foreach(var cell in Cells)
                {
                    var thing = cell.Item;
                    if (thing != null && thing.MaxStack > 1 &&
                        thing.ItemTypeId.Hash == item.ItemTypeId.Hash &&
                        thing.StackCount < thing.MaxStack)
                    {
                        bool success;
                        if (StackStore(item, thing, out success) == null)
                        {
                            //we got nothing back, we can safely return success
                            return true;
                        }
                        //at this point we must have leftovers of some kind. We'll
                        //drop down to try storing the rest as normal.
                    }
                }
            }

            //Store as a single item. If anything was left over
            //from the stack-section above it will get handled here.
            //check equipment slots first
            if (AutoEquip && AutoEquipFirst && ECells != null)
            {
                for (int i = 0; i < ECells.Count; i++)
                {
                    var tempSlot = ECells[i];
                    if (tempSlot != null)
                    {
                        //NOTE: have to check for null item here otherwise we might end up accidentally
                        //socketing something in an equipment slot.
                        if ( tempSlot.Item == null && !tempSlot.SkipAutoEquip && PGIModel.StoreItem(item, tempSlot))
                            return true;
                    }
                }
                
            }

            var pair = FindFirstFreeSpace(item);
            if (pair != null)
            {
                return PGIModel.StoreItem(item, GetCellModel(pair.X, pair.Y));
            }

            if (AutoEquip && !AutoEquipFirst && ECells != null)
            {
                for (int i = 0; i < ECells.Count; i++)
                {
                    var tempCell = ECells[i];
                    if (tempCell != null && !tempCell.SkipAutoEquip && PGIModel.StoreItem(item, tempCell))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the given item from this model. This method also triggers the 'item exit' event for the model and the item.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        public bool Drop(PGISlotItem item)
        {
            if (item == null) return true;
            if (item.Model == null || item.Model != this) return false;
            if (!item.IsEquipped && !item.IsStored) return false;

            if (item.IsEquipped)
            {
                if (PGIModel.RemoveItem(item))
                {
                    PostMovementEvents(item, null, item.Model);
                    return true;
                }
            }
            else
            {
                if (InternalRemove(item) != null)
                {
                    PostMovementEvents(item, null, item.Model);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Removes this item from its associated inventory model.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="verify"></param>
        /// <returns></returns>
        public static bool RemoveItem(PGISlotItem item, bool verify = true)
        {
            Assert.IsNotNull(item);
            PGIModel model = item.Model;
            if (model == null) return false; //not even in an inventory!
            var previousCell = item.Cell;
            
            if (verify && !model.CanDrop(item))
                return false;

            if (item.IsEquipped)
            {
                if (verify && !model.CanDrop(item))
                    return false;
                model.ECells[item.Equipped].Item = null;
                item.ProcessRemoval();
                model.TriggerEvents(EventClass.Remove, item, previousCell);
                return item;
            }
            else
            {
                //Unassign all slots that were used by the item.
                for (int t = item.yInvPos; t < item.yInvPos + item.CellHeight; t++)
                {
                    for (int s = item.xInvPos; s < item.xInvPos + item.CellWidth; s++)
                    {
                        model.Cells[s, t].Item = null;
                    }
                }
                item.ProcessRemoval();
                
                model.TriggerEvents(EventClass.Remove, item, previousCell);
                return true;
            }
        }

        /// <summary>
        /// Drops the item from this inventory model that is located at the given grid position.
        /// </summary>
        /// <returns>The item that was dropped if successful, otherwise null.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="checkCanMethods">Optional parameter. If <c>true</c>, all attached 'Can...' methods
        /// associated with the item and this model will be called to verify that the item can be properly located
        /// at the given location. It is recommened that this be used to avoid state-corruption.</param>
        PGISlotItem Remove(int x, int y, bool checkCanMethods = true)
        {
            return this.InternalRemove(GetCellContents(x, y), checkCanMethods);
        }

        /// <summary>
        /// Drops the specified item from this inventory model and returns it if successful.
        /// </summary>
        /// <returns>The item that was dropped if successful, otherwise null.</returns>
        /// <param name="item">The item to drop.</param>
        /// <param name="checkCanMethods">Optional parameter. If <c>true</c>, all attached 'Can...' methods
        /// associated with the item and this model will be called to verify that the item can be properly located
        /// at the given location. It is recommened that this be used to avoid state-corruption.</param>
        PGISlotItem InternalRemove(PGISlotItem item, bool checkCanMethods = true)
        {
            if (PGIModel.RemoveItem(item, checkCanMethods))
                return item;
            else return null;
        }
        
        /// <summary>
        /// Locates the first free location in the grid that provides enough space to store the given item 
        /// s a <see cref="PGIModel.Pos"/>. If no space exists then null is returned.
        /// </summary>
        /// <remarks>
        /// This method also invokes the 'Can...' methods of the item and model when checking for availablility.
        /// </remarks>
        /// <returns>A <see cref="PGIModel.Pos"/> with the coordinates of first free grid space or null.</returns>
        /// <param name="item">The item whose</param>
        public Pos FindFirstFreeSpace(PGISlotItem item)
        {
            for (int y = 0; y < this.GridCellsY; y++)
            {
                for (int x = 0; x < this.GridCellsX; x++)
                {
                    if (CanStore(item, GetCellModel(x, y))) return new Pos(x, y);
                }
            }
            return null;
        }

        /// <summary>
        /// Performs multiple calls to <see cref="QueryAndRemove"/> and returns a list of all
        /// found items.
        /// <seealso cref="QueryList(Query)"/>
        /// </summary>
        /// <param name="usePooling">If true, items that are instantiated from stacks will use the built-in auto-pool system.</param>
        /// <param name="queries">A list of query structures that contains information on what type of item to look for and how it should be found.</param>
        public List<List<PGISlotItem>> QueryAndRemoveAll(bool usePooling, params Query[] queries)
        {
            var list = new List<List<PGISlotItem>>(5);

            for(int i = 0; i < queries.Length; i++)
            {
                var subList = QueryAndRemove(usePooling, queries[i]);
                if (subList != null && subList.Count > 0) list.Add(subList);
            }

            return list;
        }

        /// <summary>
        /// Queries the model for the specified item type and amount.
        /// The desired number of items will be gathered from the model
        /// using the model specified. If the model does not contain enough of the
        /// desired item the list may have few items than was speified by the query.
        /// 
        /// All items found will be removed from this model.
        /// 
        /// This method will never return a stack but in fact, will split all stacks into individual items.
        /// Setting <see cref="usePooling"/> to true can, in some cases, minimize allocations.
        /// </summary>
        /// <param name="usePooling">If true, items that are instantiated from stacks will use the built-in auto-pool system.</param>
        /// <param name="query">A query structure that contains information on what type of item to look for and how it should be found.</param>
        /// <returns></returns>
        public List<PGISlotItem> QueryAndRemove(bool usePooling, Query query)
        {
            //create a new list of all items in inventory and sort by stack size
            List<PGISlotItem> items = null;
            if (query.Source == GatherSource.GridAndEquipment) items = AllItems;
            else if (query.Source == GatherSource.GridOnly) items = GridItems;
            else items = EquipmentItems;
            if (query.Mode == GatherMode.CompleteStacksFirst) items.OrderByDescending(x => x.StackCount);
            else items.OrderBy(x => x.StackCount);
            var stacks = items.Where(x => x.ItemTypeId.Hash == query.ItemTypeId);

            //gather the required number of items or the max available, whichever is smaller
            int countLeft = query.Count;
            //var itemsToAdd = new List<PGISlotItem>(25);
            var finalList = new List<PGISlotItem>(25);
            foreach(var item in stacks)
            {
                //we reuse the 'items' list in a different context here. This is to reduce allocations a little
                items.Clear();
                if (item.StackCount <= 1) items.Add(item.Model.InternalRemove(item, false));
                else items.AddRange(PGIModel.DecomposeStack(item, Mathf.Min(item.StackCount, countLeft), usePooling));

                if(items.Count > 0)
                {
                    finalList.AddRange(items);
                    countLeft -= items.Count;
                }

                if (countLeft == 0) break;
            }

            //remove all queried items from this model
            for (int i = 0; i < finalList.Count; i++)
            {
                if (finalList[i].Model != null) InternalRemove(finalList[i], false);
            }

            //cleanup the remaining stacks in the model and return our resulting list
            //RestackItems();
            return finalList;
        }
        
        /// <summary>
        /// Internal helper that restacks items without re-allocating a new list.
        /// </summary>
        /// <param name="items"></param>
        protected void RestackItems(List<PGISlotItem> items)
        {
            //we're going to do this the inefficient and lazy way.
            //We'll switch on auto stacking and then remove/restore each stack
            //in the model to ensure we have full stacks and only one partial if
            //there are any remainders.

            bool auto = AutoStack;
            AutoStack = true;
            PGISlotItem stack;
            Pos p = new Pos(0, 0);

            for (int i = 0; i < items.Count; i++)
            {
                stack = items[i];
                if(stack.StackCount < stack.MaxStack)
                {
                    p.Change(stack.xInvPos, stack.yInvPos);
                    InternalRemove(stack, false);
                    PGIModel.StoreItem(stack, GetCellModel(p.X, p.Y), false);
                }
            }

            AutoStack = auto;
        }

        /// <summary>
        /// Goes through all stacked items and makes sure that all stacks are completed before creating a partial.
        /// </summary>
        public void RestackItems()
        {
            RestackItems(AllItems);
        }

        /*
        static List<ImmutablePos> Hilights = new List<ImmutablePos>(10);
        /// <summary>
        /// Given a position, this calculates the highlights that should occur.
        /// This does not account for overriding cells that are bigger than 1x1
        /// and overlap any of the defined area.
        /// </summary>
        public List<ImmutablePos> CalculateHighlights(Pos point, PGISlotItem item)
        {
            Hilights.Clear();
            if (item == null) return Hilights;

            for (int y = point.Y; y < point.Y + item.CellHeight; y++)
            {
                for (int x = point.X; x < point.X + item.CellWidth; x++)
                {
                    var p = new ImmutablePos(x, y);
                    Hilights.Add(p);
                }
            }
            return Hilights;
        }
        */

        /// <summary>
        /// Saves this model by serializing its entire GameObject hierarchy and storing it in an XmlDocument.
        /// </summary>
        /// <remarks>
        /// In order for the model's inventory to be properly saved with it, all items must be children of this model at some level.
        /// </remarks>
        /// <param name="model">The model whose GameObject hierarchy will be serialized.</param>
        /// <param name="version">A version number that can be used to track updates to the serialization system or the model. When deserializing, the version number must be lower or equal to this value.</param>
        /// <returns>An XmlDocument containing a serialized format of the GameObject hierarchy that this model was attached to.</returns>
        public XmlDocument SaveModel(int version)
        {
            return XmlSerializer.Serialize(this.gameObject, version);
        }

        /// <summary>
        /// Loads a previously saved PGIModel hierarchy from the supplied xml.
        /// </summary>
        /// <param name="xmlText">The xml text to deserialize and load the model from.</param>
        /// <param name="maxSupportedVersion">The version number the model was serialized with. If this number is lower than the one in the xml stream, it will fail.</param>
        /// <param name="modelToReplace">A reference to the model that should be replaced with the newly deserialized one. The GameObject this component is attached to will be destroyed and the reference will be redirected to the newly deserialized model. This reference must not be null.</param>
        public static void LoadModel(string xmlText, int maxSupportedVersion, ref PGIModel modelToReplace)
        {
            GameObject go = XmlDeserializer.Deserialize(xmlText, maxSupportedVersion) as GameObject;
            if (go != null)
            {
                var newModel = go.GetComponent<PGIModel>();
                if (newModel != null && modelToReplace != null)
                {
                    var parent = modelToReplace.gameObject.transform.parent;
                    GameObject.Destroy(modelToReplace.gameObject);
                    modelToReplace = newModel;
                    newModel.transform.SetParent(parent, false);
                }
            }
        }
        #endregion


        #region Static Methods
        /// <summary>
        /// Splits a stack into two stacks. The newly created stack will be of the desired size while the
        /// source stack will be decreased by the appropriate amount. The newly created stack will also be
        /// removed from any source model whereas the source will remain where it is.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="newStackCount"></param>
        /// <param name="usePooling"></param>
        /// <returns></returns>
        public static PGISlotItem SplitStack(PGISlotItem stack, int newStackCount, bool usePooling = false)
        {
            Assert.IsTrue(stack.MaxStack > 1, "Cannot split an item that can't be stacked.");
            Assert.IsTrue(newStackCount > 0, "New stack size is invalid. It must be greater than zero.");
            Assert.IsTrue(newStackCount < stack.StackCount, "Desired count is invalid. It must be less than current stack size.");
            Assert.IsNotNull(stack);

            GameObject obj = null;
            if (usePooling) obj = Lazarus.Summon(stack.gameObject);
            else obj = Instantiate(stack.gameObject);

            var newStack = obj.GetComponent<PGISlotItem>();
            newStack.StackCount = newStackCount;
            stack.StackCount -= newStackCount;

            if (newStack.Model != null) newStack.Model.InternalRemove(newStack, false);
            return newStack;
        }

        /// <summary>
        /// Splits a stack by cloning a number of live items. Each item will be a single object with a stack size of one.
        /// The original stack, if one is left, will be reduced by the number of items cloned in this way. All new items
        /// will be removed from the model they originated from. If the stack become empty it will be destroyed.
        /// 
        /// Note: <see cref="usePooling"/> can be used to potentially mitigate the number
        /// of allocations this method must perform.
        /// 
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="count"></param>
        /// <param name="usePooling"></param>
        /// <returns>A list of the items removed from the source stack.</returns>
        public static List<PGISlotItem> DecomposeStack(PGISlotItem stack, int count, bool usePooling = false)
        {
            Assert.IsTrue(stack.MaxStack > 1, "Cannot decompose an item that can't be stacked.");
            Assert.IsTrue(count > 0, "Item count is invalid. It must be greater than zero.");
            Assert.IsTrue(count <= stack.StackCount, "Desired count is invalid. It must be less-than or equal-to current stack size.");
            Assert.IsNotNull(stack);

            var list = new List<PGISlotItem>(25);

            PGISlotItem newItem = null;
            GameObject obj = null;
            for (int i = 0; i < count; i++)
            {
                if (usePooling) obj = Lazarus.Summon(stack.gameObject);
                else obj = Instantiate(stack.gameObject);
                newItem = obj.GetComponent<PGISlotItem>();
                newItem.StackCount = 1;
                stack.StackCount--;
                if (newItem.Model != null) newItem.Model.InternalRemove(newItem, false);
            }

            if (stack.StackCount < 1)
            {
                if (usePooling) Lazarus.Summon(stack.gameObject);
                #if UNITY_EDITOR
                else
                {
                    if (Application.isPlaying) Destroy(stack.gameObject);
                    else DestroyImmediate(stack.gameObject);
                }
                #else
                else Destroy(stack.gameObject);
                #endif
            }

            return list;
        }
        #endregion
    }
}