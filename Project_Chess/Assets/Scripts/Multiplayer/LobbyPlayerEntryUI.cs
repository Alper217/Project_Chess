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
            UnsubscribeToEvents();
            playerState = state;
            SubscribeToEvents();
            UpdateUI();
            UpdateAvatar(); // Fotoğrafı ilk başta bir kere dene
        }

        private void OnEnable()
        {
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged += UpdateUI;
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged -= UpdateUI;
            UnsubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            if (playerState != null)
            {
                playerState.IsReady.OnValueChanged += OnReadyChanged;
                playerState.PlayerName.OnValueChanged += OnNameChanged;
                playerState.PlayerSteamId.OnValueChanged += OnAvatarChanged;
            }
        }

        private void UnsubscribeToEvents()
        {
            if (playerState != null)
            {
                playerState.IsReady.OnValueChanged -= OnReadyChanged;
                playerState.PlayerName.OnValueChanged -= OnNameChanged;
                playerState.PlayerSteamId.OnValueChanged -= OnAvatarChanged;
            }
        }

        private void OnReadyChanged(bool oldVal, bool newVal) => UpdateUI();
        private void OnNameChanged(Unity.Collections.FixedString32Bytes oldVal, Unity.Collections.FixedString32Bytes newVal) => UpdateUI();
        private void OnAvatarChanged(ulong oldVal, ulong newVal) => UpdateAvatar();

        private void UpdateUI()
        {
            if (playerState == null) return;

            playerNameText.text = playerState.PlayerName.Value.ToString();
            
            bool isReady = playerState.IsReady.Value;
            readyStatusText.text = isReady ? 
                AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("READY") : 
                AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation("NOT READY");
            readyStatusText.color = isReady ? readyColor : notReadyColor;
        }

        private async void UpdateAvatar()
        {
            // Steam ID gelmediyse veya Steam kapalıysa çık
            if (playerState == null || playerState.PlayerSteamId.Value == 0) return;
            if (SteamManager.Instance == null || !SteamManager.Instance.IsSteamRunning) return;

            try
            {
                var texture = await SteamManager.Instance.GetAvatarTexture(playerState.PlayerSteamId.Value);
                
                if (texture != null && playerPhoto != null)
                {
                    // Texture2D'yi Sprite'a çevirip Image komponentine basıyoruz
                    playerPhoto.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyPlayerEntryUI] Failed to load Steam avatar: {e.Message}");
            }
        }
    }
}
