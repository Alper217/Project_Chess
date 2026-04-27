using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Server-only Bot AI Controller.
    /// Handles pawn selection (Setup), card decisions (Draft) and card playing (Action)
    /// for one bot player. Attach one instance per bot player in the scene.
    ///
    /// BUFF/DEBUFF AWARENESS:
    ///   - Buffs  → only apply when card.pawnClass matches the pawn's Type (class-specific).
    ///   - Debuffs → always apply regardless of pawn class.
    /// The bot evaluates every card with this rule to make Keep/Give/Burn decisions.
    /// </summary>
    public class BotAIController : NetworkBehaviour
    {
        // ───────────────────── Inspector ─────────────────────

        [Header("Bot Identity")]
        [Tooltip("Which player ID this bot controls (1 or 2).")]
        [SerializeField] private int botPlayerID = 2;

        [Header("Pawn Selection Strategy")]
        [Tooltip("Preferred pawn type order. If empty, selects by power score automatically.")]
        [SerializeField] private Type[] preferredPawnTypes;

        [Tooltip("Power score weight for Damage stat.")]
        [SerializeField] private float damageWeight = 2f;
        [Tooltip("Power score weight for MaxHealth stat.")]
        [SerializeField] private float healthWeight = 1f;

        [Header("Timing")]
        [Tooltip("Seconds the bot 'thinks' before each Draft action (feel).")]
        [SerializeField, Min(0f)] private float draftThinkDelay = 1.5f;
        [Tooltip("Seconds the bot 'thinks' before each Action phase move.")]
        [SerializeField, Min(0f)] private float actionThinkDelay = 2.0f;
        [Tooltip("Maximum turns before the game is forced to end by points.")]
        [SerializeField, Min(10)] private int maxTurnLimit = 100;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = true;

        // ───────────────────── Properties ─────────────────────

        /// <summary>Exposes the bot's player ID so BotMatchReporter can discover this instance.</summary>
        public int BotPlayerID => botPlayerID;

        // ───────────────────── Runtime State ─────────────────────

        private HashSet<Type> myPawnTypes = new HashSet<Type>();

        // Track total moves for the XML report
        public int TotalMoves { get; private set; }
        public List<PawnData> SelectedPawnDatas { get; private set; } = new List<PawnData>();

        private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();
        private bool isProcessingDraft = false;
        private bool isProcessingAction = false;

        // ───────────────────── Unity / Network ─────────────────────

        private void Awake()
        {
            TotalMoves = 0;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // Subscribe to state changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                // If we joined or spawned late (like in a test), handle current state immediately
                HandleStateChanged(GameManager.Instance.CurrentState);
            }

            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnCardsDrawn    += HandleCardsDrawn;
                DraftManager.Instance.OnDraftFinished += HandleDraftFinished;
            }

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleStateChanged;

            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.OnCardsDrawn    -= HandleCardsDrawn;
                DraftManager.Instance.OnDraftFinished -= HandleDraftFinished;
            }

            if (TurnManager.Instance != null)
                TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;

            base.OnNetworkDespawn();
        }

        // ───────────────────── State Handlers ─────────────────────

        private void HandleStateChanged(GameState newState)
        {
            if (!IsServer) return;

            switch (newState)
            {
                case GameState.Setup:
                    StartCoroutine(BotSetupRoutine());
                    break;
                case GameState.ActionPhase:
                    RefreshGridLookup();
                    CheckTurnLimit();
                    
                    // Eğer ActionPhase başladığında sıra zaten botta ise (örn. zarı bot kazandıysa)
                    // TurnChanged eventi daha önceden (RollDice sırasında) ateşlendiği için
                    // aksiyon rutini otomatik başlamaz. Burada manuel tetikliyoruz:
                    if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayerID == botPlayerID)
                    {
                        if (!isProcessingAction)
                        {
                            StartCoroutine(BotActionRoutine());
                        }
                    }
                    break;
            }
        }

        private void HandleCardsDrawn(int playerID, List<CardData> choices)
        {
            if (!IsServer) return;
            if (playerID != botPlayerID) return;
            if (isProcessingDraft) return;

            StartCoroutine(BotDraftRoutine(choices));
        }

        private void HandleDraftFinished()
        {
            isProcessingDraft = false;
        }

        private void HandleTurnChanged(int activePlayerID)
        {
            if (!IsServer) return;
            if (activePlayerID != botPlayerID) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase) return;
            if (isProcessingAction) return;

            StartCoroutine(BotActionRoutine());
        }

        // ═══════════════════════════════════════════════════════════
        //  PHASE 1 ── SETUP: Pawn Selection
        // ═══════════════════════════════════════════════════════════

        private IEnumerator BotSetupRoutine()
        {
            // Small delay to ensure the grid is ready
            yield return new WaitForSeconds(1.5f);

            if (PawnPlacementManager.Instance == null) yield break;

            RefreshGridLookup();

            int cap = PawnPlacementManager.Instance.GetPlacementCap();
            List<int> indicesToPlace = SelectBestPawnIndices(cap);

            if (verboseLog)
                Debug.Log($"[BotAI P{botPlayerID}] Setup: Placing {indicesToPlace.Count} pawns.");

            // Find valid bot placement cells
            List<HexCell> freeCells = GetBotPlacementCells();
            
            // Randomize the free cells so the bot doesn't always place pawns on the far right
            System.Random rng = new System.Random();
            int n = freeCells.Count;
            while (n > 1) 
            {
                n--;
                int k = rng.Next(n + 1);
                HexCell value = freeCells[k];
                freeCells[k] = freeCells[n];
                freeCells[n] = value;
            }

            SelectedPawnDatas.Clear();
            myPawnTypes.Clear();

            for (int i = 0; i < indicesToPlace.Count && i < freeCells.Count; i++)
            {
                int pawnIndex = indicesToPlace[i];
                HexCell cell  = freeCells[i];

                PawnPlacementManager.Instance.SpawnBotPawn(cell, pawnIndex, botPlayerID);

                // Record selected pawn data for reporting
                PawnData pd = PawnPlacementManager.Instance.GetPawnDataByIndex(pawnIndex);
                if (pd != null)
                {
                    SelectedPawnDatas.Add(pd);
                    myPawnTypes.Add(pd.type);
                }

                yield return new WaitForSeconds(0.6f);
            }

            yield return new WaitForSeconds(0.5f);

            // Confirm placement
            PawnPlacementManager.Instance.ConfirmPlayerPlacement(botPlayerID);

            if (verboseLog)
                Debug.Log($"[BotAI P{botPlayerID}] Setup confirmed. Pawn types: {string.Join(", ", myPawnTypes)}");
        }

        /// <summary>
        /// Returns the indices of pawn prefabs to place, based on preferred list or power score.
        /// </summary>
        private List<int> SelectBestPawnIndices(int cap)
        {
            List<int> result = new List<int>();

            if (preferredPawnTypes != null && preferredPawnTypes.Length > 0)
            {
                // Priority list mode: use preferred types in order
                List<PawnData> allDatas = PawnPlacementManager.Instance.GetAllPawnDatas();
                foreach (Type preferred in preferredPawnTypes)
                {
                    if (result.Count >= cap) break;
                    for (int i = 0; i < allDatas.Count; i++)
                    {
                        if (allDatas[i] != null && allDatas[i].type == preferred && !result.Contains(i))
                        {
                            result.Add(i);
                            break;
                        }
                    }
                }
            }

            // Fill remaining slots by power score
            if (result.Count < cap)
            {
                List<PawnData> allDatas = PawnPlacementManager.Instance.GetAllPawnDatas();
                List<(int index, float score)> scored = new List<(int, float)>();
                for (int i = 0; i < allDatas.Count; i++)
                {
                    if (allDatas[i] == null || result.Contains(i)) continue;
                    float score = allDatas[i].damage * damageWeight + allDatas[i].maxHealth * healthWeight;
                    scored.Add((i, score));
                }

                scored.Sort((a, b) => b.score.CompareTo(a.score)); // Descending

                foreach (var (idx, _) in scored)
                {
                    if (result.Count >= cap) break;
                    result.Add(idx);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns free HexCells in the bot's valid placement rows.
        /// P1 rows: 7-9, P2 rows: 0-2.
        /// </summary>
        private List<HexCell> GetBotPlacementCells()
        {
            List<HexCell> cells = new List<HexCell>();
            foreach (var kvp in gridLookup)
            {
                HexCell cell = kvp.Value;
                if (cell == null || cell.IsOccupied) continue;

                int row = kvp.Key.y;
                bool inRange = botPlayerID == 1
                    ? (row >= 7 && row <= 9)
                    : (row >= 0 && row <= 2);

                if (inRange) cells.Add(cell);
            }
            return cells;
        }

        // ═══════════════════════════════════════════════════════════
        //  PHASE 2 ── DRAFT: Card Decision
        // ═══════════════════════════════════════════════════════════

        private IEnumerator BotDraftRoutine(List<CardData> choices)
        {
            isProcessingDraft = true;

            // Refresh my pawn types in case setup changed them
            RefreshMyPawnTypes();

            // Evaluate each card and decide action
            // We must consume all 3 (Keep, Give, Burn) without repeating an action this round.
            HashSet<DraftAction> usedActions = new HashSet<DraftAction>();

            // Work on a snapshot of choices since the list mutates as we pick
            List<(int originalIndex, CardData card, float score)> evaluated = new List<(int, CardData, float)>();
            for (int i = 0; i < choices.Count; i++)
            {
                float score = EvaluateCardForSelf(choices[i]);
                evaluated.Add((i, choices[i], score));
            }

            // Sort by score descending — best card gets Keep, worst gets Burn
            evaluated.Sort((a, b) => b.score.CompareTo(a.score));

            // Assign actions: best → Keep, worst → Burn, middle → Give
            // (Only 3 cards, and each action can only be used once per round)
            DraftAction[] plan = AssignDraftActions(evaluated);

            // Execute decisions in original index order (DraftManager expects current index)
            // We need to account for shifting indices as cards are removed
            // So we re-evaluate the current choices state each step.
            for (int step = 0; step < evaluated.Count; step++)
            {
                yield return new WaitForSeconds(draftThinkDelay);

                if (DraftManager.Instance == null) break;

                // Refresh current choices from DraftManager
                List<CardData> currentChoices = DraftManager.Instance.GetCurrentChoices();
                if (currentChoices.Count == 0) break;

                // Find the target card in the current live choices list
                CardData targetCard = evaluated[step].card;
                DraftAction action  = plan[step];

                int liveIndex = FindCardInList(currentChoices, targetCard);
                if (liveIndex < 0)
                {
                    // Card no longer available (already consumed); pick first available with this action
                    liveIndex = 0;
                }

                if (verboseLog)
                {
                    Debug.Log($"[BotAI P{botPlayerID}] Draft: {action} on '{targetCard?.cardName}' " +
                              $"(score: {evaluated[step].score:F1})");
                }

                // If this is the last step, clear flag early so the synchronous event is caught
                if (step == evaluated.Count - 1)
                {
                    isProcessingDraft = false;
                }

                DraftManager.Instance.HandleChoice(liveIndex, action);
            }
            
            isProcessingDraft = false;
        }

        /// <summary>
        /// Evaluates a card's value FOR THE BOT (self).
        /// Buffs only count if pawnClass matches. Debuffs always count (negative).
        /// </summary>
        private float EvaluateCardForSelf(CardData card)
        {
            if (card == null) return 0f;

            bool classMatch = myPawnTypes.Contains(card.pawnClass) || card.pawnClass == Type.None;
            float score = 0f;

            if (card.runtimeBuffs != null)
            {
                foreach (var buff in card.runtimeBuffs)
                {
                    if (buff == null) continue;
                    float val = buff.isPercentage ? buff.amount * 0.5f : buff.amount;

                    if (buff.isPositiveEffect)
                    {
                        // Buff: only useful if class matches
                        if (classMatch) score += val;
                        // else: bot knows it won't receive this buff → score += 0
                    }
                    else
                    {
                        // Debuff ALWAYS applies to self if bot keeps → penalize
                        score -= Mathf.Abs(val);
                    }
                }
            }

            // Legacy stat contributions
            if (card.healthToAdd > 0 && classMatch) score += card.healthToAdd * 0.5f;
            else if (card.healthToAdd < 0) score += card.healthToAdd * 0.5f;

            if (card.damageToAdd > 0 && classMatch) score += card.damageToAdd;
            else if (card.damageToAdd < 0) score += card.damageToAdd;

            return score;
        }

        /// <summary>
        /// Evaluates a card's value FOR THE OPPONENT (when considering Give).
        /// Debuffs always apply → opponent suffers them → good for bot.
        /// Buffs only apply if opponent's pawns match → less predictable, assume partial.
        /// </summary>
        private float EvaluateCardAsGift(CardData card)
        {
            if (card == null) return 0f;

            float debuffPenaltyOnOpponent = 0f;

            if (card.runtimeBuffs != null)
            {
                foreach (var buff in card.runtimeBuffs)
                {
                    if (buff == null) continue;
                    float val = buff.isPercentage ? buff.amount * 0.5f : buff.amount;

                    if (!buff.isPositiveEffect)
                    {
                        // Debuff always hits opponent → positive for bot
                        debuffPenaltyOnOpponent += Mathf.Abs(val);
                    }
                    // Buff on opponent: we don't know their classes exactly but assume 50% chance
                    else
                    {
                        debuffPenaltyOnOpponent -= val * 0.5f;
                    }
                }
            }

            return debuffPenaltyOnOpponent;
        }

        /// <summary>
        /// Assigns Keep/Give/Burn to each evaluated card.
        /// Rules:
        ///   - Each action can be used only ONCE per round.
        ///   - Best card for self → Keep.
        ///   - Best card to punish opponent with → Give (via debuff).
        ///   - Remaining card → Burn.
        /// </summary>
        private DraftAction[] AssignDraftActions(List<(int originalIndex, CardData card, float score)> sorted)
        {
            // sorted[0] = highest self-score, sorted[2] = lowest
            DraftAction[] result = new DraftAction[sorted.Count];
            bool keepAssigned = false;
            bool giveAssigned = false;
            bool burnAssigned = false;

            // Step 1: Best card for self → Keep
            result[0] = DraftAction.Keep;
            keepAssigned = true;

            if (sorted.Count >= 3)
            {
                // Step 2: Of the remaining two, which is better to Give (hurts opponent more)?
                float giftValue1 = EvaluateCardAsGift(sorted[1].card);
                float giftValue2 = EvaluateCardAsGift(sorted[2].card);

                if (giftValue1 >= giftValue2)
                {
                    result[1] = DraftAction.Give;
                    result[2] = DraftAction.Burn;
                }
                else
                {
                    result[1] = DraftAction.Burn;
                    result[2] = DraftAction.Give;
                }
                giveAssigned = true;
                burnAssigned = true;
            }
            else if (sorted.Count == 2)
            {
                // Only 2 cards left — check if giving is beneficial
                float giftVal = EvaluateCardAsGift(sorted[1].card);
                result[1] = giftVal > 0 ? DraftAction.Give : DraftAction.Burn;
            }

            return result;
        }

        private int FindCardInList(List<CardData> list, CardData target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return i;
            }
            return -1;
        }

        // ═══════════════════════════════════════════════════════════
        //  PHASE 3 ── ACTION: Play a Card & Move
        // ═══════════════════════════════════════════════════════════

        private IEnumerator BotActionRoutine()
        {
            isProcessingAction = true;

            yield return new WaitForSeconds(actionThinkDelay);

            if (DraftManager.Instance == null || Core.PawnActionExecutor.Instance == null)
            {
                isProcessingAction = false;
                yield break;
            }

            List<CardData> hand = DraftManager.Instance.GetHand(botPlayerID);
            if (hand == null || hand.Count == 0)
            {
                // Empty hand → draw a card and skip turn
                if (DraftManager.Instance.IsDrawAllowed)
                    DraftManager.Instance.PerformDrawOneAndSkipTurn(botPlayerID);
                else if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
                    
                isProcessingAction = false;
                yield break;
            }

            // Get all bot pawns
            List<Pawn> myPawns  = GetPawnsForPlayer(botPlayerID);
            List<Pawn> enemies  = GetPawnsForPlayer(botPlayerID == 1 ? 2 : 1);

            if (myPawns.Count == 0)
            {
                isProcessingAction = false;
                yield break;
            }

            // ── Pick best card to play ──
            CardData bestCard   = null;
            Pawn     bestPawn   = null;
            HexCell  bestTarget = null;
            bool     isAttack   = false;

            float bestScore = float.NegativeInfinity;

            foreach (CardData card in hand)
            {
                if (card == null) continue;
                bool isObstacle = card.isObstacleCard;

                foreach (Pawn pawn in myPawns)
                {
                    if (pawn == null || pawn.HasStun()) continue;
                    if (pawn.OccupiedCell == null) continue;

                    // Resolve pattern
                    bool classMatch  = pawn.PawnData != null && pawn.PawnData.type == card.pawnClass;
                    MovementPattern movePattern = classMatch ? card.pattern : card.mismatchPattern;

                    if (!isObstacle && movePattern != null)
                    {
                        // Evaluate attack options
                        (HexCell attackCell, float attackScore) = FindBestAttackTarget(pawn, pawn.PawnData?.attackPattern, enemies);
                        if (attackCell != null && attackScore > bestScore)
                        {
                            bestScore  = attackScore;
                            bestCard   = card;
                            bestPawn   = pawn;
                            bestTarget = attackCell;
                            isAttack   = true;
                        }

                        // Evaluate move options
                        (HexCell moveCell, float moveScore) = FindBestMoveTarget(pawn, movePattern, enemies);
                        if (moveCell != null && moveScore > bestScore)
                        {
                            bestScore  = moveScore;
                            bestCard   = card;
                            bestPawn   = pawn;
                            bestTarget = moveCell;
                            isAttack   = false;
                        }
                    }
                }
            }

            // Execute the best found action
            if (bestCard != null && bestPawn != null && bestTarget != null)
            {
                int cardIndex = DeckManager.Instance != null
                    ? DeckManager.Instance.GetCardIndex(bestCard)
                    : -1;

                if (cardIndex >= 0)
                    Core.PawnActionExecutor.Instance.ApplyCardEffectServerRpc(bestPawn.NetworkObjectId, cardIndex);

                if (isAttack)
                {
                    // Find the enemy pawn on that cell
                    Pawn enemy = FindPawnOnCell(bestTarget);
                    if (enemy != null)
                    {
                        Core.PawnActionExecutor.Instance.ExecuteCombatServerRpc(
                            bestPawn.NetworkObjectId, enemy.NetworkObjectId, bestTarget.Coordinates, true);
                    }
                }
                else
                {
                    Core.PawnActionExecutor.Instance.ExecuteMoveServerRpc(
                        bestPawn.NetworkObjectId, bestTarget.Coordinates, true);
                }

                DraftManager.Instance.RemoveCardFromHand(botPlayerID, bestCard);
                TotalMoves++;

                if (verboseLog)
                {
                    string actionDesc = isAttack ? "ATTACK" : "MOVE";
                    Debug.Log($"[BotAI P{botPlayerID}] Action: {actionDesc} with '{bestCard.cardName}' " +
                              $"| Pawn: {bestPawn.PawnData?.pawnName} → {bestTarget.Coordinates}");
                }
            }
            else
            {
                // No playable card found → draw and skip
                if (verboseLog)
                    Debug.Log($"[BotAI P{botPlayerID}] No valid action found. Drawing & skipping turn.");
                    
                if (DraftManager.Instance.IsDrawAllowed)
                    DraftManager.Instance.PerformDrawOneAndSkipTurn(botPlayerID);
                else if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
            }

            isProcessingAction = false;
        }

        // ───────────────────── Move/Attack Evaluation ─────────────────────

        /// <summary>
        /// Finds the best attack target cell using the pawn's attack pattern.
        /// Returns (cell, score) — score is the defender's remaining HP (lower = better target).
        /// </summary>
        private (HexCell, float) FindBestAttackTarget(Pawn attacker, MovementPattern attackPattern, List<Pawn> enemies)
        {
            if (attacker == null || attackPattern == null || attacker.OccupiedCell == null)
                return (null, float.NegativeInfinity);

            Vector2Int origin = attacker.OccupiedCell.Coordinates;
            bool isP2 = attacker.PlayerID == 2;
            List<Vector2Int> offsets = attackPattern.GetValidOffsets(origin, isP2);

            HexCell bestCell  = null;
            float   bestScore = float.NegativeInfinity;

            foreach (var offset in offsets)
            {
                Vector2Int targetCoords = origin + offset;
                if (!gridLookup.TryGetValue(targetCoords, out HexCell cell)) continue;

                Pawn occupant = FindPawnOnCell(cell);
                if (occupant == null || occupant.PlayerID == attacker.PlayerID) continue;

                // Prefer low-health targets (easier to eliminate)
                float score = -occupant.currentHealth.Value;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell  = cell;
                }
            }

            return (bestCell, bestScore);
        }

        /// <summary>
        /// Finds the best movement target — prefers cells that get the pawn closer to enemies.
        /// </summary>
        private (HexCell, float) FindBestMoveTarget(Pawn pawn, MovementPattern pattern, List<Pawn> enemies)
        {
            if (pawn == null || pattern == null || pawn.OccupiedCell == null)
                return (null, float.NegativeInfinity);

            Vector2Int origin = pawn.OccupiedCell.Coordinates;
            bool isP2 = pawn.PlayerID == 2;

            int rangeMod = pawn.GetMovementRangeModifier();
            List<Vector2Int> offsets = pattern.GetValidOffsets(origin, isP2, rangeMod);

            HexCell bestCell  = null;
            float   bestScore = float.NegativeInfinity;

            float currentMinDist = MinDistanceToEnemies(origin, enemies);

            foreach (var offset in offsets)
            {
                Vector2Int targetCoords = origin + offset;
                if (!gridLookup.TryGetValue(targetCoords, out HexCell cell)) continue;
                if (cell.IsOccupied || cell.IsObstacle) continue;

                // Score = reduction in distance to nearest enemy
                float newDist = MinDistanceToEnemies(targetCoords, enemies);
                float score   = currentMinDist - newDist; // Positive = getting closer

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell  = cell;
                }
            }

            return (bestCell, bestScore);
        }

        private float MinDistanceToEnemies(Vector2Int from, List<Pawn> enemies)
        {
            float min = float.MaxValue;
            Vector3Int fromCube = Utils.HexGridMath.OffsetToCube(from);
            foreach (var e in enemies)
            {
                if (e == null || e.OccupiedCell == null) continue;
                Vector3Int eCube = Utils.HexGridMath.OffsetToCube(e.OccupiedCell.Coordinates);
                float d = Utils.HexGridMath.CubeDistance(fromCube, eCube);
                if (d < min) min = d;
            }
            return min;
        }

        // ───────────────────── Turn Limit ─────────────────────

        private void CheckTurnLimit()
        {
            if (!IsServer) return;
            if (TurnManager.Instance == null || GameManager.Instance == null) return;

            if (TurnManager.Instance.TurnCount >= maxTurnLimit)
            {
                Debug.Log($"[BotAI] Turn limit ({maxTurnLimit}) reached. Deciding winner by points.");
                GameManager.Instance.CheckWinConditionPoints();
            }
        }

        // ───────────────────── Helpers ─────────────────────

        private void RefreshMyPawnTypes()
        {
            myPawnTypes.Clear();
            List<Pawn> pawns = GetPawnsForPlayer(botPlayerID);
            foreach (var p in pawns)
            {
                if (p?.PawnData != null)
                    myPawnTypes.Add(p.PawnData.type);
            }

            // Also use SelectedPawnDatas as fallback
            foreach (var pd in SelectedPawnDatas)
            {
                if (pd != null) myPawnTypes.Add(pd.type);
            }
        }

        private void RefreshGridLookup()
        {
            gridLookup.Clear();
            if (GridGenerator.Instance == null) return;
            foreach (var hex in GridGenerator.Instance.SpawnedHexes)
            {
                HexCell cell = hex?.GetComponent<HexCell>();
                if (cell != null) gridLookup[cell.Coordinates] = cell;
            }
        }

        private List<Pawn> GetPawnsForPlayer(int playerID)
        {
            List<Pawn> result = new List<Pawn>();
            Pawn[] all = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            foreach (var p in all)
            {
                if (p != null && p.IsSpawned && p.PlayerID == playerID)
                    result.Add(p);
            }
            return result;
        }

        private Pawn FindPawnOnCell(HexCell cell)
        {
            Pawn[] all = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            foreach (var p in all)
            {
                if (p != null && p.OccupiedCell == cell) return p;
            }
            return null;
        }
    }
}
