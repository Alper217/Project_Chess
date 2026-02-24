using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    [CreateAssetMenu(fileName = "NewMovementPattern", menuName = "Chess/Movement Pattern")]
    public class MovementPattern : ScriptableObject
    {
        public string patternName;
        
        [Tooltip("Offsets to use when the column (Q) is Even (0, 2, 4...)")]
        public List<Vector2Int> evenColumnOffsets;

        [Tooltip("Offsets to use when the column (Q) is Odd (1, 3, 5...)")]
        public List<Vector2Int> oddColumnOffsets;

        /// <summary>
        /// Returns the appropriate offsets based on the target column's parity.
        /// </summary>
        public List<Vector2Int> GetValidOffsets(int q)
        {
            // Odd-Q: q % 2 != 0 is Odd, q % 2 == 0 is Even
            return (Mathf.Abs(q) % 2 == 1) ? oddColumnOffsets : evenColumnOffsets;
        }
    }
}
