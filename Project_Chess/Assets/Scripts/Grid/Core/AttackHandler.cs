using System;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using AlperKocasalih.Chess.Grid.Utils;
using UnityEngine.Diagnostics;

public class AttackHandler : NetworkBehaviour
{
    public static AttackHandler instance;
    public NetworkVariable<int>  currentCooldown = new NetworkVariable<int>(0,NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Pawn pawn;

    private void Awake()
    {
        if (instance == null) instance = this;
        pawn = GetComponent<Pawn>();
    }

    protected override void OnNetworkPostSpawn()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
    }

    private void HandleTurnChanged(int activePlayerID)
    {
        if (activePlayerID == pawn.PlayerID && currentCooldown.Value > 0)
        {
            currentCooldown.Value--;
        }
    }
    
    public bool CanAttack() => currentCooldown.Value <= 0;

    public void ExecuteAttack(Vector2Int targetPos, Dictionary<Vector2Int, HexCell> gridLookup)
    {
        if (!CanAttack() || !IsServer ) return;

        if (pawn.PawnData.isAoE)
        {
            ApplyAreaDamage(targetPos, pawn.PawnData.AoERadius, gridLookup);
        }
        else
        {
           ApplySingleDamage(targetPos, gridLookup, pawn.PawnData.damage);
        }

        currentCooldown.Value = pawn.PawnData.attackCooldown;
    }

    private void ApplySingleDamage(Vector2Int targetPos, Dictionary<Vector2Int, HexCell> gridLookup, int damageAmount)
    {
        if (gridLookup.TryGetValue(targetPos, out HexCell targetCell))
        {
            Pawn targetPawn = FindPawnOnCell(targetCell);
            if (targetPawn != null && targetPawn.PlayerID != pawn.PlayerID)
            {
                var healthController = targetPawn.GetComponent<PawnHealthController>();
                if (healthController != null)
                {
                    healthController.TakeDamageServer(damageAmount);
                }
            }
        }
    }

    private void ApplyAreaDamage(Vector2Int centerPos, int radius,  Dictionary<Vector2Int, HexCell> gridLookup)
    {
        Dictionary<Vector2Int, int> areaTiles = GetHexesWithDistance(centerPos, radius);
        
        foreach (var tile in areaTiles)
        {
            Vector2Int pos = tile.Key;
            int distance = tile.Value;

            int finalDamage = pawn.damage.Value - (distance * pawn.PawnData.AoEDamageFallOff);
            Debug.Log($"Hedef: {pos}, Merkezden Uzaklık: {distance}, Vurulan Hasar: {finalDamage}");
            if (finalDamage > 0)
            {
                ApplySingleDamage(pos, gridLookup, finalDamage);
            }
        }
    }

    private Dictionary<Vector2Int, int> GetHexesWithDistance(Vector2Int centerOffset, int radius)
    {
        Dictionary<Vector2Int, int> results = new Dictionary<Vector2Int, int>();
        Vector3Int centerCube = HexGridMath.OffsetToCube(centerOffset);

        for (int i = -radius; i <= radius; i++)
        {
            for (int k = Mathf.Max(-radius, -i - radius); k <= Mathf.Min(radius, -i + radius); k++)
            {
                int s = -i - k;
                Vector3Int offsetCube = new Vector3Int(i, k, s);
                Vector3Int targetCube = centerCube + offsetCube;
                
                int distance = (Mathf.Abs(i)+ Mathf.Abs(k) + Mathf.Abs(s)) / 2;
                
                results.Add(HexGridMath.CubeToOffset(targetCube), distance);
            }
        }
        return results;
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

