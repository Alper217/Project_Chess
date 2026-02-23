using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Represents a pawn in the game.
    /// Manages its relationship with the HexCell it occupies.
    /// </summary>
    public class Pawn : MonoBehaviour
    {
        #region Fields

        [Header("Pawn Data")]
        [SerializeField] private string pawnType;
        [SerializeField, ReadOnly] private HexCell currentCell;

        #endregion

        #region Properties

        public string PawnType => pawnType;
        public HexCell CurrentCell => currentCell;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the pawn and links it to a cell.
        /// </summary>
        public void Initialize(HexCell cell)
        {
            currentCell = cell;
            currentCell.IsOccupied = true;
            
            // Look at the center of the grid or keep original rotation
            // transform.LookAt(new Vector3(0, transform.position.y, 0));
        }

        #endregion
    }
}
