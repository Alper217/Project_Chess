using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid;

namespace AlperKocasalih.Chess.UI
{
    public class SetupUI : MonoBehaviour
    {
        [SerializeField] private GameObject setupPanel;
        [SerializeField] private Button confirmButton;
        private bool localConfirmed = false;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (PawnPlacementManager.Instance != null)
            {
                PawnPlacementManager.Instance.OnLocalPlacementChanged += UpdateConfirmInteractable;
            }

            // Initialization state check
            if (GameManager.Instance != null)
            {
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
            else
            {
                if (setupPanel != null) setupPanel.SetActive(false);
                if (confirmButton != null) confirmButton.interactable = false;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            }
            if (PawnPlacementManager.Instance != null)
            {
                PawnPlacementManager.Instance.OnLocalPlacementChanged -= UpdateConfirmInteractable;
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            bool isSetup = newState == GameState.Setup;
            if (setupPanel != null)
            {
                setupPanel.SetActive(isSetup);
            }
            if (!isSetup) localConfirmed = false;
            UpdateConfirmInteractable();
        }

        private void OnConfirmClicked()
        {
            if (PawnPlacementManager.Instance != null)
            {
                PawnPlacementManager.Instance.ConfirmLocalPlayerPlacement();
                // Disable after confirm to prevent double-submit until next Setup state.
                localConfirmed = true;
                UpdateConfirmInteractable();
            }
        }

        private void UpdateConfirmInteractable()
        {
            if (confirmButton == null) return;
            if (GameManager.Instance == null)
            {
                confirmButton.interactable = false;
                return;
            }

            bool isSetup = GameManager.Instance.CurrentState == GameState.Setup;
            if (!isSetup || localConfirmed)
            {
                confirmButton.interactable = false;
                return;
            }

            if (PawnPlacementManager.Instance == null)
            {
                confirmButton.interactable = false;
                return;
            }

            int required = PawnPlacementManager.Instance.GetRequiredPlacementCount();
            int placed = PawnPlacementManager.Instance.GetLocalPlacedCount();
            confirmButton.interactable = required > 0 && placed >= required;
        }
    }
}
