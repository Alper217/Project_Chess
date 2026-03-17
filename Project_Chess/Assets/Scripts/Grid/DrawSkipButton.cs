using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace AlperKocasalih.Chess.Grid
{
    public class DrawSkipButton : MonoBehaviour
    {
        [SerializeField] private Button drawSkipButton;


        private TurnManager subscribedTurnManager;
        private GameManager subscribedGameManager;
        private float nextDebugTime;

        private void Awake()
        {
            if (drawSkipButton == null)
            {
                drawSkipButton = GetComponent<Button>();
                if (drawSkipButton == null)
                {
                    drawSkipButton = GetComponentInChildren<Button>();
                }
            }
        }

        private void Start()
        {
            if (drawSkipButton != null)
            {
                drawSkipButton.onClick.AddListener(OnDrawSkipClicked);
            }

            UpdateInteractable();
        }

        private void Update()
        {
            TrySubscribeManagers();
            UpdateInteractable();
        }

        private void OnDestroy()
        {
            if (subscribedTurnManager != null)
            {
                subscribedTurnManager.OnTurnChanged -= HandleTurnChanged;
                subscribedTurnManager = null;
            }

            if (subscribedGameManager != null)
            {
                subscribedGameManager.OnStateChanged -= HandleStateChanged;
                subscribedGameManager = null;
            }
        }

        public void OnDrawSkipClicked()
        {
            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.DrawOneAndSkipTurnServerRpc();
            }
        }

        private void UpdateInteractable()
        {
            if (drawSkipButton == null) return;

            bool isActionPhase = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.ActionPhase;
            bool isDrafting = DraftManager.Instance != null && DraftManager.Instance.IsDraftingActive;

            int localPlayerID = 1;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                localPlayerID = Unity.Netcode.NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            bool isMyTurn = TurnManager.Instance != null && TurnManager.Instance.ActivePlayerID == localPlayerID;
            bool isDrawAllowed = DraftManager.Instance == null || DraftManager.Instance.IsDrawAllowed;
            drawSkipButton.interactable = isActionPhase && isMyTurn && !isDrafting && isDrawAllowed;
        }

        private void HandleTurnChanged(int _)
        {
            UpdateInteractable();
        }

        private void HandleStateChanged(GameState _)
        {
            UpdateInteractable();
        }

        private void TrySubscribeManagers()
        {
            if (subscribedTurnManager == null && TurnManager.Instance != null)
            {
                subscribedTurnManager = TurnManager.Instance;
                subscribedTurnManager.OnTurnChanged += HandleTurnChanged;
            }

            if (subscribedGameManager == null && GameManager.Instance != null)
            {
                subscribedGameManager = GameManager.Instance;
                subscribedGameManager.OnStateChanged += HandleStateChanged;
            }
        }
    }
}
