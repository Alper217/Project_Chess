using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid.Utils;

public class AuraManager : NetworkBehaviour
{
    public static AuraManager instance;
    [SerializeField] private bool debugLogs = true;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    public void RefreshAuraServer(Dictionary<Vector2Int, HexCell> gridLookup)
    {
        if (debugLogs)
        {
            int lookupCount = gridLookup == null ? -1 : gridLookup.Count;
            Debug.Log($"AuraManager: RefreshAuraServer called. IsServer={IsServer} lookupCount={lookupCount}");
        }
        if (!IsServer) return;
        Dictionary<Vector2Int, HexCell> lookup = gridLookup;
        if (!EnsureGridLookup(ref lookup))
        {
            Debug.LogWarning("AuraManager: Grid lookup is empty. Aura refresh skipped.");
            return;
        }
        if (debugLogs)
        {
            Debug.Log($"AuraManager: Grid lookup ready. count={lookup.Count}");
        }
        Pawn[] allPawns = Object.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
        if (debugLogs)
        {
            Debug.Log($"AuraManager: Found pawns={allPawns.Length}");
        }
        foreach (Pawn pawn in allPawns) pawn.ResetSynergyServer();

        // CACHE ALL PAWNS IN O(1) DICTIONARY TO PREVENT FREEZE
        Dictionary<HexCell, Pawn> cellPawnMap = new Dictionary<HexCell, Pawn>();
        foreach (var p in allPawns)
        {
            if (p.OccupiedCell != null) cellPawnMap[p.OccupiedCell] = p;
        }

        foreach (var auraPawn in allPawns)
        {
            if (auraPawn.PawnData.hasAura)
            {
                ApplyAuraEffect(auraPawn, lookup, cellPawnMap);
            }
        }
    }

    private bool EnsureGridLookup(ref Dictionary<Vector2Int, HexCell> gridLookup)
    {
        if (gridLookup != null && gridLookup.Count > 0) return true;
        if (GridGenerator.Instance == null)
        {
            if (debugLogs) Debug.LogWarning("AuraManager: GridGenerator.Instance is null.");
            return false;
        }

        if (gridLookup == null) gridLookup = new Dictionary<Vector2Int, HexCell>();
        else gridLookup.Clear();

        foreach (var hex in GridGenerator.Instance.SpawnedHexes)
        {
            if (hex == null) continue;
            HexCell cell = hex.GetComponent<HexCell>();
            if (cell != null) gridLookup[cell.Coordinates] = cell;
        }

        return gridLookup.Count > 0;
    }

    private void ApplyAuraEffect(Pawn pawn, Dictionary<Vector2Int, HexCell> gridLookup, Dictionary<HexCell, Pawn> cellPawnMap)
    {
        if (pawn == null)
        {
            if (debugLogs) Debug.LogWarning("AuraManager: ApplyAuraEffect called with null pawn.");
            return;
        }
        if (pawn.PawnData == null)
        {
            if (debugLogs) Debug.LogWarning($"AuraManager: Pawn '{pawn.name}' has no PawnData.");
            return;
        }
        if (pawn.OccupiedCell == null)
        {
            if (debugLogs) Debug.LogWarning($"AuraManager: Pawn '{pawn.name}' has no OccupiedCell.");
            return;
        }

        var affectedHexes = HexGridMath.GetHexesWithDistance(pawn.OccupiedCell.Coordinates, pawn.PawnData.auraRadius);
        int matchedCells = 0;
        int buffsApplied = 0;
        foreach (var hex in affectedHexes)
        {
            Vector2Int hexPos = hex.Key;
            
            if (gridLookup.TryGetValue(hexPos, out var cell))
            {
                matchedCells++;
                cellPawnMap.TryGetValue(cell, out Pawn targetPawn);

                if (targetPawn != null && targetPawn != pawn && targetPawn.PlayerID == pawn.PlayerID)
                {
                    targetPawn.ApplyBuffsServer(pawn.PawnData.healthbuff, pawn.PawnData.damageBuff);
                    buffsApplied++;
                }
            }
        }
        if (debugLogs)
        {
            Debug.Log($"AuraManager: Aura from '{pawn.name}' player={pawn.PlayerID} radius={pawn.PawnData.auraRadius} affected={affectedHexes.Count} matchedCells={matchedCells} buffsApplied={buffsApplied}");
        }
    }
    private Pawn FindPawnOnCell(HexCell cell)
    {
        Pawn[] allPawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
        foreach (var pawn in allPawns)
        {
            if(pawn.OccupiedCell == cell) return pawn;
        }
        return null;
    }
 
}
