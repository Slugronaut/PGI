using UnityEngine;


namespace PowerGridInventory.Extensions
{
    /// <summary>
    /// A utility component that can be used in the editor
    /// to disable/enable specific slots within a <see cref="PGIModel"/>'s grid.
    /// </summary>
    [AddComponentMenu("Power Grid Inventory/Extensions/Model Grid Mask", 20)]
    public class ModelGridMask : MonoBehaviour
    {
        [Tooltip("The model's whose grid will be affected by this mask.")]
        public PGIModel SourceModel;
    }
}