/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PowerGridInventory.Extensions.Tooltip
{

    /// <summary>
    /// Simple example of a component that activates an object when its
    /// hover event is triggered and deactivates it when the hover ends.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Tooltip/Tooltip Display")]
    public class TooltipDisplay : MonoBehaviour
    {
        /// <summary>
        /// Reference to the GameObject that a UI element belongs to for displaying data.
        /// This reference is for activating and deactivating the element based on hover-events.
        /// </summary>
        public GameObject HoverDisplay;

        /// <summary>
        /// Reference to the UI.Text element that will display the name of a hovered item.
        /// The name displayed will be the item's GameObject.name.
        /// </summary>
        public Text Title;

        /// <summary>
        /// Reference to the UI.Text element that will display the description of a hovered item.
        /// The description displayed will be obtained from an <see cref="ItemDesc"/> component
        /// attached to the item.
        /// </summary>
        public Text ItemDescription;

        /// <summary>
        /// Sometimes the z-axis matters when arranging UI elements. This can be used to offset the z-axis of the
        /// tooltip display object so that it can appear in the correct order.
        /// </summary>
        public float ZOffset = 0.0f;

        /// <summary>
        /// Where, in relation to the mouse the display should be shown.
        /// </summary>
        public Vector2 OffsetFromMouse;

        float OriginalZPos;

        void Awake()
        {
            OriginalZPos = transform.position.z;
        }

        public void OnHover(PointerEventData eventData, PGISlot slot)
        {
            if (slot == null || slot.Item == null) return;
            TooltipInfo desc = slot.Item.GetComponent<TooltipInfo>();
            if (desc == null) return;

            if (HoverDisplay != null && !HoverDisplay.activeSelf)
            {
                HoverDisplay.SetActive(true);
                if (Title != null && slot.Item != null)
                {
                    Title.text = desc.Name;
                    if (ItemDescription != null)
                    {
                        ItemDescription.text = desc.Description;

                    }
                }
                else Title.text = "<Missing item reference>";

                Canvas canvas = HoverDisplay.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    RectTransform trans = HoverDisplay.GetComponent<RectTransform>();
                    if (trans == null) throw new UnityException("The display panel must have a RectTransform component attached in order to be displayed properly.");
                    //Move the hover display to the mouse and then offset above a little
                    Vector2 pos = Utility.PGICanvasMouseFollower.GetPointerPosOnCanvas(canvas, Input.mousePosition) + (Vector3)OffsetFromMouse;
                    trans.position = new Vector3(pos.x, pos.y, OriginalZPos + ZOffset);
                }
                if (canvas == null) throw new UnityException("The display panel must be the child of a canvas in order to be displayed properly.");

            }

        }

        public void OnEndHover(PointerEventData eventData, PGISlot slot)
        {
            if (HoverDisplay != null)
            {
                HoverDisplay.SetActive(false);
                RectTransform trans = HoverDisplay.GetComponent<RectTransform>();
                trans.position = new Vector3(trans.position.x, trans.position.y, OriginalZPos);
            }
        }

    }
}
