/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using PowerGridInventory.Utility;
using System.Xml.Serialization;
using Toolbox.Graphics;
using Toolbox.Common;
using System.Collections;

namespace PowerGridInventory
{
    /// <summary>
    /// Represents a single slot within the inventory grid's UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Power Grid Inventory/PGI Slot", 13)]
    [Serializable]
    public class PGISlot : Selectable,
                    IBeginDragHandler,
                    IDragHandler,
                    IEndDragHandler,
                    IDropHandler,
                    IPointerEnterHandler,
                    IPointerExitHandler,
                    IPointerClickHandler,
                    IPointerUpHandler,
                    IPointerDownHandler,
                    ISubmitHandler,
                    ICancelHandler,
                    IEventSystemHandler
    {
        #region Internal Classes
        [Serializable]
        public class DragTrigger : UnityEvent<PointerEventData> { }
        [Serializable]
        public class SlotTrigger : UnityEvent<PointerEventData, PGISlot> { }
        [Serializable]
        public class SlotGamePadTrigger : UnityEvent<BaseEventData, PGISlot> { }
        [Serializable]
        public class SlotItemTrigger : UnityEvent<PointerEventData, PGISlot, PGISlotItem> { }
        #endregion




        #region Members and Properties
        /// <summary>
        /// Used to track which slot was the last one selected.
        /// Needed sometime when disabling/enabling all renderers in a View since race conditions can
        /// cause the Selectables to not update their color correctly.
        /// </summary>
        static PGISlot LastSlot;

        [Tooltip("Used to enable/disable interaction with this slot.")]
        public bool Interactable = true;

        /// <summary>
        /// Exposes a hidden feature of Selectables.
        /// </summary>
        public bool IsSelected
        {
            get { return base.currentSelectionState == SelectionState.Highlighted || base.currentSelectionState == SelectionState.Pressed; }
        }

        //due to the dumb way unity handles this, I need to provide my own backing fields for 
        //the Selectable.Navigation struct. Why the crap do they not make their own datatypes serializable! >:(
        [SerializeField]
        [HideInInspector]
        Selectable _OnUpSelect;
        public Selectable SelectOnUp
        {
            get { return _OnUpSelect; }
            set
            {
                _OnUpSelect = value;
                var n = navigation;
                n.selectOnUp = value;
                navigation = n;
            }
        }

        [SerializeField]
        [HideInInspector]
        Selectable _OnDownSelect;
        public Selectable SelectOnDown
        {
            get { return _OnDownSelect; }
            set
            {
                _OnDownSelect = value;
                var n = navigation;
                n.selectOnDown = value;
                navigation = n;
            }
        }

        [SerializeField]
        [HideInInspector]
        Selectable _OnLeftSelect;
        public Selectable SelectOnLeft
        {
            get { return _OnLeftSelect; }
            set
            {
                _OnLeftSelect = value;
                var n = navigation;
                n.selectOnLeft = value;
                navigation = n;
            }
        }

        [SerializeField]
        [HideInInspector]
        Selectable _OnRightSelect;
        public Selectable SelectOnRight
        {
            get { return _OnRightSelect; }
            set
            {
                _OnRightSelect = value;
                var n = navigation;
                n.selectOnRight = value;
                navigation = n;
            }
        }

        [SerializeField]
        [HideInInspector]
        Navigation.Mode _NavMode = Navigation.Mode.Automatic;
        public Navigation.Mode NavMode
        {
            get { return _NavMode; }
            set
            {
                _NavMode = value;
                var n = navigation;
                n.mode = value;
                navigation = n;
            }
        }


        /// <summary>
        /// Excludes this slot from being considered when a <see cref="PGIModel"/>
        /// is searching for empty equipment slots to auto-equip an item to.
        /// </summary>
        [Header("Inventory Behaviour")]
        [Tooltip("Excludes this slot from being considered when a PGIModel is searching for empty equipment slots to auto-equip an item to.")]
        public bool SkipAutoEquip = false;

        /// <summary>
        /// The default icon to use when no item is displayed in this slot.
        /// </summary>
        [XmlIgnore]
        public Sprite DefaultIcon
        {
            get { return _DefaultIcon; }
            set
            {
                _DefaultIcon = value;
                if (Item == null) SetDefaultIcon();
            }
        }
        [SerializeField]
        [Tooltip("The default icon to use when no item is displayed in this slot.")]
        private Sprite _DefaultIcon;

        /// <summary>
        /// The color of the icon when no item is equipped in it's slot.
        /// </summary>
        [Tooltip("The color of the icon when no item is equipped in it's slot.")]
        public Color DefaultIconColor = Color.white;

        /// <summary>
        /// A reference to the 'Highlighted' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.
        /// </summary>
        [Header("Sub-GameObject References")]
        [Tooltip("A reference to the 'Highlighted' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.")]
        public Image Highlight;
        
        /// <summary>
        /// A reference to the 'Icon' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.
        /// </summary>
        [Tooltip("A reference to the 'Icon' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.")]
        public Image IconImage;

        /// <summary>
        /// A reference to the '3D Icon' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.
        /// </summary>
        [Tooltip("A reference to the '3D Icon' portion of a SlotPrefab GameObject. See the Manual about 'Slot Prefab Spec' for more details.")]
        public Image3D IconImage3D;

        /// <summary>
        /// A reference to the 'StackSize' portion of a SlotPrefab GameOBject. See the Manual about 'Slot Prefab Spec' for more details.
        /// </summary>
        [Tooltip("A reference to the 'StackSize' portion of a SlotPrefab GameOBject. See the Manual about 'Slot Prefab Spec' for more details.")]
        public Text StackSize;

        /// <summary>
        /// The equipment index this slot is assigned to if it is being used as an
        /// equipment slot by a <see cref="PGIModel"/> or -1 if it is not.
        /// <seealso cref="PGIModel.EquipmentSlots"/>
        /// </summary>
        public int EquipmentIndex { get { return CorrespondingCell == null ? -1 : CorrespondingCell.EquipIndex; } }

        /// <summary>
        /// The x-axis grid location this slot has been assigned to by
        /// a <see cref="PGIView"/> or -1 if it is not a grid slot.
        /// <seealso cref="PGIView.GetSlotCell"/>
        /// </summary>
        public int xPos { get { return CorrespondingCell == null ? -1 : CorrespondingCell.xPos; } }

        /// <summary>
        /// The y-axis location this slot has been assigned to by
        /// a <see cref="PGIView"/> or -1 if it is not a grid slot.
        /// <seealso cref="PGIView.GetSlotCell"/>
        /// </summary>
        public int yPos { get { return CorrespondingCell == null ? -1 : CorrespondingCell.yPos; } }

        /// <summary>
        /// The number of cells wide this slot is in a grid.
        /// This value will often be changed automatically to match
        /// the item contents of this slot.
        /// </summary>
        public int GridWidth
        {
            get
            {
                //man, I wish I had C# 6.0 :(  This would be so much easier
                var item = CorrespondingCell == null ? null : CorrespondingCell.Item;
                return item == null ? 1 : item.CellWidth;
            }
        }

        /// <summary>
        /// The number of cells high this slot is in a grid.
        /// This value will often be changed automatically to match
        /// the item contents of this slot.
        /// </summary>
        public int GridHeight
        {
            get
            {
                //man, I wish I had C# 6.0 :(  This would be so much easier
                var item = CorrespondingCell == null ? null : CorrespondingCell.Item;
                return item == null ? 1 : item.CellHeight;
            }
        }

        /// <summary>
        /// The slot that overrlays this one if that cell is bigger than 1x1.
        /// <seealso cref="PGIView.GetSlotCell"/>
        /// </summary>
        [HideInInspector]
        public PGISlot OverridingSlot; //slot that is covering this one
        
        /// <summary>
        /// The <see cref="PGIModel"/> this slot is being used by.
        /// </summary>
        [HideInInspector]
        public PGIModel Model
        {
            get { return View == null ? null : View.Model; }
        }

        /// <summary>
        /// The <see cref="PGIView"/> this slot is being used by
        /// if it is part of a grid.
        /// </summary>
        [HideInInspector]
        public PGIView View;
        [HideInInspector]
        [SerializeField]
        bool _Blocked = false;

        /// <summary>
        /// Flags this slot as being blocked and disallows any item from being equipped to it.
        /// </summary>
        [HideInInspector]
        public bool Blocked
        {
            get { return _Blocked; }
            set { _Blocked = value; }
        }
        
        /// <summary>
        /// A reference to the <see cref="PGISlotItem"/> component of the item GameObject
        /// that is being stored in this slot. Or null if there is no item.
        /// </summary>
        [HideInInspector]
        public virtual PGISlotItem Item { get { return CorrespondingCell == null ? null : CorrespondingCell.Item; } }
        
        /// <summary>
        /// This is a reference to the cell in the model that this slot represents. 
        /// You should not change this unless you know exactly what you are doing.
        /// </summary>
        public virtual CellModel CorrespondingCell { get; set; }
        
        //None of this stuff needs to be serialized. It's either temporary storage
        //or it's set up during the awake sequence.
        [HideInInspector]
        public RectTransform LocalRect { get; protected set; }
        private Canvas _Canvas;
        protected Canvas Canvas
        {
            get
            {
                if (_Canvas == null)
                {
                    _Canvas = gameObject.GetComponentInParent<Canvas>();
                    if (_Canvas == null)
                        Debug.LogError("This GridSlot must have a parent somewhere in its heirarchy that has a Canvas attached to it.");
                }
                return _Canvas;
            }
        }
        protected Image BackgroundImage;
        protected Vector2 CachePosition;

        //Data shared by all slots.
        protected static bool InvalidateClick = false;
        protected static bool TouchHover = false;
        protected static float HoverStartTime = 0.0f;
        protected static readonly float MinHoverTime = 0.75f;
        

        /// <summary>
        /// Returns true if this slot is considered a <see cref="PGIModel"/>'s equipment slot.
        /// </summary>
        [HideInInspector]
        [XmlIgnore]
        public bool IsEquipmentSlot
        {
            get { return (EquipmentIndex >= 0); }
        }

        /// <summary>
        /// Convenience accessor that references this slot's IconImage sprite.
        /// See the Manual under 'Slot Prefab Spec' for more details.
        /// </summary>
        [HideInInspector]
        [XmlIgnore]
        public virtual Sprite IconSprite
        {
            protected set
            {
                if (IconImage == null) return;
                if(IconImage.sprite != value)//adding this check will significantly imporve redraw performance!
                    IconImage.sprite = value;
                if (value == null) IconImage.enabled = false;
                else IconImage.enabled = true;
            }
            get 
            {
                return IconImage == null ? null : IconImage.sprite;  
            }
        }

        /// <summary>
        /// Convenience accessor that references this slot's CanvasMesh mesh.
        /// See the Manual under 'Slot Prefab Spec' for more details.
        /// </summary>
        [HideInInspector]
        [XmlIgnore]
        public virtual Mesh IconMesh 
        {
            protected set
            {
                if (IconImage3D == null) return;
                if(IconImage3D.Mesh != value) //same as Icon setter above... but actually unecessary since it does the same internally. Just here for consitency.
                    IconImage3D.Mesh = value;
                if (value == null) IconImage3D.enabled = false;
                else IconImage3D.enabled = true;
            }
            get { return IconImage3D == null ? null : IconImage3D.Mesh; }
        }

        /// <summary>
        /// Convenience accessor that references this slot's CanvasMesh material.
        /// See the Manual under 'Slot Prefab Spec' for more details.
        /// </summary>
        [HideInInspector]
        public Material Icon3DMat
        {
            protected set
            {
                if (IconImage3D != null)
                    IconImage3D.material = value;
            }
            get { return IconImage3D == null ? null : IconImage3D.material; } 
        }

        protected int LastStackCount = 1;

        /// <summary>
        /// Convenience accessor that references the current stack count displayed in
        /// the Text UI element of this slot.
        /// </summary>
        [XmlIgnore]
        public int StackCount
        {
            protected set
            {
                if (StackSize == null) return;

                if (LastStackCount != value)
                {
                    //the stack count has changed for our item, update the text
                    LastStackCount = value;
                    if (value < 2) StackSize.enabled = false;
                    else
                    {
                        StackSize.text = value.ToString();
                        StackSize.enabled = true;
                    }
                }
                else if (value < 2) StackSize.enabled = false;
            }

            get 
            {
                if (StackSize == null) return 0;
                if (string.IsNullOrEmpty(this.StackSize.text)) return 0;
                else return int.Parse(StackSize.text);
            }
        }

        /// <summary>
        /// Convenience accessor that reference this slot's 'Highlight' UI element's color.
        /// See the Manual under 'Slot Prefab Spec' for more details.
        /// </summary>
        [HideInInspector]
        [XmlIgnore]
        public Color HighlightColor
        {
            set
            {
                if (Highlight == null) return;

                //NOTE: We are checking for null because sometimes when switching a view's model
                //around, we have leftover views to deselect and this will get called even though
                //there are no slots active for that view.
                if (Highlight != null)
                {
                    Highlight.color = value;
                    //disable the highlight if the color is fully transparent
                    if (value.a <= 0.001f) Highlight.enabled = false;
                    else Highlight.enabled = true;
                }
            }
            get { return Highlight == null ? Color.white : Highlight.color; }
        }

        #endregion


        #region Triggered Events

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
        /// Mostly for internal use by a <see cref="PGIView"/>. This event allows
        /// the view to hook into the drag 'n drop actions of this object.
        /// </summary>
        [HideInInspector]
        [PGIFoldedEvent]
        public DragTrigger OnBeginDragEvent = new DragTrigger();

        /// <summary>
        /// Mostly for internal use by a <see cref="PGIView"/>. This event allows
        /// the view to hook into the drag 'n drop actions of this object.
        /// </summary>
        [HideInInspector]
        [PGIFoldedEvent]
        public DragTrigger OnEndDragEvent = new DragTrigger();

        /// <summary>
        /// Mostly for internal use by a <see cref="PGIView"/>. This event allows
        /// the view to hook into the drag 'n drop actions of this object.
        /// </summary>
        [HideInInspector]
        [PGIFoldedEvent]
        public DragTrigger OnDragEvent = new DragTrigger();

        /// <summary>
        /// Mostly for internal use by a <see cref="PGIView"/>. This event allows
        /// the view to hook into the drag 'n drop actions of this object.
        /// </summary>
        [HideInInspector]
        [PGIFoldedEvent]
        public DragTrigger OnDragDropEvent = new DragTrigger();

        /// <summary>
        /// Invoked when the pointer is clicked on this slot when an item is in it.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotTrigger OnClick = new SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is first pressed down on this slot.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotTrigger OnPointerPressed = new SlotTrigger();

        /// <summary>
        /// Invoked when the pointer is released on this slot.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotTrigger OnPointerReleased = new SlotTrigger();

        /// <summary>
        /// Invoked when a submit input operation is performed.
        /// This is mainly for GamePad support.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotGamePadTrigger OnSubmitInput = new SlotGamePadTrigger();

        /// <summary>
        /// Invoked when a cancel input operation is performed.
        /// This is mainly for GamePad support.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotGamePadTrigger OnCancelInput = new SlotGamePadTrigger();

        /// <summary>
        /// Invoked when this slot is selected.
        /// This is mainly for GamePad support.
        /// <seealso cref="PGISloItem.OnClick"/> 
        /// <seealso cref="PGIView.OnClickSlot"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotGamePadTrigger OnSelected = new SlotGamePadTrigger();

        /// <summary>
        /// Invoked when the pointer first enters this slot and an item is in it.
        /// <seealso cref="PGIView.OnHover"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotTrigger OnHover = new SlotTrigger();

        /// <summary>
        /// Invoked when the pointer leaves this slot and an item is in it.
        /// <seealso cref="PGIView.OnEndHover"/> 
        /// </summary>
        [SerializeField]
        [PGIFoldedEvent]
        public SlotTrigger OnEndHover = new SlotTrigger();
        [SerializeField]
        [FoldFlag("Events")]
        public bool FoldedEvents = false; //used by the inspector
        #endregion

        
        #region Unity Events
        protected override void Awake()
        {
            base.Awake();
            if (Application.isPlaying && IconImage == null)
                throw new UnityException("You must supply a gameobject with a UI.Image component attached to the 'IconImage' child object of each Grid Slot");
            
            BackgroundImage = GetComponent<Image>();
            LocalRect = this.GetComponent<RectTransform>();

            if (Application.isPlaying)
            {
                if (IconImage3D == null) throw new UnityException("You must supply a gameobject with a PowerGridInventory.Icon3D component attached to the 'IconMesh' child object of each Grid Slot");
                if (Highlight == null) throw new UnityException("You must supply a gameobject with a UI.Image component attached to the 'Highlight' child object of each Grid Slot");
                if (StackSize == null) throw new UnityException("You must supply a gameobject with a UI.Text component attached to the 'Stack' child object of each Grid Slot");
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_Blocked && this.View != null) this.HighlightColor = this.View.BlockedColor;
            else if (Item != null)
            {
                this.HighlightColor = Item.Highlight;
            }
            else if (this.View != null) this.HighlightColor = this.View.NormalColor;
            InitIcon();

            if (View != null)
                View.OnViewUpdated.AddListener(HandleViewUpdated);
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            if (View != null)
                View.OnViewUpdated.RemoveListener(HandleViewUpdated);
        }

        void HandleViewUpdated(PGIView view)
        {
            if(View != null && CorrespondingCell != null)
            {
                CorrespondingCell.Blocked = this.Blocked;
                CorrespondingCell.SkipAutoEquip = this.SkipAutoEquip;
            }
        }
        #endregion


        #region Methods
        /// <summary>
        /// Helper for setting this slot's GameObject active state. Checks for redundencies
        /// since calling SetActive() will call OnEnable/OnDisable regardless of state (these two
        /// call are expensive for slots since they now derive from Selectable).
        /// </summary>
        /// <param name="state"></param>
        public void Activate(bool state)
        {
            if (state != gameObject.activeSelf)
                gameObject.SetActive(state);
        }

        /// <summary>
        /// Helper used to initialize the icon when creating the slot, either
        /// through instatiation or through deserialization.
        /// </summary>
        void InitIcon()
        {
            if (IconImage != null && IconImage3D != null)
            {
                //make sure our icon state matched the default value upon startup
                if (DefaultIcon == null)
                {
                    IconSprite = null;
                    IconMesh = null;
                }
                else
                {
                    IconSprite = DefaultIcon;
                    IconMesh = null;
                }
            }
        }

        /// <summary>
        /// Updates this slot's internal representation of itself.
        /// Invoked by Views to force slots to update their icons.
        /// </summary>
        public virtual void UpdateView(bool updateHighlighting)
        {
            var item = this.Item;

            if(item == null)
            {
                SetDefaultIcon();
                StackCount = 0;
            }
            else
            {
                //make sure default color is removed - otherwise we might tint our icon incorrectly
                IconImage.color = Color.white;
                StackCount = item.StackCount;

                //set icon data
                if (item.IconType == PGISlotItem.IconAssetType.Sprite && IconImage != null)
                {
                    IconSprite = item.Icon;
                    IconImage3D.material = null;
                    IconImage3D.Rotation = item.IconOrientation;
                    IconMesh = null;

                    IconImage.enabled = true;
                    IconImage3D.enabled = false;

                    //the second instance of this component is for the rotation of the slot item
                    //regardless of the orientation in the model (i.e. arbitrary visual rotation)
                    UIRotate[] rot = IconImage.GetComponents<UIRotate>();
                    if (rot != null && rot.Length > 1) rot[1].EulerAngles = item.IconOrientation;

                }
                else if (item.IconType == PGISlotItem.IconAssetType.Mesh && IconImage3D != null)
                {
                    IconSprite = null;
                    IconImage3D.material = item.IconMaterial;
                    IconImage3D.Rotation = item.IconOrientation;
                    IconMesh = item.Icon3D;

                    IconImage.enabled = false;
                    IconImage3D.enabled = true;
                }
                else SetDefaultIcon(); //fallback in case we are somehow missing a sprite *and* a mesh for this item's icon.

                OrientIcon(item);
            }


            if (updateHighlighting)
            {
                if (this == LastSlot)
                {
                    this.Select();
                    this.OnSelect(null); //HACK: Workaround for broken slection UI
                }
                else this.DoStateTransition(SelectionState.Normal, true);
            }
        }

        /// <summary>
        /// Helper used to rotate images to match orientation but only if it is for grid.
        /// Equipment is ignored since it doesn't affect anything non-visual.
        /// </summary>
        void OrientIcon(PGISlotItem item)
        {
            if (!this.IsEquipmentSlot)
            {
                UIRotate rotateEffect = IconImage.GetComponent<UIRotate>();
                if (rotateEffect != null)
                {
                    switch (item.RotatedDir)
                    {
                        case PGISlotItem.RotateDirection.None:
                            {
                                rotateEffect.EulerAngles = Vector3.zero;
                                break;
                            }
                        case PGISlotItem.RotateDirection.CW:
                            {
                                rotateEffect.EulerAngles = new Vector3(0.0f, 0.0f, 270.0f);
                                break;
                            }
                        case PGISlotItem.RotateDirection.CCW:
                            {
                                rotateEffect.EulerAngles = new Vector3(0.0f, 0.0f, 90.0f);
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }//end switch
                }//end null check
            }//end if
        }

        /// <summary>
        /// Helper for setting default icon of the slot.
        /// </summary>
        void SetDefaultIcon()
        {
            if (IconImage3D != null)
            {
                IconImage3D.Rotation = Vector3.zero;
                IconImage3D.material = null;
                IconMesh = null;
            }
            StackCount = 0;
            if (IconImage != null)
            {
                IconSprite = DefaultIcon;
                IconImage.color = DefaultIconColor;
            }
        }

        /// <summary>
        /// Helper used to restore this slot's color from a
        /// drag-related highlighted state to whatever it should
        /// be otherwise. The color restored to usually depends
        /// on if it has an item stored in it currently.
        /// </summary>
        /// <param name="defaultViewColor">The default color for slots when nothing is stored and the slot has no special state.</param>
        public void AssignHighlighting(PGIView view)
        {
            if (view == null) return;
            if (Blocked) HighlightColor = view.BlockedColor;
            else if (Item != null) HighlightColor = Item.Highlight;
            else HighlightColor = view.NormalColor;
            if(view.BlockDisabledCells)
            {
                var pos = view.GetSlotLocation(this);
                if (view.Model.IsCellDisabled(pos.X, pos.Y))
                    HighlightColor = view.BlockedColor;
            }
        }

        /*
        public void RestoreHighlight(Color defaultViewColor)
        {
            if (Blocked) HighlightColor = this.View.BlockedColor;
            else if (Item != null) HighlightColor = Item.Highlight;
            else HighlightColor = defaultViewColor;
        }*/


        /// <summary>
        /// Gets the mouse location in local coordinates for this UI object.
        /// </summary>
        /// <returns>The local mouse coords.</returns>
        public Vector2 GetLocalMouseCoords()
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(LocalRect, Input.mousePosition, this.Canvas.worldCamera, out CachePosition))
            {
                return CachePosition;
            }

            return Vector2.zero;
        }
        #endregion


