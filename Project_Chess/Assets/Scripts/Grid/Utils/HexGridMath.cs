using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid.Utils
{
    public static class HexGridMath
    {
        public static List<Vector2Int> GenerateAccurateWorldOffsetsFromPattern(Vector2Int centerWorldCoords, List<Vector2Int> localPatternOffsets, bool rotate180 = false)
        {
            List<Vector2Int> worldCoords = new List<Vector2Int>();
            Vector3Int worldCenterCube = OffsetToCube(centerWorldCoords);
            Vector3Int localCenterCube = OffsetToCube(new Vector2Int(3, 3)); // 3,3 is the pattern center

            foreach (var localOffsetCoord in localPatternOffsets)
            {
                Vector2Int absoluteLocalCoord = new Vector2Int(3 + localOffsetCoord.x, 3 + localOffsetCoord.y);
                Vector3Int localCube = OffsetToCube(absoluteLocalCoord);
                
                // Difference in cube space
                Vector3Int diff = localCube - localCenterCube;
                
                if (rotate180)
                {
                    // Rotating 180 degrees in hex cube coords is simply negating the cube vector coords
                    diff = new Vector3Int(-diff.x, -diff.y, -diff.z);
                }

                // Add diff to world center in cube space
                Vector3Int newWorldCube = worldCenterCube + diff;
                
                Vector2Int finalWorldOffset = CubeToOffset(newWorldCube);
                worldCoords.Add(finalWorldOffset);
            }

            return worldCoords;
        }

        public static Vector3Int OffsetToCube(Vector2Int hex)
        {
            int q = hex.x;
            int r = hex.y;
            int x = q;
            int z = r - (q - (q & 1)) / 2;
            int y = -x - z;
            return new Vector3Int(x, y, z);
        }

        public static Vector2Int CubeToOffset(Vector3Int cube)
        {
            int q = cube.x;
            int r = cube.z + (cube.x - (cube.x & 1)) / 2;
            return new Vector2Int(q, r);
        }

        public static int CubeDistance(Vector3Int a, Vector3Int b)
        {
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
        }

        public static Vector3 CubeLerp(Vector3Int a, Vector3Int b, float t)
        {
            return new Vector3(
                Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.z, b.z, t)
            );
        }

        public static Vector3Int CubeRound(Vector3 frac)
        {
            int q = Mathf.RoundToInt(frac.x);
            int r = Mathf.RoundToInt(frac.y);
            int s = Mathf.RoundToInt(frac.z);

            float q_diff = Mathf.Abs(q - frac.x);
            float r_diff = Mathf.Abs(r - frac.y);
            float s_diff = Mathf.Abs(s - frac.z);

            if (q_diff > r_diff && q_diff > s_diff)
                q = -r - s;
            else if (r_diff > s_diff)
                r = -q - s;
            else
                s = -q - r;

            return new Vector3Int(q, r, s);
        }

        public static Vector2Int GetMirroredOffset(Vector2Int offset, int startingQ)
        {
            int newDQ = offset.x;
            int newDR = -offset.y;

            if (Mathf.Abs(newDQ) % 2 != 0)
            {
                if (startingQ % 2 == 0) newDR -= 1; 
                else newDR += 1;                  
            }

            return new Vector2Int(newDQ, newDR);
        }
    }
}
