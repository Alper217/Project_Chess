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
            UpdateAvatar(); // Fotoğrafı ilk başta bir kere dene
            
            // Listen for changes
            playerState.IsReady.OnValueChanged += (oldVal, newVal) => UpdateUI();
            playerState.PlayerName.OnValueChanged += (oldVal, newVal) => UpdateUI();
            playerState.PlayerSteamId.OnValueChanged += (oldVal, newVal) => UpdateAvatar();
        }

        private void UpdateUI()
        {
            if (playerState == null) return;

            playerNameText.text = playerState.PlayerName.Value.ToString();
            
            bool isReady = playerState.IsReady.Value;
            readyStatusText.text = isReady ? "READY" : "NOT READY";
            readyStatusText.color = isReady ? readyColor : notReadyColor;
        }

        private async void UpdateAvatar()
        {
            // Steam ID gelmediyse veya Steam kapalıysa çık
            if (playerState == null || playerState.PlayerSteamId.Value == 0) return;
            if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamRunning) return;

            var texture = await SteamManager.Instance.GetAvatarTexture(playerState.PlayerSteamId.Value);
            
            if (texture != null && playerPhoto != null)
            {
                // Texture2D'yi Sprite'a çevirip Image komponentine basıyoruz
                playerPhoto.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            }
        }
    }
}
