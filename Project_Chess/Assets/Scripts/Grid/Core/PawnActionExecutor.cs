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
        public void ExecuteMoveServerRpc(ulong pawnNetworkID, Vector2Int targetCoords, bool endTurn = true)
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
            }
            
            if (endTurn && TurnManager.Instance != null) TurnManager.Instance.NextTurn();
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

            if (pawn.PawnData != null && pawn.PawnData.moveSound != null)
            {
                AudioManager.instance.PlaySfx(pawn.PawnData.moveSound);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ExecuteCombatServerRpc(ulong attackerID, ulong defenderID, Vector2Int targetCoords, bool endTurn = true)
        {
            NetworkObject attackerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID];
            NetworkObject defenderObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[defenderID];
            
            Pawn pawn = attackerObj.GetComponent<Pawn>();

            if (attackerObj != null && defenderObj != null)
            {
                Pawn attacker = attackerObj.GetComponent<Pawn>();
                Pawn defender = defenderObj.GetComponent<Pawn>();
                if (attacker == null || defender == null) return;
                
                bool isFriendly = (attacker.PlayerID == defender.PlayerID);
                if (isFriendly && !attacker.PawnData.isHealer)
                {
                    Debug.LogWarning("PawnActionExecutor: Friendly fire attempt blocked on server.");
                }
                else
                {
                    AttackHandler attackHandler = attacker.GetComponent<AttackHandler>();
                    if (attackHandler != null && attackHandler.CanAttack())
                    {
                        attackHandler.ExecuteAttack(defender.OccupiedCell.Coordinates, gridLookup);

                        // Broadcast combat sound to ALL clients (including host-client).
                        // Previously AudioManager.PlaySfx was called here on the server only,
                        // which meant clients never heard attack/heal sounds.
                        bool playHeal = isFriendly && attacker.PawnData.isHealer;
                        PlayCombatSoundClientRpc(attackerID, playHeal);
                    }
                    else
                    {
                        Debug.LogWarning("Attack blocked on server (cooldown or missing handler).");
                    }
                }
                }

            if (endTurn && TurnManager.Instance != null) TurnManager.Instance.NextTurn();
        }

        /// <summary>
        /// Plays the attacker's attack or heal sound on every client.
        /// </summary>
        [ClientRpc]
        private void PlayCombatSoundClientRpc(ulong attackerID, bool playHeal)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(attackerID, out NetworkObject attackerObj)) return;

            Pawn attacker = attackerObj.GetComponent<Pawn>();
            if (attacker == null || attacker.PawnData == null) return;

            AudioClip clip = playHeal ? attacker.PawnData.healSound : attacker.PawnData.attackSound;
            if (clip != null && AudioManager.instance != null)
            {
                AudioManager.instance.PlaySfx(clip);
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

            // Health: Apply debuff always, buff fully if match, else 50%
            if (card.healthToAdd < 0) finalH = card.healthToAdd;
            else if (isMatch) finalH = card.healthToAdd;
            else finalH = Mathf.RoundToInt(card.healthToAdd * 0.5f);

            // Damage: Apply debuff always, buff fully if match, else 50%
            if (card.damageToAdd < 0) finalD = card.damageToAdd;
            else if (isMatch) finalD = card.damageToAdd;
            else finalD = Mathf.RoundToInt(card.damageToAdd * 0.5f);

            if (finalH != 0 || finalD != 0)
            {
                pawn.ApplyCardEffectServer(finalH, finalD);
            }

            // 2. Handle Runtime Buffs
            if (card.runtimeBuffs != null && card.runtimeBuffs.Count > 0)
            {
                List<BuffData> fullBuffs = new List<BuffData>();
                List<BuffData> halfBuffs = new List<BuffData>();

                foreach (var b in card.runtimeBuffs)
                {
                    if (b == null) continue;

                    // SPECIAL: Double Use only applies if it's a match
                    if (b.effectType == EffectType.DoubleUse)
                    {
                        if (isMatch) fullBuffs.Add(b);
                        continue;
                    }
                    
                    // Apply debuffs (negative effects) always fully. 
                    // Apply buffs (positive effects) fully if match, else 50%.
                    if (!b.isPositiveEffect || isMatch)
                    {
                        fullBuffs.Add(b);
                    }
                    else
                    {
                        halfBuffs.Add(b);
                    }
                }

                if (fullBuffs.Count > 0)
                {
                    pawn.ApplyRuntimeBuffsServer(fullBuffs, 1.0f);
                }
                if (halfBuffs.Count > 0)
                {
                    pawn.ApplyRuntimeBuffsServer(halfBuffs, 0.5f);
                }
            }
        }
    }
}

