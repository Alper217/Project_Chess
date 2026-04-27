using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Threading.Tasks;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject bootstrapUI;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button playVsBotButton;        // ← Yeni: Bot Modu butonu
        [SerializeField] private TMP_InputField codeInputField; // Artık IP değil, Relay Kodu girişi
        [SerializeField] private TextMeshProUGUI relayCodeText; // Host olduğumuzda kodu göstereceğimiz text
        [SerializeField] private string gameSceneName = "GameScene";
        public static string JoinCode { get; private set; }

        private async void Start()
        {
            // Unity Services'i başlat ve anonim giriş yap (Relay için zorunlu)
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in to Unity Services. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
        }

        public async void StartHost()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null! Is there a NetworkManager in the scene?");
                return;
            }

            // If already running, shut it down first
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Relay Kurulumu (Host) - Max 1 bağlantı (2 kişilik satranç oyunu için: 1 Host + 1 Client)
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"Relay Host Created. Join Code: {JoinCode}");
                
                if (relayCodeText != null)
                {
                    relayCodeText.text = $"Room Code: {JoinCode}";
                    relayCodeText.gameObject.SetActive(true);
                }

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                if (NetworkManager.Singleton.StartHost())
                {
                    Debug.Log("Host started successfully via Relay, loading game scene...");
                    NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogError("Failed to start Host!");
                }
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Relay Host Error: {e.Message}");
            }
        }

        public async void StartClient()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null! Is there a NetworkManager in the scene?");
                return;
            }

            string joinCode = codeInputField != null ? codeInputField.text : "";
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Please enter a valid Join Code!");
                return;
            }

            // If already running, shut it down first
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Relay Bağlantısı (Client)
            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                Debug.Log("Relay Client Joined.");

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                if (NetworkManager.Singleton.StartClient())
                {
                    Debug.Log("Client started successfully via Relay, waiting for connection...");
                    HideUI();
                }
                else
                {
                    Debug.LogError("Failed to start Client!");
                }
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Relay Client Error: {e.Message}");
            }
        }

        /// <summary>
        /// Bot modunu başlatır: Relay gerektirmez, local host olarak oyunu yükler.
        /// GameScene yüklendiğinde BotSpawner, BotSession.IsActive'i kontrol ederek
        /// BotAIController'ı otomatik spawn eder.
        /// </summary>
        public void StartVsBot()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null!");
                return;
            }

            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            // UnityTransport, Relay kurulmadığında default olarak 127.0.0.1:7777 kullanır.
            // SetConnectionData() çağrısı Unity.Networking.Transport assembly'si gerektirdiği için kullanılmıyor.

            // Bot modunu PlayerPrefs ile işaretle (namespace/assembly bağımlılığı yok)
            PlayerPrefs.SetInt("BotMode", 1);
            PlayerPrefs.Save();
            JoinCode = "BOT";

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[VsBot] Host başlatıldı (local). Sahne yükleniyor...");
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[VsBot] Host başlatılamadı!");
                PlayerPrefs.DeleteKey("BotMode");
                PlayerPrefs.Save();
            }
        }

        private void HideUI()
        {
            if (bootstrapUI != null) bootstrapUI.SetActive(false);
        }
    }
}

