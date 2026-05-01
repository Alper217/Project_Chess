using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;

namespace AlperKocasalih.Chess.Grid.Utils
{
    /// <summary>
    /// A powerful on-screen debug tool to test ALL 14+ Buff/Debuff types.
    /// Toggle with the 'F1' key.
    /// </summary>
    public class BuffDebugger : NetworkBehaviour
    {
        public static BuffDebugger Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool showUI = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private Vector2 scrollPos;
        private Pawn selectedPawn;
        private float testAmount = 5f;
        private int testDuration = 2;
        private bool testIsPercentage = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) showUI = !showUI;
        }

        private void OnGUI()
        {
            if (!showUI) return;

            GUI.Box(new Rect(10, 10, 450, 600), "BUFF/DEBUFF MASTER TESTER (F1)");

            if (!IsServer)
            {
                GUI.Label(new Rect(20, 40, 400, 30), "<color=red>HATA: Sadece HOST/SERVER üzerinden buff basılabilir.</color>");
                return;
            }

            // --- Left Column: Pawn List ---
            GUI.Label(new Rect(20, 40, 150, 20), "<b>Select Pawn:</b>");
            Pawn[] allPawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            
            float pawnY = 60;
            foreach (var p in allPawns)
            {
                if (p == null) continue;
                string label = $"[{p.PlayerID}] {p.PawnData.pawnName}";
                if (GUI.Button(new Rect(20, pawnY, 150, 25), label))
                {
                    selectedPawn = p;
                }
                pawnY += 30;
            }

            if (selectedPawn == null)
            {
                GUI.Label(new Rect(180, 60, 250, 20), "Please select a pawn from the left...");
                return;
            }

            // --- Right Column: Details & Controls ---
            float startX = 180;
            GUI.Label(new Rect(startX, 40, 250, 20), $"<b>Selected:</b> {selectedPawn.PawnData.pawnName}");
            GUI.Label(new Rect(startX, 60, 250, 20), $"HP: {selectedPawn.currentHealth.Value}/{selectedPawn.maxHealth.Value} | DMG: {selectedPawn.damage.Value}");

            // Parameter Settings
            GUI.Label(new Rect(startX, 90, 60, 20), "Amount:");
            string amtStr = GUI.TextField(new Rect(startX + 60, 90, 40, 20), testAmount.ToString());
            float.TryParse(amtStr, out testAmount);
            
            GUI.Label(new Rect(startX + 110, 90, 40, 20), "Turns:");
            string durStr = GUI.TextField(new Rect(startX + 150, 90, 40, 20), testDuration.ToString());
            int.TryParse(durStr, out testDuration);

            testIsPercentage = GUI.Toggle(new Rect(startX + 200, 90, 60, 20), testIsPercentage, " %");

            // --- Buff List (Scroll View) ---
            Rect viewRect = new Rect(startX, 120, 250, 400);
            Rect contentRect = new Rect(0, 0, 230, 700);
            scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect);

            float buttonY = 0;
            foreach (EffectType effect in Enum.GetValues(typeof(EffectType)))
            {
                if (effect == EffectType.None) continue;

                if (GUI.Button(new Rect(0, buttonY, 220, 25), $"Apply {effect}"))
                {
                    QuickApply(effect);
                }
                buttonY += 30;
            }

            // Special Controls (The "13th and 14th" logic types)
            GUI.color = Color.yellow;
            if (GUI.Button(new Rect(0, buttonY, 220, 25), "DEBUG: Move this Pawn (No Card)"))
            {
                if (PlayerInputController.Instance != null && selectedPawn != null)
                {
                    PlayerInputController.Instance.SelectMovementPattern(selectedPawn.PawnData.attackPattern);
                }
            }
            buttonY += 30;

            if (GUI.Button(new Rect(0, buttonY, 220, 25), "Toggle Force Attack Pattern"))
            {
                selectedPawn.ToggleForceAttackPattern();
            }
            buttonY += 30;

            if (GUI.Button(new Rect(0, buttonY, 220, 25), "Add Synergy (+10 HP/DMG)"))
            {
                selectedPawn.ApplyBuffsServer(10, 10);
            }
            buttonY += 30;
            GUI.color = Color.white;

            GUI.EndScrollView();

            // Bottom Buttons
            if (GUI.Button(new Rect(startX, 530, 120, 30), "RESET ALL"))
            {
                selectedPawn.ResetSynergyServer();
                selectedPawn.activeBuffs.Clear();
            }

            if (GUI.Button(new Rect(startX + 130, 530, 120, 30), "NEXT TURN"))
            {
                if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
            }
        }

        private void QuickApply(EffectType type)
        {
            if (selectedPawn == null) return;

            BuffData data = ScriptableObject.CreateInstance<BuffData>();
            data.buffName = "Debug " + type;
            data.effectType = type;
            data.amount = testAmount;
            data.durationTurns = testDuration;
            data.isPercentage = testIsPercentage;
            
            // Logic: Positive amount = Positive effect (Green UI), Negative = Debuff (Red UI)
            data.isPositiveEffect = testAmount >= 0;

            selectedPawn.ApplyRuntimeBuffsServer(new List<BuffData> { data });
            Debug.Log($"[BUFF DEBUGGER] Applied {type} (Amt:{testAmount}, Dur:{testDuration}, %:{testIsPercentage}) to {selectedPawn.PawnData.pawnName}");
        }
    }
}
