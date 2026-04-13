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

            // --- ACTION PHASE ---
            Debug.Log("--- STARTING BOT VS BOT TEST: ACTION PHASE ---");

            // Round 1
            Assert.IsTrue(IsDrawLocked(), "Draw should be locked in Round 1.");
            TurnManager.Instance.NextTurn(); // P1 end turn
            yield return new WaitForSeconds(0.2f);
            TurnManager.Instance.NextTurn(); // P2 end turn
            yield return new WaitForSeconds(0.2f);

            // Round 2
            Assert.IsTrue(IsDrawLocked(), "Draw should be locked in Round 2.");
            TurnManager.Instance.NextTurn(); // P1 end turn
            yield return new WaitForSeconds(0.2f);
            TurnManager.Instance.NextTurn(); // P2 end turn
            yield return new WaitForSeconds(0.2f);

            // Round 3
            Assert.IsFalse(IsDrawLocked(), "Draw should be unlocked in Round 3.");
            
            Debug.Log("--- BOT VS BOT TEST COMPLETED SUCCESSFULLY ---");
        }

        private List<HexCell> FindCellsInRows(int[] rows)
        {
            List<HexCell> cells = new List<HexCell>();
            HexCell[] allCells = GameObject.FindObjectsByType<HexCell>(FindObjectsSortMode.None);
            foreach (var cell in allCells)
            {
                if (rows.Contains(cell.R))
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        private bool IsDrawLocked()
        {
            // Use reflection to access private IsDrawLocked or check blockDrawForRounds
            var method = typeof(DraftManager).GetMethod("IsDrawLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method.Invoke(DraftManager.Instance, null);
        }
    }
}
