using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public class DeckVisual3D : MonoBehaviour
    {
        #region Interaction

        private void OnMouseDown()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DraftPhase)
            {
                // Placeholder for DraftPhase logic
                Debug.Log("DeckVisual3D: Deck clicked during Draft Phase!");
                return;
            }

            if (CanDraw())
            {
                if (DraftManager.Instance != null)
                {
                    DraftManager.Instance.DrawOneAndSkipTurnServerRpc();
                }
            }
            else
            {
                Debug.Log("DeckVisual3D: Cannot draw a card right now.");
            }
        }

        private bool CanDraw()
        {
            bool isActionPhase = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.ActionPhase;
            bool isDrafting = DraftManager.Instance != null && DraftManager.Instance.IsDraftingActive;

            int localPlayerID = 1;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                localPlayerID = Unity.Netcode.NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            bool isMyTurn = TurnManager.Instance != null && TurnManager.Instance.ActivePlayerID == localPlayerID;
            bool isDrawAllowed = DraftManager.Instance == null || DraftManager.Instance.IsDrawAllowed;

            return isActionPhase && isMyTurn && !isDrafting && isDrawAllowed;
        }

        #endregion
    }
}
