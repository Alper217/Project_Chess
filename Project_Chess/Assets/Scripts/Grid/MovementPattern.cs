using System.Collections.Generic;
using UnityEngine;
using AlperKocasalih.Chess.Grid.Utils;

namespace AlperKocasalih.Chess.Grid
{
    [CreateAssetMenu(fileName = "NewMovementPattern", menuName = "Chess/Movement Pattern")]
    public class MovementPattern : ScriptableObject
    {
        public string patternName;
        [HideInInspector]
        public bool[] gridData = new bool[49]; // 7x7

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
                        int dq = x - centerIdxX;
                        int dr = -(y - centerIdxY); 
                        activeOffsets.Add(new Vector2Int(dq, dr));
                    }
                }
            }
            return activeOffsets;
        }

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
            if (rangeModifier == 0 || localOffsets.Count == 0) return localOffsets;

            List<Vector2Int> modifiedOffsets = new List<Vector2Int>(localOffsets);
            Vector3Int localCenterCube = HexGridMath.OffsetToCube(new Vector2Int(3, 3));

            // Store max distance per exact direction string to avoid floating point key issues
            Dictionary<string, int> maxDistPerDir = new Dictionary<string, int>();
            Dictionary<string, Vector3> dirVectors = new Dictionary<string, Vector3>();

            foreach (var loc in localOffsets)
            {
                Vector2Int absLoc = new Vector2Int(3 + loc.x, 3 + loc.y);
                Vector3Int cubePos = HexGridMath.OffsetToCube(absLoc);
                Vector3Int diff = cubePos - localCenterCube;

                if (diff == Vector3Int.zero) continue;

                int dist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
                Vector3 dir = new Vector3((float)diff.x / dist, (float)diff.y / dist, (float)diff.z / dist);
                
                // Use a string key for the direction to ensure points in the same line are grouped
                string dirKey = dir.ToString("F2"); 

                if (!maxDistPerDir.ContainsKey(dirKey) || dist > maxDistPerDir[dirKey])
                {
                    maxDistPerDir[dirKey] = dist;
                    dirVectors[dirKey] = dir;
                }
            }

            if (rangeModifier > 0)
            {
                foreach (var kvp in maxDistPerDir)
                {
                    string key = kvp.Key;
                    Vector3 dir = dirVectors[key];
                    int currentMaxDist = kvp.Value;

                    for (int i = 1; i <= rangeModifier; i++)
                    {
                        int newDist = currentMaxDist + i;
                        // For hex grid, we must round to nearest cube coordinate carefully
                        Vector3 fracPos = new Vector3(dir.x * newDist, dir.y * newDist, dir.z * newDist);
                        Vector3Int newDiff = HexGridMath.CubeRound(fracPos);
                        
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
                int shrinkAmount = -rangeModifier;
                modifiedOffsets.Clear();

                foreach (var loc in localOffsets)
                {
                    Vector2Int absLoc = new Vector2Int(3 + loc.x, 3 + loc.y);
                    Vector3Int cubePos = HexGridMath.OffsetToCube(absLoc);
                    Vector3Int diff = cubePos - localCenterCube;
                    
                    if (diff == Vector3Int.zero) 
                    {
                        modifiedOffsets.Add(loc);
                        continue;
                    }

                    int dist = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
                    Vector3 dir = new Vector3((float)diff.x / dist, (float)diff.y / dist, (float)diff.z / dist);
                    string dirKey = dir.ToString("F2");

                    int maxDistForThisDir = maxDistPerDir.ContainsKey(dirKey) ? maxDistPerDir[dirKey] : dist;
                    if (dist <= maxDistForThisDir - shrinkAmount)
                    {
                        modifiedOffsets.Add(loc);
                    }
                }
            }

            return modifiedOffsets;
        }
    }
}
