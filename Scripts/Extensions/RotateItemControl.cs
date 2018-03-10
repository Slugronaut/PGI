/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System;
using Toolbox.Common;

namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// Allows rotating the currently dragged item by either pressing a mouse button (requires DragToggle to be turned
    /// on in the view) or by scrolling the mouse wheel.
    /// </summary>
    [RequireComponent(typeof(PGIView))]
    public class RotateItemControl : MonoBehaviour
    {
        public PointerEventData.InputButton Button = PointerEventData.InputButton.Right;
        public PGISlotItem.RotateDirection RotateDirection = PGISlotItem.RotateDirection.CCW;
        PGIView View;

        void Start()
        {
            View = GetComponent<PGIView>();
        }

        void Update()
        {
            if (!View.IsDragging) return;

            //if (PointerUtility.GetPointerDown((int)Button, true))
            if(Input.GetMouseButtonDown((int)Button))
            {
                var item = PGIView.DraggedItem.Item;
                PGISlotItem.RotateDirection rot;
                if (item.Rotated) rot = PGISlotItem.RotateDirection.None;
                else rot = RotateDirection;

                item.Rotate(rot);
                View.RotateDragIcon(rot);
            }
        }
    }
}