        #region Interfaces
        /// <summary>
        /// Handles the begin drag event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so thatit can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            if (!Interactable) return;

            if (Item != null)
            {
                OnBeginDragEvent.Invoke(eventData);
                InvalidateClick = true;
            }
        }

        /// <summary>
        /// Helper that is called by the view when a drop operation fails and we need to reset the drag state.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public virtual void OnResetDrag()
        {
            if (!Interactable) return;
            InvalidateClick = true;
        }

        /// <summary>
        /// Handles the drop event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so that it can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public virtual void OnDrop(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnDragDropEvent.Invoke(eventData);
            InvalidateClick = false;
        }

        /// <summary>
        /// Handles the end drag event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so that it can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public virtual void OnEndDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnEndDragEvent.Invoke(eventData);
            InvalidateClick = false;
        }

        /// <summary>
        /// Handles the drag event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so that it can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!Interactable) return;
            OnDragEvent.Invoke(eventData);
        }

        /// <summary>
        /// Handles the pointer click event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so that it can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            if (InvalidateClick) return;
            if(TouchHover && Time.unscaledTime - HoverStartTime > MinHoverTime) return;

            OnClick.Invoke(eventData, this);
            if (this.Item != null) Item.OnClick.Invoke(eventData, this);
        }
        #endregion


        #region Overriden Interfaces
        /// <summary>
        /// Handles the pointer enter event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so thatit can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (!Interactable) return;
            base.OnPointerEnter(eventData);
            OnHover.Invoke(eventData, this);

            //Because hovering on a touch-device necessitates touching the control we need a special case here.
            //If we are using a touch device and we were within the hovering phase before clicking, we can conclude
            //that this should not be considered a click.
            if (Input.touches.Length > 0)
            {
                HoverStartTime = Time.unscaledTime;
                TouchHover = true;
            }
        }

        /// <summary>
        /// Handles the pointer enter event.
        /// Usually, a <see cref="PGIView"/> will hook to this event
        /// so thatit can handle drag 'n drop events.
        /// </summary>
        /// <param name="eventData">Event data.</param>
        public override void OnPointerExit(PointerEventData eventData)
        {
            if (!Interactable) return;
            base.OnPointerExit(eventData);
            OnEndHover.Invoke(eventData, this);
            InvalidateClick = false;
            TouchHover = false;
        }

        /// <summary>
        /// Handles the pointer-up event.
        /// Usually, <see cref="PGIView"/> will hook to this event
        /// so that it can handle discrete, toggled drag 'n' drop events.
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!Interactable) return;
            base.OnPointerUp(eventData);
            if (InvalidateClick) return;
            
            OnPointerReleased.Invoke(eventData, this);
            if (this.Item != null) Item.OnPointerReleased.Invoke(eventData, this);
        }

        /// <summary>
        /// Handles the pointer-down event.
        /// Usually, <see cref="PGIView"/> will hook to this event
        /// so that it can handle discrete, toggled drag 'n' drop events.
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            base.OnPointerDown(eventData);
            if (InvalidateClick) return;

            OnPointerPressed.Invoke(eventData, this);
            if (this.Item != null) Item.OnPointerPressed.Invoke(eventData, this);
        }
        
        /// <summary>
        /// Handles the selection event from the StandardInputModule.
        /// This allows GamePads to highlight there current selection.
        /// This hihlighting is for the 'user cursor', not PGI's view
        /// hilighting that determines valid drop locations.
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnSelect(BaseEventData eventData)
        {
            if (!Interactable) return;
            base.OnSelect(eventData);
            LastSlot = this;
            OnSelected.Invoke(eventData, this);
        }

        /// <summary>
        /// Handles the de-selection event from the StandardInputModule.
        /// This allows GamePads to un-hilight there current selection.
        /// This highlighting is for the 'user cursor', not PGI's view
        /// hilighting that determines valid drop locations.
        /// </summary>
        /// <param name="eventData"></param>
        public override void OnDeselect(BaseEventData eventData)
        {
            if (!Interactable) return;
            base.OnDeselect(eventData);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        public void OnSubmit(BaseEventData eventData)
        {
            if (!Interactable) return;
            if (InvalidateClick) return;
            OnSubmitInput.Invoke(eventData, this);
            //if (this.Item != null) Item.OnSubmitInput.Invoke(eventData, this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventData"></param>
        public void OnCancel(BaseEventData eventData)
        {
            if (!Interactable) return;
            OnCancelInput.Invoke(eventData, this);
        }

        
        #endregion
    }
}