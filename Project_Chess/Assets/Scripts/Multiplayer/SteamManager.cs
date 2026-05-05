using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }
        
        [SerializeField] private uint appId = 480; // Spacewar (Test için)
        
        public bool IsSteamRunning { get; private set; }
        public string PlayerName => IsSteamRunning ? SteamClient.Name : "Offline Player";
        public SteamId PlayerSteamId => IsSteamRunning ? SteamClient.SteamId : 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSteam();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSteam()
        {
            try
            {
                SteamClient.Init(appId);
                IsSteamRunning = true;
                Debug.Log($"[Steam] Başarıyla bağlanıldı! Oyuncu: {SteamClient.Name}");

                // Davetleri dinle
                SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
                SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            }
            catch (Exception e)
            {
                IsSteamRunning = false;
                Debug.LogError($"[Steam] Başlatılamadı! Hata: {e.Message}");
            }
        }

        // Host için: Steam Lobisi kur ve Relay kodunu içine yaz
        public async void CreateSteamLobby(string relayJoinCode)
        {
            if (!IsSteamRunning) return;

            var lobby = await SteamMatchmaking.CreateLobbyAsync(2);
            if (lobby.HasValue)
            {
                lobby.Value.SetPublic();
                lobby.Value.SetJoinable(true);
                lobby.Value.SetData("RelayCode", relayJoinCode); // Relay kodunu buraya gizliyoruz
                Debug.Log("[Steam] Lobi kuruldu ve Relay kodu işlendi.");
            }
        }

        private void OnLobbyInvite(Friend friend, Lobby lobby)
        {
            Debug.Log($"[Steam] {friend.Name} sizi oyuna davet etti!");
            // Davet kabul edildiğinde otomatik katılma mantığı tetiklenebilir
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            string relayCode = lobby.GetData("RelayCode");
            if (!string.IsNullOrEmpty(relayCode))
            {
                Debug.Log($"[Steam] Lobiden Relay kodu alındı: {relayCode}. Katılınıyor...");
                // NetworkBootstrap üzerinden bu kodla bağlanacağız
                if (NetworkBootstrap.Instance != null)
                {
                    NetworkBootstrap.Instance.JoinWithCode(relayCode);
                }
            }
        }

        private void Update()
        {
            if (IsSteamRunning)
            {
                SteamClient.RunCallbacks();
            }
        }

        // Steam'den büyük boy fotoğrafı çeker ve Unity Texture2D formatına çevirir
        public async Task<Texture2D> GetAvatarTexture(ulong steamId)
        {
            if (!IsSteamRunning) return null;

            // Büyük boy avatarı iste
            var image = await SteamFriends.GetLargeAvatarAsync(steamId);
            if (!image.HasValue) 
            {
                Debug.LogWarning($"[Steam] {steamId} için avatar bulunamadı.");
                return null;
            }

            int width = (int)image.Value.Width;
            int height = (int)image.Value.Height;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // Steam verisi üstten aşağıya (top-down), Unity ise alttan yukarıya (bottom-up) çalışır.
            // Bu yüzden pikselleri ters çevirerek yüklemeliyiz.
            byte[] rawData = image.Value.Data;
            byte[] flippedData = new byte[rawData.Length];

            int stride = width * 4; // RGBA = 4 byte
            for (int y = 0; y < height; y++)
            {
                Array.Copy(rawData, y * stride, flippedData, (height - 1 - y) * stride, stride);
            }

            texture.LoadRawTextureData(flippedData);
            texture.Apply();

            Debug.Log($"[Steam] Avatar yüklendi: {width}x{height}");
            return texture;
        }

        private void OnApplicationQuit()
        {
            if (IsSteamRunning)
            {
                SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
                SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
                SteamClient.Shutdown();
            }
        }
    }
}
