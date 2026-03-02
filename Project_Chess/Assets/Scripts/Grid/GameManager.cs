using System;
using UnityEngine;
using Unity.Netcode;

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
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            currentState.OnValueChanged += (oldValue, newValue) => {
                Debug.Log($"GameManager: State changed from {oldValue} to {newValue}");
                HandleStateChange(newValue);
                OnStateChanged?.Invoke(newValue);
            };
            
            // Initial handle for current state
            HandleStateChange(currentState.Value);
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
            ChangeState(GameState.EndGame);
            OnGameEnded?.Invoke(winnerID);
            EndGameClientRpc(winnerID);
        }

        [ClientRpc]
        private void EndGameClientRpc(int winnerID)
        {
            if (!IsServer)
            {
                OnGameEnded?.Invoke(winnerID);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRestartServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
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
            if (hostWantsRestart.Value && clientWantsRestart.Value)
            {
                hostWantsRestart.Value = false;
                clientWantsRestart.Value = false;
                RestartGame();
            }
        }

        public void RestartGame()
        {
            if (!IsServer) return;
            
            Debug.Log("GameManager: Restarting Game...");
            RestartGameClientRpc();
        }


        [ClientRpc]
        private void RestartGameClientRpc()
        {
            // 1. Clear Pawns
            Pawn[] allPawns = FindObjectsOfType<Pawn>();
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
            if (DeckManager.Instance != null) DeckManager.Instance.InitializeDeck();

            // 4. Return to Setup
            if (IsServer) ChangeState(GameState.Setup);
        }

        #endregion
    }
}

