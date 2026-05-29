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
        public static NetworkBootstrap Instance { get; private set; }

        [SerializeField] private GameObject bootstrapUI;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button playVsBotButton;
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private TextMeshProUGUI relayCodeText;
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";
        public static string JoinCode { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private async void Start()
        {
            Application.targetFrameRate = 60;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                Debug.Log("[NetworkBootstrap] Unity Services initialized and signed in anonymously.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkBootstrap] Services initialization failed: {e.Message}");
            }
        }

        public async void StartHost()
        {
            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            
            PlayerPrefs.DeleteKey("BotMode");
            PlayerPrefs.Save();

            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                
                if (relayCodeText != null)
                {
                    relayCodeText.text = $"Room Code: {JoinCode}";
                    relayCodeText.gameObject.SetActive(true);
                }

                // Steam Lobi Entegrasyonu
                if (SteamManager.Instance != null && SteamManager.Instance.IsSteamRunning)
                {
                    SteamManager.Instance.CreateSteamLobby(JoinCode);
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
                    NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Relay Host Error: {e.Message}");
            }
        }

        public async void StartClient()
        {
            string joinCode = codeInputField != null ? codeInputField.text : "";
            JoinWithCode(joinCode);
        }

        public async void JoinWithCode(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode)) return;

            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            
            PlayerPrefs.DeleteKey("BotMode");
            PlayerPrefs.Save();

            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
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
                    HideUI();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Relay Client Error: {e.Message}");
            }
        }

        public void StartVsBot()
        {
            if (NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();

            PlayerPrefs.SetInt("BotMode", 1);
            PlayerPrefs.Save();
            JoinCode = "BOT";

            if (NetworkManager.Singleton.StartHost())
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
        }

        public void HideUI()
        {
            if (bootstrapUI != null) bootstrapUI.SetActive(false);
        }
    }
}
