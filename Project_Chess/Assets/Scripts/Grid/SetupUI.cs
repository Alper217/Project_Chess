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
        }

        private void HandleStateChanged(GameState newState)
        {
            if (setupPanel != null)
            {
                setupPanel.SetActive(newState == GameState.Setup);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = (newState == GameState.Setup);
            }
        }

        private void OnConfirmClicked()
        {
            if (PawnPlacementManager.Instance != null)
            {
                PawnPlacementManager.Instance.ConfirmLocalPlayerPlacement();
                // Disable after confirm to prevent double-submit until next Setup state.
                if (confirmButton != null) confirmButton.interactable = false;
            }
        }
    }
}
