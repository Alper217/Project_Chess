using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;

namespace AlperKocasalih.Chess.Grid.Utils
{
    /// <summary>
    /// A simple on-screen debug tool to test the Buff/Debuff system.
    /// Toggle with the 'F1' key.
    /// </summary>
    public class BuffDebugger : NetworkBehaviour
    {
        public static BuffDebugger Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool showUI = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        [Header("Test Buffs (Assign in Inspector or use Defaults)")]
        public BuffData healthBuff;
        public BuffData damageDebuff;
        public BuffData stunDebuff;
        public BuffData permanentAuraBuff;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showUI = !showUI;
            }
        }

        private void OnGUI()
        {
            if (!showUI) return;

            GUI.Box(new Rect(10, 10, 300, 450), "Buff/Debuff Test System (Server Only)");

            if (!IsServer)
            {
                GUI.Label(new Rect(20, 40, 260, 40), "ERROR: You must be the HOST/SERVER to apply buffs via this tool.");
                return;
            }

            int activePID = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayerID : 1;
            GUI.Label(new Rect(20, 40, 260, 20), $"Active Player ID: {activePID}");

            Pawn[] allPawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            List<Pawn> myPawns = new List<Pawn>();
            foreach (var p in allPawns)
            {
                if (p.PlayerID == activePID) myPawns.Add(p);
            }

            if (myPawns.Count == 0)
            {
                GUI.Label(new Rect(20, 70, 260, 20), "No pawns found for active player.");
                return;
            }

            float y = 70;
            foreach (var p in myPawns)
            {
                GUI.Label(new Rect(20, y, 260, 20), $"[{p.PawnData.pawnName}] HP:{p.currentHealth.Value}");
                y += 25;

                if (GUI.Button(new Rect(20, y, 85, 20), "+HP (Inst)")) ApplyBuff(p, "HealthInst");
                if (GUI.Button(new Rect(110, y, 85, 20), "-DMG (2T)")) ApplyBuff(p, "DamageTimed");
                if (GUI.Button(new Rect(200, y, 85, 20), "Stun (1T)")) ApplyBuff(p, "StunTimed");
                
                y += 25;
                if (GUI.Button(new Rect(20, y, 130, 20), "+Range (Perm)")) ApplyBuff(p, "RangePerm");
                if (GUI.Button(new Rect(155, y, 130, 20), "Clear (Synergy)") ) p.ResetSynergyServer();

                y += 35;
                if (y > 400) break; // Simple overflow protection
            }

            if (GUI.Button(new Rect(10, 420, 280, 20), "Skip Turn (Test Expiry)"))
            {
                if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
            }
        }

        private void ApplyBuff(Pawn p, string type)
        {
            if (!IsServer) return;

            BuffData data = ScriptableObject.CreateInstance<BuffData>();
            data.isPercentage = false;

            switch (type)
            {
                case "HealthInst":
                    data.buffName = "Instant Heal";
                    data.effectType = EffectType.CurrentHealth;
                    data.amount = 10;
                    data.durationTurns = 0;
                    data.isPositiveEffect = true;
                    break;
                case "DamageTimed":
                    data.buffName = "Damage Weakness";
                    data.effectType = EffectType.OutgoingDamageModifier;
                    data.amount = -5; // -5 flat damage? Code uses it as multiplier if percentage, but let's see.
                    data.isPercentage = false;
                    data.durationTurns = 2;
                    data.isPositiveEffect = false;
                    break;
                case "StunTimed":
                    data.buffName = "Freeze";
                    data.effectType = EffectType.Stun;
                    data.durationTurns = 1;
                    data.isPositiveEffect = false;
                    break;
                case "RangePerm":
                    data.buffName = "Swift Foot";
                    data.effectType = EffectType.MovementRangeModifier;
                    data.amount = 1;
                    data.durationTurns = 0; // Permanent!
                    data.isPositiveEffect = true;
                    break;
            }

            p.ApplyRuntimeBuffsServer(new List<BuffData> { data });
            Debug.Log($"Applied {data.buffName} to {p.PawnData.pawnName}");
        }
    }
}
