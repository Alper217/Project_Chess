using System;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using AlperKocasalih.Chess.Multiplayer;

namespace AlperKocasalih.Chess.Grid
{
    public enum GameState
    {
        Setup,
        RollDice,
        DraftPhase,
        ActionPhase,
        EndGame
    }

    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Fields

        [Header("Game State")]
        [SerializeField, ReadOnly] private NetworkVariable<GameState> currentState = new NetworkVariable<GameState>(GameState.Setup, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Restart Readiness")]
        [SerializeField, ReadOnly] private NetworkVariable<bool> hostWantsRestart = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        [SerializeField, ReadOnly] private NetworkVariable<bool> clientWantsRestart = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Cameras")]
        public GameObject player1Camera;
        public GameObject player2Camera;

        [Header("Scores")]
        [SerializeField, ReadOnly] public NetworkVariable<int> player1Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        [SerializeField, ReadOnly] public NetworkVariable<int> player2Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Score UI")]
        [SerializeField] private TextMeshProUGUI player1ScoreText;
        [SerializeField] private GameObject player1UI;
        [SerializeField] private TextMeshProUGUI player2ScoreText;
        [SerializeField] private GameObject player2UI;


        #endregion

        #region Events

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnGameEnded;

        #endregion

        #region Properties

        public GameState CurrentState => currentState.Value;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            Application.targetFrameRate = -1;
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            currentState.OnValueChanged += (oldValue, newValue) => {
                Debug.Log($"GameManager: State changed from {oldValue} to {newValue}");
                HandleStateChange(newValue);
                UpdateScoreUI(); // State değiştiğinde puan tablosunu güncelle
                OnStateChanged?.Invoke(newValue);
            };
            
            player1Score.OnValueChanged += (oldValue, newValue) => {
                Debug.Log($"GameManager: Player 1 Score changed from {oldValue} to {newValue}");
                UpdateScoreUI();
            };
            player2Score.OnValueChanged += (oldValue, newValue) => {
                Debug.Log($"GameManager: Player 2 Score changed from {oldValue} to {newValue}");
                UpdateScoreUI();
            };
            
            // Dil değiştiğinde skoru anında güncelle
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged += UpdateScoreUI;

            // Initial handle for current state
            HandleStateChange(currentState.Value);
            UpdateScoreUI();
        }

        private void OnDestroy()
        {
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged -= UpdateScoreUI;
        }

        private void UpdateScoreUI()
        {
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            bool shouldShow = currentState.Value != GameState.Setup;
            string scoreLabel = AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("My Score");

            if (player1ScoreText != null) 
            {
                player1ScoreText.text = $"{scoreLabel}: {player1Score.Value}";
                player1ScoreText.gameObject.SetActive(shouldShow && localPlayerID == 1);
                player1UI.SetActive(shouldShow);
            }

            if (player2ScoreText != null) 
            {
                player2ScoreText.text = $"{scoreLabel}: {player2Score.Value}";
                player2ScoreText.gameObject.SetActive(shouldShow && localPlayerID == 2);
                player2UI.SetActive(shouldShow);
            }
        }

        private void Start()
        {
            if (IsServer)
            {
                ChangeState(GameState.Setup);
            }
            
        }

        #endregion

        #region State Management

        public void ChangeState(GameState newState)
        {
            if (!IsServer)
            {
                Debug.LogWarning("GameManager: Only Server can change game state!");
                return;
            }

            if (currentState.Value == newState) return;

            // State Protection: Cannot leave EndGame unless resetting to Setup
            if (currentState.Value == GameState.EndGame && newState != GameState.Setup)
            {
                Debug.LogWarning("GameManager: Game has already ended. State change blocked.");
                return;
            }

            currentState.Value = newState;
        }

        private void HandleStateChange(GameState newState)
        {
            switch (newState)
            {
                case GameState.Setup:
                    
                    break;
                case GameState.RollDice:
                    if (IsServer && TurnManager.Instance != null)
                    {
                        TurnManager.Instance.RollForTurn();
                    }
                    break;
                case GameState.DraftPhase:
                    Debug.Log("GameManager: Draft phase started.");
                    if (DraftManager.Instance != null)
                    {
                        DraftManager.Instance.StartDraft();
                    }
                    else if (IsServer)
                    {
                        Debug.LogError("GameManager: DraftManager not found! Skipping to ActionPhase.");
                        ChangeState(GameState.ActionPhase);
                    }
                    break;
                case GameState.ActionPhase:
                    Debug.Log("GameManager: Action phase started.");
                    break;
                case GameState.EndGame:
                    Debug.Log("GameManager: Game Over!");
                    break;
            }
        }

        public void EndGame(int winnerID)
        {
            if (!IsServer) return;
            
            Debug.Log($"GameManager: EndGame called. Winner: {winnerID}");
            ChangeState(GameState.EndGame);
            
            // Invoke locally for host/singleplayer
            OnGameEnded?.Invoke(winnerID);
            
            // Notify clients
            EndGameClientRpc(winnerID);
        }

