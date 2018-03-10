using UnityEngine;
using System.Linq;


namespace PowerGridInventory
{
    /// <summary>
    /// This component allows us to override the default prefab used by a <see cref="PGIView"/>.
    /// It must be attached to the same GameObject as the View that will use it. Multiple
    /// prefabs can be supplied to allow for multiple prefabs. If any two overrides override
    /// the same location, the first one found will be used by the view.
    /// </summary>
    [RequireComponent(typeof(PGIView))]
    public sealed class SlotPrefabOverride : MonoBehaviour
    {
        [Tooltip("This slot will be used in place of the default one for the given PGIView.")]
        public PGISlot PrefabOverride;

        [Tooltip("All slots for any of the zero-index rows of the view will use this component's slot prefab override.")]
        public int[] Rows;

        [Tooltip("All slots for any of the zero-index columns of the view will use this component's slot prefab override.")]
        public int[] Columns;

        [Tooltip("Any cells in the view at these locations will use the slot prefab override.")]
        public PGIModel.Pos[] Cells;

        /// <summary>
        /// Returns true if this component provides an override slot prefab for the given location.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool OverridesLocation(int x, int y)
        {
            if (Rows.Any((row) => y == row)) return true;
            else if (Columns.Any((column) => x == column)) return true;
            else if (Cells.Any((cell) => x == cell.X && y == cell.Y)) return true;
            else return false;
        }
    }
}