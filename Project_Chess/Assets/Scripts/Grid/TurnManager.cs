using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class TurnManager : NetworkBehaviour
    {
        public static TurnManager Instance { get; private set; }
        public System.Action<int> OnTurnChanged;

        #region Fields

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI turnInfoText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI diceResultText;
        [SerializeField] private CanvasGroup diceUI;

        [Header("Turn State")]
        [SerializeField, ReadOnly] private NetworkVariable<int> activePlayerID = new NetworkVariable<int>(1);
        [SerializeField, ReadOnly] private NetworkVariable<int> turnCount = new NetworkVariable<int>(1);

        [Header("Turn Timer")]
        [SerializeField] private float turnDurationSeconds = 180f;
        [SerializeField] private int maxAfkCount = 3;
        [SerializeField, ReadOnly] private NetworkVariable<float> remainingTurnTime = new NetworkVariable<float>(180f);
        [SerializeField, ReadOnly] private NetworkVariable<int> player1AfkCount = new NetworkVariable<int>(0);
        [SerializeField, ReadOnly] private NetworkVariable<int> player2AfkCount = new NetworkVariable<int>(0);

        #endregion

        #region Properties

        public int ActivePlayerID => activePlayerID.Value;
        public int TurnCount => turnCount.Value;
        public float RemainingTurnTime => remainingTurnTime.Value;
        public int Player1AfkCount => player1AfkCount.Value;
        public int Player2AfkCount => player2AfkCount.Value;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            LocalizationManager.OnLanguageChanged += UpdateTurnInfoUI;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= UpdateTurnInfoUI;
        }

        public override void OnNetworkSpawn()
        {
            activePlayerID.OnValueChanged += (oldValue, newValue) => {
                UpdateTurnInfoUI();
                UpdateTimerUI();
                OnTurnChanged?.Invoke(newValue);
            };

            remainingTurnTime.OnValueChanged += (oldValue, newValue) => UpdateTimerUI();
            player1AfkCount.OnValueChanged += (oldValue, newValue) => UpdateTimerUI();
            player2AfkCount.OnValueChanged += (oldValue, newValue) => UpdateTimerUI();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += (state) => {
                    UpdateTurnInfoUI();
                    UpdateTimerUI();
                    if (IsServer && state == GameState.ActionPhase)
                    {
                        ResetTurnTimer();
                    }
                };
            }
            
            UpdateTurnInfoUI();
            UpdateTimerUI();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase) return;

            // Safety net: If both players have empty hands in ActionPhase, return to DraftPhase immediately
            if (DraftManager.Instance != null && 
                DraftManager.Instance.GetHand(1).Count == 0 && 
                DraftManager.Instance.GetHand(2).Count == 0)
            {
                Debug.Log("TurnManager: Both hands empty in ActionPhase. Transitioning to DraftPhase.");
                GameManager.Instance.ChangeState(GameState.DraftPhase);
                return;
            }

            if (remainingTurnTime.Value <= 0f) return;

            remainingTurnTime.Value = Mathf.Max(0f, remainingTurnTime.Value - Time.deltaTime);
            if (remainingTurnTime.Value <= 0f)
            {
                HandleTurnTimeout();
            }
        }

        #endregion

        #region Turn Logic

        public void RollForTurn()
        {
            if (!IsServer) return;
            StartCoroutine(RollDiceRoutine());
        }

        private IEnumerator RollDiceRoutine()
        {
            // Dice UI animation and value syncing
            ShowDiceUIClientRpc();

            int p1Final = 0;
            int p2Final = 0;

            for (int i = 0; i < 10; i++)
            {
                p1Final = Random.Range(1, 101);
                p2Final = Random.Range(1, 101);
                UpdateDiceTextClientRpc(p1Final, p2Final, false);
                yield return new WaitForSeconds(0.1f);
            }

            while (p1Final == p2Final)
            {
                p1Final = Random.Range(1, 101);
                p2Final = Random.Range(1, 101);
            }

            UpdateDiceTextClientRpc(p1Final, p2Final, true);

            activePlayerID.Value = p1Final > p2Final ? 1 : 2;

            yield return new WaitForSeconds(1.5f);

            HideDiceUIClientRpc();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.DraftPhase);
            }
        }

        [ClientRpc]
        private void ShowDiceUIClientRpc()
        {
            if (diceUI != null)
            {
                diceUI.alpha = 0;
                diceUI.gameObject.SetActive(true);
                diceUI.DOFade(1, 0.5f);
            }
        }

        [ClientRpc]
        private void UpdateDiceTextClientRpc(int p1, int p2, bool isFinal)
        {
            if (diceResultText != null)
                diceResultText.text = isFinal ? $"Final - P1: {p1} | P2: {p2}" : $"P1: {p1} | P2: {p2}";
        }

        [ClientRpc]
        private void HideDiceUIClientRpc()
        {
            if (diceUI != null)
            {
                diceUI.DOFade(0, 0.5f).OnComplete(() => diceUI.gameObject.SetActive(false));
            }
        }

        public void NextTurn()
        {
            Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();
            if (!IsServer)
            {
                NextTurnServerRpc();
                return;
            }

            activePlayerID.Value = activePlayerID.Value == 1 ? 2 : 1;
            turnCount.Value++;
            ResetTurnTimer();
            
            // Safe Aura Refresh
            if (AuraManager.instance == null)
            {
                AuraManager[] found = FindObjectsByType<AuraManager>(FindObjectsSortMode.None);
                if (found.Length > 0) AuraManager.instance = found[0];
            }

            if (AuraManager.instance != null)
            {
                AuraManager.instance.RefreshAuraServer(gridLookup);
            }
            else
            {
                Debug.LogWarning("TurnManager: AuraManager.instance is null and none found; skipping aura refresh.");
            }

            Debug.Log($"TurnManager: Player {activePlayerID.Value}'s turn.");
        }

        private void HandleTurnTimeout()
        {
            int timedOutPlayerID = activePlayerID.Value;
            int newAfkCount = IncrementAfkCount(timedOutPlayerID);

            Debug.Log($"TurnManager: Player {timedOutPlayerID} timed out. AFK count: {newAfkCount}/{maxAfkCount}");

            if (newAfkCount >= maxAfkCount)
            {
                int winnerID = timedOutPlayerID == 1 ? 2 : 1;
                if (BotMatchReporter.Instance != null)
                {
                    BotMatchReporter.Instance.SetWinCondition(WinConditionType.Timeout);
                }

                GameManager.Instance.EndGame(winnerID);
                return;
            }

            NextTurn();
        }

        private int IncrementAfkCount(int playerID)
        {
            if (playerID == 1)
            {
                player1AfkCount.Value++;
                return player1AfkCount.Value;
            }

            player2AfkCount.Value++;
            return player2AfkCount.Value;
        }

        private void ResetTurnTimer()
        {
            remainingTurnTime.Value = turnDurationSeconds;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void NextTurnServerRpc()
        {
            NextTurn();
        }

        private void UpdateTurnInfoUI()
        {
            if (turnInfoText != null)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Setup)
                {
                    turnInfoText.text = LocalizationManager.GetTranslation("Setup Phase");
                }
                else
                {
                    int localPlayerID = 1;
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    {
                        localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                    }

                    bool isMyTurn = localPlayerID == activePlayerID.Value;
                    turnInfoText.text = isMyTurn ? LocalizationManager.GetTranslation("Your Turn") : LocalizationManager.GetTranslation("Opponent's Turn");
                }
                
                // Reset scale and kill previous tweens to prevent cumulative scaling (UI growing)
                turnInfoText.transform.DOKill(true);
                turnInfoText.transform.localScale = Vector3.one;
                turnInfoText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
            }
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.ActionPhase)
            {
                timerText.gameObject.SetActive(false);
                return;
            }

            timerText.gameObject.SetActive(true);

            int secondsLeft = Mathf.CeilToInt(remainingTurnTime.Value);
            int minutes = secondsLeft / 60;
            int seconds = secondsLeft % 60;
            int afkCount = activePlayerID.Value == 1 ? player1AfkCount.Value : player2AfkCount.Value;

            timerText.text = $"{minutes:00}:{seconds:00} | AFK {afkCount}/{maxAfkCount}";
        }

        public void RefreshTurnInfoUI()
        {
            UpdateTurnInfoUI();
            UpdateTimerUI();
        }

        public void ResetManager()
        {
            if (!IsServer) return;
            activePlayerID.Value = 1;
            turnCount.Value = 1;
            player1AfkCount.Value = 0;
            player2AfkCount.Value = 0;
            ResetTurnTimer();
        }

        #endregion
    }
}

