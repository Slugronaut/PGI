using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PowerGridInventory
{
    /// <summary>
    /// Attach this to your view in order to make it a selectable object.
    /// You can then provide a default grid location to select in the view.
    /// </summary>
    public class GridSelectable : Selectable
    {
        [Tooltip("The View that the selection will be deferred to.")]

        public PGIView View;
        [Tooltip("The x location of the slot in the view that should be selected when this object is selected.")]
        public int CellX;

        [Tooltip("The y location of the slot in the view that should be selected when this object is selected.")]
        public int CellY;


        public override void OnSelect(BaseEventData eventData)
        {
            if (View == null) return;
            var slot = View.GetSlotCell(CellX, CellY);

            //due to the fact that other things may try to change the selection
            //after this, we need to delay this by a frame
            if (slot != null)
                StartCoroutine(DelaySelect(slot));
        }

        IEnumerator DelaySelect(PGISlot slot)
        {
            yield return new WaitForEndOfFrame();
            if (slot.OverridingSlot != null)
                slot.OverridingSlot.Select();
            else slot.Select();
            yield break;
        }
    }
}