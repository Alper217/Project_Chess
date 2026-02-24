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
        [SerializeField] private int playerID; // 1 or 2
        [SerializeField] private int typeIndex;
        [SerializeField, ReadOnly] private HexCell currentCell;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer meshRenderer;
        private Color originalColor;

        #endregion

        #region Properties

        public string PawnType => pawnType;
        public HexCell OccupiedCell => currentCell;
        public int PlayerID { get => playerID; set => playerID = value; }
        public int TypeIndex { get => typeIndex; set => typeIndex = value; }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the pawn and links it to a cell.
        /// </summary>
        public void Initialize(HexCell cell)
        {
            currentCell = cell;
            currentCell.IsOccupied = true;
            
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null) originalColor = meshRenderer.material.color;
        }

        public void SetCell(HexCell cell)
        {
            currentCell = cell;
        }

        /// <summary>
        /// Highlights the pawn visually.
        /// </summary>
        public void VisualHighlight(Color color)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Resets the pawn's visual highlight.
        /// </summary>
        public void ResetHighlight()
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = originalColor;
            }
        }

        #endregion
    }
}
