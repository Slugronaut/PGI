/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Toolbox.Common
{
    /// <summary>
    /// Utility object that merges mouse and touchcreen input
    /// into a single normalized interface.
    /// Also helps with tracking UI-presses.
    /// </summary>
    public static class PointerUtility
    {

        private static bool StartedOverUI = false;

        /// <summary>
        /// Returns the position of the pointer.
        /// </summary>
        /// <param name="pointerId"></param>
        /// <returns></returns>
        public static Vector3 GetPosition(int pointerId = 0)
        {
            return Input.mousePosition;
        }

        /// <summary>
        /// Returns true if the pointer action occurred on a UI element.
        /// </summary>
        /// <returns></returns>
        public static bool PressedUI()
        {
            EventSystem eventSystem = EventSystem.current;
            if (Input.touchSupported && Input.touchCount > 0)
            {
                if (eventSystem != null && eventSystem.IsPointerOverGameObject())
                {
                    //touch
                    return true;
                }
            }
            else if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                //mouse
                return true;
            }

            return (GUIUtility.hotControl != 0);
        }

        /// <summary>
        /// Returns true if pointer was released this frame.
        /// </summary>
        /// <returns></returns>
        public static bool GetPointerUp(int pointerId = 0)
        {
            if (PressedUI()) return false;

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(pointerId);
                if (Input.GetTouch(pointerId).fingerId == 0 && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
                {
                    return true;
                }
                else if (Input.GetMouseButtonUp(pointerId))
                {
                    return true;
                }
            }
            else if (Input.GetMouseButtonUp(pointerId))
            {
                return true;
            }


            return false;
        }

        /// <summary>
        /// Returns true if pointer was pressed this frame.
        /// </summary>
        /// <returns></returns>
        public static bool GetPointerDown(int pointerId = 0, bool ignoreUI = false)
        {
            if (ignoreUI)
            {
                if (PressedUI()) return false;
            }

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(pointerId);
                if (t.fingerId == 0 && t.phase == TouchPhase.Began)
                {
                    return true;
                }
                else if (Input.GetMouseButtonDown(pointerId))
                {
                    return true;
                }
            }
            else if (Input.GetMouseButtonDown(pointerId))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if pointer was held down this frame and false if it was not.
        /// </summary>
        /// <returns></returns>
        public static bool GetPointerHeld(int pointerId = 0)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(pointerId);
                if (t.fingerId == 0 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
                {
                    return CheckUIStatus(pointerId);
                }
                else if (Input.GetMouseButtonDown(pointerId))
                {
                    return CheckUIStatus(pointerId);
                }
            }
            else if (Input.GetMouseButton(pointerId))
            {
                return CheckUIStatus(pointerId);
            }

            StartedOverUI = false;
            return false;
        }

        /// <summary>
        /// Helper method for checking if the pointer started over a UI element or not.
        /// This way we can tell if a hold-operation should be considered valid or not.
        /// </summary>
        /// <param name="pointerId"></param>
        /// <returns></returns>
        private static bool CheckUIStatus(int pointerId)
        {
            if (PressedUI())
            {
                StartedOverUI = true;
                return false;
            }
            return !StartedOverUI;
        }
    }


    [Serializable]
    public class PointerEvent : UnityEvent<PointerEventData> { }



    public enum PointerButton
    {
        Any = -1,
        Left = 0,
        Right = 1,
        Middle = 2,
    }
}