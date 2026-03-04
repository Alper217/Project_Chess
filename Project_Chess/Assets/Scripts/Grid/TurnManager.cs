using System.Collections;
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
        [SerializeField] private TextMeshProUGUI diceResultText;
        [SerializeField] private CanvasGroup diceUI;

        [Header("Turn State")]
        [SerializeField, ReadOnly] private NetworkVariable<int> activePlayerID = new NetworkVariable<int>(1);
        [SerializeField, ReadOnly] private NetworkVariable<int> turnCount = new NetworkVariable<int>(1);

        #endregion

        #region Properties

        public int ActivePlayerID => activePlayerID.Value;
        public int TurnCount => turnCount.Value;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            activePlayerID.OnValueChanged += (oldValue, newValue) => {
                UpdateTurnInfoUI();
                OnTurnChanged?.Invoke(newValue);
            };
            
            UpdateTurnInfoUI();
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
            if (!IsServer)
            {
                NextTurnServerRpc();
                return;
            }

            activePlayerID.Value = activePlayerID.Value == 1 ? 2 : 1;
            turnCount.Value++;
            
            Debug.Log($"TurnManager: Player {activePlayerID.Value}'s turn.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void NextTurnServerRpc()
        {
            NextTurn();
        }

        private void UpdateTurnInfoUI()
        {
            if (turnInfoText != null)
            {
                int localPlayerID = 1;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                }

                bool isMyTurn = localPlayerID == activePlayerID.Value;
                turnInfoText.text = $"Turn: Player {activePlayerID.Value}";
                turnInfoText.color = isMyTurn ? Color.green : Color.gray;
                turnInfoText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
            }
        }

        public void RefreshTurnInfoUI()
        {
            UpdateTurnInfoUI();
        }

        public void ResetManager()
        {
            if (!IsServer) return;
            activePlayerID.Value = 1;
            turnCount.Value = 1;
        }

        #endregion
    }
}