        /// <summary>
        /// Called when a pawn is eliminated and we need to check if that player is wiped out.
        /// Sets WinCondition to AllEnemyPawnsEliminated before ending.
        /// </summary>
        public void EndGameByElimination(int winnerID)
        {
            if (!IsServer) return;
            if (BotMatchReporter.Instance != null)
                BotMatchReporter.Instance.SetWinCondition(WinConditionType.AllEnemyPawnsEliminated);
            EndGame(winnerID);
        }

        public void AddScore(int playerID, int points)
        {
            if (!IsServer) return;
            if (playerID == 1) player1Score.Value += points;
            else if (playerID == 2) player2Score.Value += points;
            
            Debug.Log($"GameManager: Player {playerID} scored {points} points! Current Score -> P1: {player1Score.Value}, P2: {player2Score.Value}");
        }

        public void CheckWinConditionPoints()
        {
            if (!IsServer) return;

            WinConditionType condType;
            if (player1Score.Value > player2Score.Value)
            {
                condType = WinConditionType.PointAdvantage;
                if (BotMatchReporter.Instance != null) BotMatchReporter.Instance.SetWinCondition(condType);
                EndGame(1);
            }
            else if (player2Score.Value > player1Score.Value)
            {
                condType = WinConditionType.PointAdvantage;
                if (BotMatchReporter.Instance != null) BotMatchReporter.Instance.SetWinCondition(condType);
                EndGame(2);
            }
            else
            {
                condType = WinConditionType.Draw;
                if (BotMatchReporter.Instance != null) BotMatchReporter.Instance.SetWinCondition(condType);
                // Tie (0 means equality/draw)
                EndGame(0);
            }
        }

        public void CheckWinCondition(int loserID)
        {
            if (!IsServer) return;

            Pawn[] allPawns = GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            bool hasPawnsLeft = false;
            foreach (var p in allPawns)
            {
                if (p != null && p.IsSpawned && p.PlayerID == loserID)
                {
                    hasPawnsLeft = true;
                    break;
                }
            }

            if (!hasPawnsLeft)
            {
                int winnerID = loserID == 1 ? 2 : 1;
                // Set win condition before ending
                if (BotMatchReporter.Instance != null)
                    BotMatchReporter.Instance.SetWinCondition(WinConditionType.AllEnemyPawnsEliminated);
                EndGame(winnerID);
            }
        }

        [ClientRpc]
        private void EndGameClientRpc(int winnerID)
        {
            if (!IsServer)
            {
                OnGameEnded?.Invoke(winnerID);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestRestartServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (clientId == NetworkManager.ServerClientId)
            {
                hostWantsRestart.Value = true;
            }
            else
            {
                clientWantsRestart.Value = true;
            }

            CheckForRestart();
        }

        private void CheckForRestart()
        {
            // In Singleplayer (only 1 connected client), only the host needs to want a restart
            bool isSingleplayer = NetworkManager.Singleton.ConnectedClients.Count <= 1;
            
            if (isSingleplayer)
            {
                if (hostWantsRestart.Value)
                {
                    hostWantsRestart.Value = false;
                    clientWantsRestart.Value = false;
                    RestartGame();
                }
            }
            else
            {
                // In Multiplayer, both must confirm
                if (hostWantsRestart.Value && clientWantsRestart.Value)
                {
                    hostWantsRestart.Value = false;
                    clientWantsRestart.Value = false;
                    RestartGame();
                }
            }
        }

        public void RestartGame()
        {
            if (!IsServer) return;
            
            player1Score.Value = 0;
            player2Score.Value = 0;
            
            Debug.Log("GameManager: Restarting Game...");
            RestartGameClientRpc();
        }


        [ClientRpc]
        private void RestartGameClientRpc()
        {
            // 1. Clear Pawns
            Pawn[] allPawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            foreach (var pawn in allPawns)
            {
                if (pawn.OccupiedCell != null)
                {
                    pawn.OccupiedCell.IsOccupied = false;
                    pawn.OccupiedCell.ClearOccupiedPawn();
                }
                // On Server, NetworkObjects should be despawned. 
                // Locally we might need to handle destruction if not a NetworkObject yet.
                if (IsServer) pawn.GetComponent<NetworkObject>()?.Despawn();
                else if (pawn.GetComponent<NetworkObject>() == null) Destroy(pawn.gameObject);
            }

            // 2. Reset Managers
            if (TurnManager.Instance != null) TurnManager.Instance.ResetManager();
            if (PawnPlacementManager.Instance != null) PawnPlacementManager.Instance.ResetTracking();
            if (DraftManager.Instance != null) DraftManager.Instance.ResetManager();

            // 3. Reset Deck
            if (IsServer && DeckManager.Instance != null && DraftManager.Instance != null)
            {
                int newSeed = UnityEngine.Random.Range(1000, 999999);
                DraftManager.Instance.deckSeed.Value = newSeed;
                DeckManager.Instance.InitializeDeckWithSeed(newSeed);
            }

            // 4. Return to Setup
            if (IsServer) ChangeState(GameState.Setup);
        }

        #endregion
    }
}

