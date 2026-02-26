using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject bootstrapUI;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private string gameSceneName = "GameScene";



        public void StartHost()
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

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host started successfully, loading game scene...");
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("Failed to start Host! 1. Başka bir instance açık kalmış olabilir. 2. 7777 portu dolu olabilir. 3. UnityTransport ayarlarını kontrol et.");
            }
        }

        public void StartClient()
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

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client started successfully, waiting for connection...");
                HideUI();
            }
            else
            {
                Debug.LogError("Failed to start Client! Host'un açık olduğundan ve aynı portu kullandığınızdan emin olun.");
            }
        }

        private void HideUI()
        {
            if (bootstrapUI != null) bootstrapUI.SetActive(false);
        }
    }
}

