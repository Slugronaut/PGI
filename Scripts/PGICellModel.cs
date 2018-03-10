using UnityEngine;


namespace PowerGridInventory
{
    /// <summary>
    /// Behaviour that contains a <see cref="CellModel"/> reference.
    /// </summary>
    public class PGICellModel : MonoBehaviour
    {
        public CellModel Cell { get; protected set; }

        public void InitCell(int equipmentIndex, PGIModel model)
        {
            Cell = new CellModel(equipmentIndex, model);
        }
    }
}
