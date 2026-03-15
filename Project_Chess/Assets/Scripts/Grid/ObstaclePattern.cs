using System.Collections.Generic;
using UnityEngine;
using AlperKocasalih.Chess.Grid.Utils;

namespace AlperKocasalih.Chess.Grid
{
    [CreateAssetMenu(fileName = "NewObstaclePattern", menuName = "Chess/Obstacle Pattern")]
    public class ObstaclePattern : ScriptableObject
    {
        public string patternName;
        [Header("Identity")]
        public string cardName;
        public Sprite cardSprite;
        // 7x7 grid representation (flat array for Unity serialization)
        // True means the target hex is an obstacle
        [HideInInspector]
        public bool[] gridData = new bool[49]; // 7x7

        /// <summary>
        /// Gets the boolean value at grid coordinates where center is (3,3).
        /// Returns relative offsets if the cell is true.
        /// </summary>
        public List<Vector2Int> GetObstacleOffsets(int centerQ)
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
                        int dr = -(y - centerIdxY); // Invert Y because array 0,0 is usually top-left, but hex R goes up/down. Let's just use raw distance.

                        activeOffsets.Add(new Vector2Int(dq, dr));
                    }
                }
            }
            return activeOffsets;
        }

        /// <summary>
        /// Converts the pattern into movement offsets in world coordinates,
        /// accounting for odd-q layout and optional 180-degree rotation.
        /// </summary>
        public List<Vector2Int> GetMovementOffsets(Vector2Int centerWorldCoords, bool rotate180)
        {
            List<Vector2Int> localOffsets = GetObstacleOffsets(centerWorldCoords.x);
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
