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

    public override void OnNetworkSpawn()
    {
        if (TurnManager.Instance != null && IsServer)
        {
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged; // Safe unsubscribe
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        }
    }

    private void Start()
    {
        if (IsServer && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged; // Safe unsubscribe
            TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (TurnManager.Instance != null && IsServer)
        {
            TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
        }
    }

    private void HandleTurnChanged(int activePlayerID)
    {
        if (!IsServer) return;
        if (activePlayerID == pawn.PlayerID && currentCooldown.Value > 0)
        {
            currentCooldown.Value--;
        }
    }
    
    public bool CanAttack() => (currentCooldown.Value <= 0 || pawn.HasDoubleUseBuff()) && !pawn.HasStun();

    public void ExecuteAttack(Vector2Int targetPos, Dictionary<Vector2Int, HexCell> gridLookup)
    {
        if (!CanAttack() || !IsServer ) return;

        if (pawn.PawnData.isAoE)
        {
            ApplyAreaDamage(targetPos, pawn.PawnData.AoERadius, gridLookup);
        }
        else
        {
           ApplySingleDamage(targetPos, gridLookup, pawn.damage.Value);
        }

        bool usedDoubleUse = currentCooldown.Value > 0 && pawn.HasDoubleUseBuff();
        if (usedDoubleUse) {
            pawn.ConsumeDoubleUseBuff();
        } else {
            currentCooldown.Value = pawn.PawnData.attackCooldown;
        }
    }

    private void ApplySingleDamage(Vector2Int targetPos, Dictionary<Vector2Int, HexCell> gridLookup, int damageAmount)
    {
        if (gridLookup.TryGetValue(targetPos, out HexCell targetCell))
        {
            Pawn targetPawn = FindPawnOnCell(targetCell);
            if (targetPawn != null && targetPawn.PlayerID != pawn.PlayerID)
            {
                if (targetPawn.ConsumeDamageBlock())
                {
                    var hc = targetPawn.GetComponent<PawnHealthController>();
                    if (hc != null) hc.ShowBlockedFeedbackClientRpc();
                    return;
                }

                float outMultiplier = pawn.GetOutgoingDamageMultiplier();
                float inMultiplier = targetPawn.GetIncomingDamageMultiplier();
                int finalDamage = Mathf.RoundToInt(damageAmount * outMultiplier * inMultiplier);

                Debug.Log($"[COMBAT] Attacker: {pawn.PawnData.pawnName} (Mult: {outMultiplier}) -> " +
                          $"Target: {targetPawn.PawnData.pawnName} (Mult: {inMultiplier}) | " +
                          $"Base: {damageAmount}, Final: {finalDamage}");

                var healthController = targetPawn.GetComponent<PawnHealthController>();
                if (healthController != null)
                {
                    healthController.TakeDamageServer(finalDamage);

                    // --- NEW: LIFESTEAL ---
                    float lifestealPct = pawn.GetLifestealPercentage();
                    if (lifestealPct > 0)
                    {
                        int healAmount = Mathf.RoundToInt(finalDamage * lifestealPct);
                        var selfHealth = pawn.GetComponent<PawnHealthController>();
                        if (selfHealth != null) selfHealth.HealServer(healAmount);
                    }

                    // --- NEW: RECOIL ---
                    float recoilPct = pawn.GetRecoilPercentage();
                    if (recoilPct > 0)
                    {
                        int recoilDamage = Mathf.RoundToInt(finalDamage * recoilPct);
                        var selfHealth = pawn.GetComponent<PawnHealthController>();
                        if (selfHealth != null) selfHealth.TakeDamageServer(recoilDamage);
                    }
                }
            }

            if (pawn.PawnData.isHealer)
            {
                if (targetPawn.PlayerID == pawn.PlayerID)
                {
                    PawnHealthController healthController = targetPawn.GetComponent<PawnHealthController>();
                    healthController.HealServer(pawn.PawnData.healAmount);
                }
            }
        }
    }

    private void ApplyAreaDamage(Vector2Int centerPos, int radius,  Dictionary<Vector2Int, HexCell> gridLookup)
    {
        Dictionary<Vector2Int, int> areaTiles = HexGridMath.GetHexesWithDistance(centerPos, radius);
        
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

