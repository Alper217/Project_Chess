using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class LobbyPlayerEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI readyStatusText;
        [SerializeField] private Image playerPhoto; // Placeholder for future Steam photo
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;

        private LobbyPlayerState playerState;

        public void SetPlayer(LobbyPlayerState state)
        {
            playerState = state;
            UpdateUI();
            
            // Listen for changes
            playerState.IsReady.OnValueChanged += (oldVal, newVal) => UpdateUI();
            playerState.PlayerName.OnValueChanged += (oldVal, newVal) => UpdateUI();
        }

        private void UpdateUI()
        {
            if (playerState == null) return;

            playerNameText.text = playerState.PlayerName.Value.ToString();
            
            bool isReady = playerState.IsReady.Value;
            readyStatusText.text = isReady ? "READY" : "NOT READY";
            readyStatusText.color = isReady ? readyColor : notReadyColor;
            
            // Photo placeholder logic can go here (e.g. gray box if no steam)
        }
    }
}
