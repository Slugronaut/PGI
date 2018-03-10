using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;
using Toolbox;
using UnityEngine.EventSystems;

namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// Helper component that can be used to select a default gird location to hilight
    /// when this inventory is enabled. This is primarily for Gamepad support
    /// since slot locations can be specified in the InputModule due to PGI dynamically
    /// re-creating the grid at startup.
    /// 
    /// TODO: PGIView should have an event that is triggered when is disabled/enables rendering
    /// so that we can plug this into it.
    /// 
    /// </summary>
    public class GamepadDefaultSelect : MonoBehaviour
    {

        public enum EventTrigger
        {
            Start,
            Enable,
        }

        [Tooltip("The view this will affect.")]
        public PGIView View;

        [Tooltip("When does this occur?")]
        public EventTrigger HappensOn;

        [Tooltip("The x-coordinate of the slot to select by default.")]
        public int x;

        [Tooltip("The y-coordinate of the slot to select by default.")]
        public int y;

        public float Delay;


        void Start()
        {
            if (HappensOn == EventTrigger.Start)
                Invoke("Select", Delay);
        }
        
        void OnEnable()
        {
            //using a slight delay so that we can be sure all other objects
            //(including the view!) are enabled first, otherwise we might
            //not have our slots generated yet.
            if (HappensOn == EventTrigger.Enable)
                Invoke("Select", Delay);
        }

        public void Select()
        {
            if (!isActiveAndEnabled) return;
            //var view = GetComponent<PGIView>();
            if (View == null) return;
            PGISlot slot = null;
            try { slot = View.GetSlotCell(x, y); }
            #pragma warning disable 0168
            catch (ArgumentOutOfRangeException e)
            #pragma warning restore 0168
            {
                throw new UnityException("The view '" + View.name + "' does not have a slot at the location (" + x + "," + y + ").");
            }

            
            slot.Select();
            //slot.OnSelect(null); //HACK: workaround for broken selection UI
            StartCoroutine(StupidDelayBecauseSelectableIsStupid(slot.gameObject));
        }

        IEnumerator StupidDelayBecauseSelectableIsStupid(GameObject selectedGo)
        {
            yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectedGo);
        }
    }
}
