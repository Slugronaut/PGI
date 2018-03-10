/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using UnityEngine;
using Toolbox.Common;

namespace PowerGridInventory.Utility
{
    /// <summary>
    /// Helper component that allows a gameobject to follow the mouse
    /// as it travels over a uGUI canvas.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Power Grid Inventory/Utility/Mouse Follower", 20)]
    public class PGICanvasMouseFollower : MonoBehaviour
    {
        /// <summary>
        /// The canvas that will be used when determining
        /// where to position this GameObject.
        /// </summary>
        public Canvas Canvas;


        void Update()
        {
            transform.position = GetPointerPosOnCanvas(Canvas, PointerUtility.GetPosition());
        }

        /// <summary>
        /// Global static helper method for finding the position
        /// of the mouse on a canvas in any rendering mode.
        /// </summary>
        /// <remarks>Note that in world-rendering mode the position
        /// of the mouse is projected to the canvas so the resulting
        /// Vector2 may appear slightly offset in some cases if the
        /// z-axis is not flattended.</remarks>
        /// <returns>The projected mouse position on canvas.</returns>
        /// <param name="canvas">The canvas to find the mouse position on.</param>
        public static Vector3 GetPointerPosOnCanvas(Canvas canvas, Vector2 pointerPos)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, pointerPos, canvas.worldCamera, out pos);
                return canvas.transform.TransformPoint(pos);
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return PointerUtility.GetPosition();
            }
            else
            {
                Vector3 globalMousePos;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas.transform as RectTransform, PointerUtility.GetPosition(), canvas.worldCamera, out globalMousePos))
                {
                    return globalMousePos;
                }
            };

            return Vector2.zero;
        }

    }
}
