using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid;
using System.Linq;

namespace Tests.PlayMode
{
    public class BotVsBotIntegrationTest
    {
        private bool _isSceneLoaded = false;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 1. Load MainMenu to get NetworkManager
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            _isSceneLoaded = false;
            SceneManager.sceneLoaded += OnSceneLoaded;

            float timeout = 10f;
            while (!_isSceneLoaded && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!_isSceneLoaded)
            {
                Assert.Fail("MainMenu scene could not be loaded in time!");
            }

            // 2. Start NetworkManager as Host
            if (NetworkManager.Singleton != null)
            {
                // Disable Relay for local test
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData("127.0.0.1", 7777);
                }

                NetworkManager.Singleton.StartHost();
            }
            else
            {
                Assert.Fail("NetworkManager instance not found in MainMenu scene!");
            }

            // Wait for Network ready
            yield return new WaitUntil(() => NetworkManager.Singleton.IsListening);
            
            // 3. Use NetworkManager to load GameScene
            _isSceneLoaded = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

            timeout = 10f;
            while (!_isSceneLoaded && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!_isSceneLoaded)
            {
                Assert.Fail("GameScene could not be loaded in time!");
            }

            // Wait for Managers to initialize
            yield return new WaitForSeconds(1f);
            
            CleanUpAudioListeners();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" || scene.name == "GameScene")
            {
                _isSceneLoaded = true;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void CleanUpAudioListeners()
        {
            // Disable extra audio listeners to avoid warnings
            AudioListener[] listeners = GameObject.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners.Length > 1)
            {
                Debug.Log($"Cleaning up {listeners.Length - 1} extra audio listeners.");
                for (int i = 1; i < listeners.Length; i++)
                {
                    listeners[i].enabled = false;
                }
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullGameFlow_BotVsBot()
        {
            // --- SETUP PHASE ---
            Debug.Log("--- STARTING BOT VS BOT TEST: SETUP PHASE ---");
            
            Assert.AreEqual(GameState.Setup, GameManager.Instance.CurrentState, "Game should start in Setup state.");

            // 1. Place 3 pawns for P1 (Bot 1)
            // Rows 7-9 are valid for P1
            List<HexCell> p1Cells = FindCellsInRows(new int[] { 7, 8, 9 }).Take(3).ToList();
            Assert.AreEqual(3, p1Cells.Count, "Could not find 3 valid cells for P1.");

            // Set maxPawns to 3 for this test
            typeof(PawnPlacementManager)
                .GetField("maxPawns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(PawnPlacementManager.Instance, 3);
            // Actually let's use the public methods of PawnPlacementManager
            
            for (int i = 0; i < 3; i++)
            {
                // Simulate placing a pawn (type i) on cell i
                // Use the ServerRpc directly or the internal method since we are server
                typeof(PawnPlacementManager)
                    .GetMethod("SpawnPawnOnServer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(PawnPlacementManager.Instance, new object[] { p1Cells[i], i, 1 });
                yield return new WaitForSeconds(0.2f);
            }

            // 2. Place 3 pawns for P2 (Bot 2)
            // Rows 0-2 are valid for P2
            List<HexCell> p2Cells = FindCellsInRows(new int[] { 0, 1, 2 }).Take(3).ToList();
            Assert.AreEqual(3, p2Cells.Count, "Could not find 3 valid cells for P2.");

            for (int i = 0; i < 3; i++)
            {
                typeof(PawnPlacementManager)
                    .GetMethod("SpawnPawnOnServer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(PawnPlacementManager.Instance, new object[] { p2Cells[i], i, 2 });
                yield return new WaitForSeconds(0.2f);
            }

            // 3. Confirm Ready
            PawnPlacementManager.Instance.ConfirmPlayerPlacementServerRpc(1);
            PawnPlacementManager.Instance.ConfirmPlayerPlacementServerRpc(2);

            yield return new WaitUntil(() => GameManager.Instance.CurrentState != GameState.Setup);
            Debug.Log($"State after setup: {GameManager.Instance.CurrentState}");

            // Wait for DraftPhase to start
            if (GameManager.Instance.CurrentState == GameState.RollDice)
            {
                yield return new WaitUntil(() => GameManager.Instance.CurrentState == GameState.DraftPhase);
            }
            
            Assert.AreEqual(GameState.DraftPhase, GameManager.Instance.CurrentState, "Game should enter DraftPhase.");

            // --- DRAFT PHASE ---
            Debug.Log("--- STARTING BOT VS BOT TEST: DRAFT PHASE ---");

            // Total 6 drafting turns (3 for each player)
            for (int turn = 0; turn < 6; turn++)
            {
                yield return new WaitUntil(() => DraftManager.Instance.IsDraftingActive);
                int activePlayer = DraftManager.Instance.DraftingPlayerID;
                Debug.Log($"Drafting Turn {turn + 1}/6 for Player {activePlayer}");

                // Wait for cards to be drawn (synced)
                yield return new WaitForSeconds(0.5f);
                List<CardData> choices = DraftManager.Instance.GetCurrentChoices();
                Assert.AreEqual(3, choices.Count, "Expected 3 cards in draft choices.");

                // Apply "Give", "Burn", "Take"
                // Order: 0 -> Give, 1 -> Burn, 2 -> Take (Keep)
                // Note: HandleChoice uses index and enum
                DraftManager.Instance.HandleChoiceServerRpc(0, DraftAction.Give);
                yield return new WaitForSeconds(0.1f);
                DraftManager.Instance.HandleChoiceServerRpc(0, DraftAction.Burn); // Index 0 again because previous 0 was removed
                yield return new WaitForSeconds(0.1f);
                DraftManager.Instance.HandleChoiceServerRpc(0, DraftAction.Keep);
                
                yield return new WaitForSeconds(0.5f);
            }

            yield return new WaitUntil(() => GameManager.Instance.CurrentState == GameState.ActionPhase);
            Assert.AreEqual(GameState.ActionPhase, GameManager.Instance.CurrentState, "Game should enter ActionPhase.");

            // --- ACTION / FULL GAME LOOP ---
            Debug.Log("--- STARTING BOT VS BOT TEST: FULL GAME LOOP ---");

            // ENSURE MANAGERS ARE READY
            if (AuraManager.instance == null)
            {
                AuraManager am = Object.FindFirstObjectByType<AuraManager>();
                if (am != null) AuraManager.instance = am;
            }

            // Ensure Grid Lookup is initialized for ActionExecutor
            {
                HexCell[] allCells = GameObject.FindObjectsByType<HexCell>(FindObjectsSortMode.None);
                Dictionary<Vector2Int, HexCell> cellLookup = allCells.ToDictionary(c => c.Coordinates, c => c);
                AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.InitializeGridReference(cellLookup);
            }

            int safetyCounter = 0;
            int maxSafetyIterations = 1000; // Increased limit for long games

            int lastTurnCount = -1;
            int stagnantTurns = 0;

            while (GameManager.Instance.CurrentState != GameState.EndGame && safetyCounter < maxSafetyIterations)
            {
                safetyCounter++;
                
                // 1. Handle Overflow Burn (Highest Priority)
                if (DraftManager.Instance.IsOverflowBurnPending)
                {
                    int burnPlayer = (int)typeof(DraftManager).GetField("pendingOverflowBurnPlayerID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(DraftManager.Instance);
                    Debug.Log($"BotVsBot: Player {burnPlayer} is burning overflow cards.");
                    // Just burn the first available card that isn't locked
                    List<CardData> hand = DraftManager.Instance.GetHand(burnPlayer);
                    int burnIndex = -1;
                    for (int i = 0; i < hand.Count; i++)
                    {
                        if (!DraftManager.Instance.IsBurnLocked(burnPlayer, i))
                        {
                            burnIndex = i;
                            break;
                        }
                    }

                    if (burnIndex != -1)
                    {
                        DraftManager.Instance.BurnOverflowCardAtIndexServerRpc(burnPlayer, burnIndex);
                    }
                    else
                    {
                        Debug.LogWarning("BotVsBot: All cards in hand are locked! This might cause a stall.");
                        // To avoid stall, we might need a workaround or just wait
                    }
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                // 2. Handle Draft Phase (3-card choice or 1-card action draft)
                if (DraftManager.Instance.IsDraftingActive)
                {
                    int draftingPlayer = DraftManager.Instance.DraftingPlayerID;
                    List<CardData> choices = DraftManager.Instance.GetCurrentChoices();
                    if (choices != null && choices.Count > 0)
                    {
                        bool isAction = DraftManager.Instance.IsActionDraftActive;
                        Debug.Log($"BotVsBot [DEBUG]: Drafting card. Choices: {choices.Count}, Player: {draftingPlayer}, IsActionDraft: {isAction}");
                        
                        if (isAction)
                        {
                            DraftManager.Instance.HandleChoiceServerRpc(0, DraftAction.Keep);
                        }
                        else
                        {
                            DraftAction actionToTake = (choices.Count == 3) ? DraftAction.Give : (choices.Count == 2 ? DraftAction.Burn : DraftAction.Keep);
                            Debug.Log($"BotVsBot [DEBUG]: Full DraftPhase choice: {actionToTake}");
                            DraftManager.Instance.HandleChoiceServerRpc(0, actionToTake);
                        }
                    }
                    else if (GameManager.Instance.CurrentState != GameState.EndGame)
                    {
                        // Fallback if drafting is active but no cards found (e.g. deck empty resolve)
                        // Manual cleanup of draft state to prevent freeze, but ONLY if game hasn't ended
                        typeof(DraftManager).GetMethod("FinishDraft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(DraftManager.Instance, null);
                    }
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                // 3. Handle Action Phase Turn
                if (GameManager.Instance.CurrentState == GameState.ActionPhase)
                {
                    int activePlayer = TurnManager.Instance.ActivePlayerID;
                    
                    if (TurnManager.Instance.TurnCount != lastTurnCount)
                    {
                        lastTurnCount = TurnManager.Instance.TurnCount;
                        stagnantTurns = 0;
                        Debug.Log($"--- Turn {lastTurnCount}: Player {activePlayer}'s turn ---");
                    }
                    else
                    {
                        stagnantTurns++;
                    }

                    if (stagnantTurns > 5)
                    {
                        Debug.LogWarning("BotVsBot: Stagnant turn detected. Forcing turn skip.");
                        TurnManager.Instance.NextTurn();
                        yield return new WaitForSeconds(1.0f);
                        continue;
                    }

                    yield return RunBotAction(activePlayer);
                }

                yield return new WaitForSeconds(0.5f);
            }

            if (safetyCounter >= maxSafetyIterations)
            {
                Debug.LogError("BotVsBot: Test reached safety limit! Possible infinite loop or stall.");
            }
            else
            {
                Debug.Log($"--- BOT VS BOT TEST COMPLETED SUCCESSFULLY IN {safetyCounter} STEPS ---");
                Debug.Log($"Final Scores - P1: {GameManager.Instance.player1Score.Value}, P2: {GameManager.Instance.player2Score.Value}");
            }
        }

        private IEnumerator RunBotAction(int playerID)
        {
            List<CardData> hand = DraftManager.Instance.GetHand(playerID);
            bool isDrawAllowed = !IsDrawLocked();

            // Cache data once per turn
            HexCell[] allCells = GameObject.FindObjectsByType<HexCell>(FindObjectsSortMode.None);
            Dictionary<Vector2Int, HexCell> gridLookup = allCells.ToDictionary(c => c.Coordinates, c => c);
            Pawn[] allPawns = GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            List<Pawn> enemyPawns = allPawns.Where(p => p.PlayerID != playerID).ToList();
            List<Pawn> myPawns = allPawns.Where(p => p.PlayerID == playerID).ToList();

            // DIAGNOSTICS: Log current state
            string myPawnPositions = string.Join(", ", myPawns.Select(p => $"{p.PawnData?.pawnName}@{p.OccupiedCell?.Coordinates}"));
            Debug.Log($"BotVsBot: [Turn {TurnManager.Instance.TurnCount}] Player {playerID} processing. Hand: {hand.Count}. MyPawns: {myPawnPositions}");

            // AuraManager Check
            if (AuraManager.instance == null)
            {
                AuraManager am = Object.FindFirstObjectByType<AuraManager>();
                if (am != null) AuraManager.instance = am;
            }

            // 1. Evaluate Best Tactical Action (FIRST PRIORITY) - TACTICS BEFORE DRAWING
            BotAction bestAction = EvaluateBestAction(playerID, hand, myPawns, enemyPawns, gridLookup);

            if (bestAction != null && bestAction.score > 0)
            {
                Debug.Log($"BotVsBot: Player {playerID} CHOSE: {bestAction.pawn.PawnData.pawnName} via {bestAction.card.cardName} to {bestAction.target} (Score: {bestAction.score})");
                
                int cardIdxInDeck = AlperKocasalih.Chess.Grid.DeckManager.Instance.GetCardIndex(bestAction.card);
                AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.ApplyCardEffectServerRpc(bestAction.pawn.NetworkObjectId, cardIdxInDeck);
                
                if (bestAction.isAttack)
                    AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.ExecuteCombatServerRpc(bestAction.pawn.NetworkObjectId, bestAction.targetPawn.NetworkObjectId, bestAction.target);
                else
                    AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.ExecuteMoveServerRpc(bestAction.pawn.NetworkObjectId, bestAction.target);

                DraftManager.Instance.RemoveCardFromHand(playerID, bestAction.card);
                yield break; 
            }

            // 2. Strategic Drawing Check (ONLY if no good tactical move found)
            if (isDrawAllowed && (hand.Count < 6))
            {
                Debug.Log($"BotVsBot: Player {playerID} found no ideal move and hand is {hand.Count}. Strategic drawing.");
                DraftManager.Instance.PerformDrawOneAndSkipTurn(playerID);
                yield return new WaitForSeconds(1.0f);
                yield break;
            }

            // 3. Desperation Pass: Find ANY valid move (even if score is low) to prevent stall
            Debug.Log($"BotVsBot: Player {playerID} searching for desperation move...");
            bestAction = EvaluateBestAction(playerID, hand, myPawns, enemyPawns, gridLookup, true);
            if (bestAction != null)
            {
                Debug.Log($"BotVsBot: [DESPERATION] Player {playerID} moving to {bestAction.target}");
                int cardIdx = AlperKocasalih.Chess.Grid.DeckManager.Instance.GetCardIndex(bestAction.card);
                AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.ApplyCardEffectServerRpc(bestAction.pawn.NetworkObjectId, cardIdx);
                AlperKocasalih.Chess.Grid.Core.PawnActionExecutor.Instance.ExecuteMoveServerRpc(bestAction.pawn.NetworkObjectId, bestAction.target);
                DraftManager.Instance.RemoveCardFromHand(playerID, bestAction.card);
                yield break;
            }

            // 4. Final Fallback: Skip turn if absolutely no action possible
            if (isDrawAllowed && hand.Count < 6)
            {
                 Debug.Log($"BotVsBot: Player {playerID} STALL fallback. Drawing.");
                 DraftManager.Instance.PerformDrawOneAndSkipTurn(playerID);
            }
            else
            {
                Debug.Log($"BotVsBot: Player {playerID} STOLL fallback. Skipping turn.");
                TurnManager.Instance.NextTurn();
            }
        }

        private class BotAction
        {
            public Pawn pawn;
            public CardData card;
            public Vector2Int target;
            public Pawn targetPawn;
            public float score;
            public bool isAttack;
        }

        private BotAction EvaluateBestAction(int playerID, List<CardData> hand, List<Pawn> myPawns, List<Pawn> enemyPawns, Dictionary<Vector2Int, HexCell> gridLookup, bool desperationMode = false)
        {
            BotAction best = null;
            float maxScore = -1000f;

            foreach (var card in hand)
            {
                foreach (var pawn in myPawns)
                {
                    if (pawn.HasStun()) continue;

                    // Support Mismatch Patterns
                    MovementPattern pattern = (pawn.PawnData.type == card.pawnClass) ? card.pattern : card.mismatchPattern;
                    if (pattern == null) pattern = card.pattern;
                    if (pattern == null) continue;

                    int rangeMod = pawn.GetMovementRangeModifier();
                    // ROTATION LOGIC: P1 (rows 7-9) rotates to move UP (-r). P2 (rows 0-2) moves DOWN (+r) naturally.
                    bool shouldRotate = (playerID == 1); 
                    
                    List<Vector2Int> offsets = pattern.GetValidOffsets(pawn.OccupiedCell.Coordinates, shouldRotate, rangeMod);
                    if (offsets == null) continue;

                    foreach (var offset in offsets)
                    {
                        Vector2Int targetPos = pawn.OccupiedCell.Coordinates + offset;
                        
                        // In desperation mode, we ignore path blocking to increase move probability
                        if (!desperationMode && IsPathBlockedOptimized(pawn.OccupiedCell.Coordinates, targetPos, gridLookup)) continue;

                        if (!gridLookup.TryGetValue(targetPos, out HexCell targetCell)) continue;
                        if (targetCell.IsObstacle) continue;

                        Pawn occupant = FindPawnOptimized(targetCell, myPawns, enemyPawns);
                        float score = 0;
                        bool isAttack = false;

                        if (occupant != null)
                        {
                            if (occupant.PlayerID != playerID)
                            {
                                AttackHandler attackHandler = pawn.GetComponent<AttackHandler>();
                                if (attackHandler != null && attackHandler.CanAttack())
                                {
                                    isAttack = true;
                                    score = 500 + occupant.PawnData.pointValue;
                                    if (occupant.currentHealth.Value <= (pawn.damage.Value + card.damageToAdd)) score += 10000;
                                }
                                else continue;
                            }
                            else continue; 
                        }
                        else
                        {
                            score = 20; 
                            Pawn nearest = GetNearestEnemy(targetPos, enemyPawns);
                            if (nearest != null)
                            {
                                int currentDist = GetDistance(pawn.OccupiedCell.Coordinates, nearest.OccupiedCell.Coordinates);
                                int newDist = GetDistance(targetPos, nearest.OccupiedCell.Coordinates);
                                if (newDist < currentDist) score += (currentDist - newDist) * 100;
                            }
                        }

                        if (desperationMode) score = 1; 

                        if (score > maxScore)
                        {
                            maxScore = score;
                            best = new BotAction { pawn = pawn, card = card, target = targetPos, targetPawn = occupant, score = score, isAttack = isAttack };
                        }
                    }
                }
            }
            return best;
        }

        private Pawn GetNearestEnemy(Vector2Int from, List<Pawn> enemies)
        {
            Pawn nearest = null;
            int minDist = int.MaxValue;
            foreach (var e in enemies)
            {
                int d = GetDistance(from, e.OccupiedCell.Coordinates);
                if (d < minDist) { minDist = d; nearest = e; }
            }
            return nearest;
        }

        private int GetDistance(Vector2Int a, Vector2Int b)
        {
            Vector3Int aCube = AlperKocasalih.Chess.Grid.Utils.HexGridMath.OffsetToCube(a);
            Vector3Int bCube = AlperKocasalih.Chess.Grid.Utils.HexGridMath.OffsetToCube(b);
            return AlperKocasalih.Chess.Grid.Utils.HexGridMath.CubeDistance(aCube, bCube);
        }

        private HexCell FindCell(Vector2Int coords)
        {
            HexCell[] allCells = GameObject.FindObjectsByType<HexCell>(FindObjectsSortMode.None);
            return allCells.FirstOrDefault(c => c.Coordinates == coords);
        }

        private Pawn FindPawnOptimized(HexCell cell, List<Pawn> myPawns, List<Pawn> enemyPawns)
        {
            Pawn p = myPawns.FirstOrDefault(x => x.OccupiedCell == cell);
            if (p == null) p = enemyPawns.FirstOrDefault(x => x.OccupiedCell == cell);
            return p;
        }

        private bool IsPathBlockedOptimized(Vector2Int start, Vector2Int end, Dictionary<Vector2Int, HexCell> lookup)
        {
            Vector3Int startCube = AlperKocasalih.Chess.Grid.Utils.HexGridMath.OffsetToCube(start);
            Vector3Int targetCube = AlperKocasalih.Chess.Grid.Utils.HexGridMath.OffsetToCube(end);
            int dist = AlperKocasalih.Chess.Grid.Utils.HexGridMath.CubeDistance(startCube, targetCube);

            if (dist <= 1) return false; 

            for (int i = 1; i < dist; i++) 
            {
                Vector3 cubeFloat = AlperKocasalih.Chess.Grid.Utils.HexGridMath.CubeLerp(startCube, targetCube, 1f / dist * i);
                Vector3Int cubePoint = AlperKocasalih.Chess.Grid.Utils.HexGridMath.CubeRound(cubeFloat);
                Vector2Int pathCoord = AlperKocasalih.Chess.Grid.Utils.HexGridMath.CubeToOffset(cubePoint);

                if (lookup.TryGetValue(pathCoord, out HexCell pathCell))
                {
                    if (pathCell.IsObstacle || pathCell.IsOccupied) return true; // Friendly/Enemy pawns also block path unless distance is 1
                }
                else return true;
            }
            return false;
        }

        private List<HexCell> FindCellsInRows(int[] rows)
        {
            List<HexCell> cells = new List<HexCell>();
            HexCell[] allCells = GameObject.FindObjectsByType<HexCell>(FindObjectsSortMode.None);
            foreach (var cell in allCells)
            {
                if (rows.Contains(cell.R)) { cells.Add(cell); }
            }
            return cells;
        }

        private bool IsDrawLocked()
        {
            var method = typeof(DraftManager).GetMethod("IsDrawLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null) return false;
            return (bool)method.Invoke(DraftManager.Instance, null);
        }
    }
}
