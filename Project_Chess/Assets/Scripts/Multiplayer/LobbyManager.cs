using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private LobbyUI lobbyUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            RefreshLobbyUI();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            RefreshLobbyUI();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            RefreshLobbyUI();
        }

        public void RefreshLobbyUI()
        {
            if (lobbyUI != null)
            {
                StopAllCoroutines();
                StartCoroutine(RefreshNextFrame());
            }
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null; // Wait 1 frame
            if (lobbyUI != null) lobbyUI.UpdatePlayerList();
        }

        public bool AreAllPlayersReady()
        {
            var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
            if (players.Length < 2 && !IsVsBotMode()) return false; // Vs Bot değilse en az 2 oyuncu lazım

            foreach (var player in players)
            {
                if (!player.IsReady.Value) return false;
            }
            return true;
        }

        public void StartGame()
        {
            if (!IsServer) return;

            if (AreAllPlayersReady())
            {
                Debug.Log("All players ready! Starting game...");
                NetworkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("Not all players are ready!");
            }
        }

        private bool IsVsBotMode()
        {
            return PlayerPrefs.GetInt("BotMode", 0) == 1;
        }
    }
}
