using System.Collections.Generic;
using UnityEngine;
using AlperKocasalih.Chess.Grid.Utils;

namespace AlperKocasalih.Chess.Grid
{
    [CreateAssetMenu(fileName = "NewMovementPattern", menuName = "Chess/Movement Pattern")]
    public class MovementPattern : ScriptableObject
    {
        public string patternName;
        // 7x7 grid representation (flat array for Unity serialization)
        // True means the target hex is a valid move
        [HideInInspector]
        public bool[] gridData = new bool[49]; // 7x7

        /// <summary>
        /// Gets the boolean value at grid coordinates where center is (3,3).
        /// Returns relative offsets if the cell is true.
        /// </summary>
        private List<Vector2Int> GetPatternOffsets()
        {
            List<Vector2Int> activeOffsets = new List<Vector2Int>();
            int centerIdxX = 3;
            int centerIdxY = 3;

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 7; y++)
                {
                    int index = y * 7 + x;
                    if (gridData[index])
                    {
                        // Calculate offset from center (3,3)
                        int dq = x - centerIdxX;
                        int dr = -(y - centerIdxY); // Invert Y because array 0,0 is top-left

                        activeOffsets.Add(new Vector2Int(dq, dr));
                    }
                }
            }
            return activeOffsets;
        }

        /// <summary>
        /// Converts the pattern into movement offsets in world coordinates,
        /// using the pawn as the center and accounting for odd-q layout and optional 180-degree rotation.
        /// </summary>
        public List<Vector2Int> GetValidOffsets(Vector2Int centerWorldCoords, bool rotate180)
        {
            List<Vector2Int> localOffsets = GetPatternOffsets();
            List<Vector2Int> worldCoords = HexGridMath.GenerateAccurateWorldOffsetsFromPattern(
                centerWorldCoords,
                localOffsets,
                rotate180
            );

            List<Vector2Int> movementOffsets = new List<Vector2Int>(worldCoords.Count);
            foreach (var worldCoord in worldCoords)
            {
                Vector2Int offset = worldCoord - centerWorldCoords;
                if (offset != Vector2Int.zero)
                {
                    movementOffsets.Add(offset);
                }
            }
            return movementOffsets;
        }
    }
}
