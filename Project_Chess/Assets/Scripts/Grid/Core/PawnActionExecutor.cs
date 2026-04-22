using UnityEngine;
using Unity.Netcode;
using DG.Tweening;
using System.Collections.Generic;

namespace AlperKocasalih.Chess.Grid.Core
{
    public class PawnActionExecutor : NetworkBehaviour
    {
        public static PawnActionExecutor Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float moveDuration = 0.5f;
        [SerializeField] private Vector3 pawnVisualOffset = new Vector3(0, 0.5f, 0);

        private Dictionary<Vector2Int, HexCell> gridLookup;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void InitializeGridReference(Dictionary<Vector2Int, HexCell> lookup)
        {
            gridLookup = lookup;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExecuteMoveServerRpc(ulong pawnNetworkID, Vector2Int targetCoords)
        {
            NetworkObject pawnObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pawnNetworkID];
            if (pawnObj == null) return;

            Pawn pawn = pawnObj.GetComponent<Pawn>();
            if (gridLookup != null && gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
            {
                HexCell oldCell = pawn.OccupiedCell;
                if (oldCell != null) oldCell.IsOccupied = false;
                
                pawn.SetCell(targetCell);
                targetCell.IsOccupied = true;
                
                ExecuteMoveClientRpc(pawnNetworkID, targetCoords);
                
                if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
            }
        }

        [ClientRpc]
        private void ExecuteMoveClientRpc(ulong pawnNetworkID, Vector2Int targetCoords)
        {
            NetworkObject pawnObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pawnNetworkID];
            if (pawnObj == null) return;

            Pawn pawn = pawnObj.GetComponent<Pawn>();
            if (gridLookup != null && gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
            {
                HexCell oldCell = pawn.OccupiedCell;
                if (oldCell != null) oldCell.IsOccupied = false;
                
                pawn.SetCell(targetCell);
                targetCell.IsOccupied = true;

                pawn.transform.DOMove(targetCell.transform.position + pawnVisualOffset, moveDuration)
                    .SetEase(Ease.OutQuad);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExecuteCombatServerRpc(ulong attackerID, ulong defenderID, Vector2Int targetCoords)
        {
            NetworkObject attackerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID];
            NetworkObject defenderObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[defenderID];

            if (attackerObj != null && defenderObj != null)
            {
                Pawn attacker = attackerObj.GetComponent<Pawn>();
                Pawn defender = defenderObj.GetComponent<Pawn>();
                if (attacker == null || defender == null) return;
                
                if (attacker.PlayerID == defender.PlayerID && !attacker.PawnData.isHealer)
                {
                    Debug.LogWarning("PawnActionExecutor: Friendly fire attempt blocked on server.");
                    return;
                }
                if (attacker.PlayerID != defender.PlayerID && attacker.PawnData.isHealer)
                {
                    Debug.LogWarning("Şifacılar düşman birimlerini iyileştiremez!");
                    return;
                }

                AttackHandler attackHandler = attacker.GetComponent<AttackHandler>();
                if (attackHandler != null && attackHandler.CanAttack())
                {
                    attackHandler.ExecuteAttack(defender.OccupiedCell.Coordinates, gridLookup);
                    if(TurnManager.Instance != null) TurnManager.Instance.NextTurn();
                }
                else
                {
                    Debug.Log("Attack on cooldown.");
                }
                

               

            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ApplyCardEffectServerRpc(ulong pawnNetworkID, int cardIndex)
        {
            NetworkObject pawnObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pawnNetworkID];
            if (pawnObj == null) return;

            Pawn pawn = pawnObj.GetComponent<Pawn>();
            ApplyCardEffectIfApplicable(pawn, cardIndex);
        }

        private void ApplyCardEffectIfApplicable(Pawn pawn, int cardIndex)
        {
            if (!IsServer) return;
            if (pawn == null) return;
            if (cardIndex < 0) return;
            if (DeckManager.Instance == null) return;

            CardData card = DeckManager.Instance.GetCardByIndex(cardIndex);
            if (card == null) return;
            if (pawn.PawnData == null) return;

            bool isMatch = (pawn.PawnData.type == card.pawnClass);

            // 1. Handle Legacy Stats (healthToAdd, damageToAdd)
            int finalH = 0;
            int finalD = 0;

            // Health: Apply debuff always, buff only if match
            if (card.healthToAdd < 0) finalH = card.healthToAdd;
            else if (isMatch) finalH = card.healthToAdd;

            // Damage: Apply debuff always, buff only if match
            if (card.damageToAdd < 0) finalD = card.damageToAdd;
            else if (isMatch) finalD = card.damageToAdd;

            if (finalH != 0 || finalD != 0)
            {
                pawn.ApplyCardEffectServer(finalH, finalD);
            }

            // 2. Handle Runtime Buffs
            if (card.runtimeBuffs != null && card.runtimeBuffs.Count > 0)
            {
                List<BuffData> buffsToApply = new List<BuffData>();
                foreach (var b in card.runtimeBuffs)
                {
                    if (b == null) continue;
                    
                    // Apply debuffs (negative effects) always. 
                    // Apply buffs (positive effects) only if the pawn class matches.
                    if (!b.isPositiveEffect || isMatch)
                    {
                        buffsToApply.Add(b);
                    }
                }

                if (buffsToApply.Count > 0)
                {
                    pawn.ApplyRuntimeBuffsServer(buffsToApply);
                }
            }
        }
    }
}
