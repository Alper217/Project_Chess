using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public class DeckVisual3D : MonoBehaviour
    {
        #region Interaction

        private void OnMouseDown()
        {
            // Check if we are in a state that allows starting a draft round
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DraftPhase)
            {
                if (DraftManager.Instance != null)
                {
                    // Logic to start a round if it's not already active or if we want to trigger draw
                    // For now, let's just log or trigger a draw if needed
                    Debug.Log("DeckVisual3D: Deck clicked!");
                }
            }
        }

        #endregion
    }
}
