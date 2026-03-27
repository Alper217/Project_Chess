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
        /// using the pawn as the center and accounting for odd-q layout, rotation, and range modifiers.
        /// </summary>
        public List<Vector2Int> GetValidOffsets(Vector2Int centerWorldCoords, bool rotate180, int rangeModifier = 0)
        {
            List<Vector2Int> localOffsets = GetPatternOffsets();

            if (rangeModifier != 0)
            {
                localOffsets = ApplyRangeModifier(localOffsets, rangeModifier);
            }

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

        private List<Vector2Int> ApplyRangeModifier(List<Vector2Int> localOffsets, int rangeModifier)
        {
            List<Vector2Int> modifiedOffsets = new List<Vector2Int>(localOffsets);
            Vector3Int localCenterCube = HexGridMath.OffsetToCube(new Vector2Int(3, 3));

            Dictionary<Vector3, int> maxDistPerDir = new Dictionary<Vector3, int>();
            Dictionary<Vector3, Vector3Int> tipDiffPerDir = new Dictionary<Vector3, Vector3Int>();

            // Convert to cube diffs and find tips
            List<Vector3Int> allDiffs = new List<Vector3Int>();
            foreach (var loc in localOffsets)
            {
                Vector2Int absLoc = new Vector2Int(3 + loc.x, 3 + loc.y);
                Vector3Int diff = HexGridMath.OffsetToCube(absLoc) - localCenterCube;
                allDiffs.Add(diff);

                if (diff == Vector3Int.zero) continue;

                int dist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
                Vector3 dir = new Vector3((float)diff.x / dist, (float)diff.y / dist, (float)diff.z / dist);
                
                // Discretize to avoid float issues
                dir = new Vector3(Mathf.Round(dir.x * 100f) / 100f, Mathf.Round(dir.y * 100f) / 100f, Mathf.Round(dir.z * 100f) / 100f);

                if (!maxDistPerDir.ContainsKey(dir) || dist > maxDistPerDir[dir])
                {
                    maxDistPerDir[dir] = dist;
                    tipDiffPerDir[dir] = diff;
                }
            }

            if (rangeModifier > 0)
            {
                // Expand
                foreach (var kvp in tipDiffPerDir)
                {
                    Vector3 dir = kvp.Key;
                    int currentMaxDist = maxDistPerDir[dir];

                    for (int i = 1; i <= rangeModifier; i++)
                    {
                        int newDist = currentMaxDist + i;
                        Vector3Int newDiff = new Vector3Int(
                            Mathf.RoundToInt(dir.x * newDist),
                            Mathf.RoundToInt(dir.y * newDist),
                            Mathf.RoundToInt(dir.z * newDist)
                        );
                        
                        Vector3Int newLocalCube = localCenterCube + newDiff;
                        Vector2Int newAbsLoc = HexGridMath.CubeToOffset(newLocalCube);
                        Vector2Int newLocalOffset = new Vector2Int(newAbsLoc.x - 3, newAbsLoc.y - 3);
                        
                        if (!modifiedOffsets.Contains(newLocalOffset))
                        {
                            modifiedOffsets.Add(newLocalOffset);
                        }
                    }
                }
            }
            else if (rangeModifier < 0)
            {
                // Shrink: Remove points that are at distance > maxDist - |rangeModifier|
                int shrinkAmount = -rangeModifier;
                modifiedOffsets.Clear();

                foreach (var loc in localOffsets)
                {
                    Vector2Int absLoc = new Vector2Int(3 + loc.x, 3 + loc.y);
                    Vector3Int diff = HexGridMath.OffsetToCube(absLoc) - localCenterCube;
                    
                    if (diff == Vector3Int.zero) 
                    {
                        modifiedOffsets.Add(loc);
                        continue;
                    }

                    int dist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
                    Vector3 dir = new Vector3((float)diff.x / dist, (float)diff.y / dist, (float)diff.z / dist);
                    dir = new Vector3(Mathf.Round(dir.x * 100f) / 100f, Mathf.Round(dir.y * 100f) / 100f, Mathf.Round(dir.z * 100f) / 100f);

                    int maxDistForThisDir = maxDistPerDir.ContainsKey(dir) ? maxDistPerDir[dir] : dist;
                    if (dist <= maxDistForThisDir - shrinkAmount || dist == 0)
                    {
                        modifiedOffsets.Add(loc);
                    }
                }
            }

            return modifiedOffsets;
        }
    }
}
