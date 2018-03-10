/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;

namespace PowerGridInventory.Utility
{
    /// <summary>
    /// Attached to child objects of Slot GameObjects so
    /// that they can be re-arranged at runtime to aid
    /// uGUI's batch rendereing.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Utility/Slot Batch", 17)]
    public class SlotBatch : MonoBehaviour
    {
        
        [HideInInspector]
        public RectTransform Rect;
        [HideInInspector]
        public Vector2 OriginalSizeDelta;
        [HideInInspector]
        public Vector3 OriginalPos;
        [HideInInspector]
        public Vector2 OffsetMin;
        [HideInInspector]
        public Vector2 OffsetMax;
        [HideInInspector]
        public Vector2 AnchorMin;
        [HideInInspector]
        public Vector2 AnchorMax;
        [HideInInspector]
        public Vector3 OriginalScale;

        void Awake()
        {
            Rect = this.GetComponent<RectTransform>();
            OriginalSizeDelta = Rect.sizeDelta;
            OriginalPos = Rect.position;
            OffsetMin = Rect.offsetMin;
            OffsetMax = Rect.offsetMax;
            AnchorMin = Rect.anchorMin;
            AnchorMax = Rect.anchorMax;
            OriginalScale = Rect.localScale;
        }

    }
}
