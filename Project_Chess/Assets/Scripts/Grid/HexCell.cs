using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Represents a single hexagonal cell in the grid.
    /// Stores its grid coordinates (Q, R).
    /// </summary>
    public class HexCell : MonoBehaviour
    {
        #region Fields

        [Header("Coordinates")]
        [SerializeField, ReadOnly] private int q;
        [SerializeField, ReadOnly] private int r;

        [Header("Status")]
        [SerializeField, ReadOnly] private bool isOccupied;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer meshRenderer;
        private Color originalColor;

        #endregion

        #region Properties

        public int Q => q;
        public int R => r;
        public Vector2Int Coordinates => new Vector2Int(q, r);
        public bool IsOccupied
        {
            get => isOccupied;
            set => isOccupied = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the cell with its grid coordinates.
        /// </summary>
        /// <param name="q">Column index.</param>
        /// <param name="r">Row index.</param>
        public void Initialize(int q, int r)
        {
            this.q = q;
            this.r = r;
            
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) originalColor = meshRenderer.material.color;
        }

        /// <summary>
        /// Highlights the cell with a specific color.
        /// </summary>
        public void Highlight(Color color)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Resets the cell highlight to its original color.
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
