using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Player List UI")]
        [SerializeField] private GameObject playerEntryPrefab;
        [SerializeField] private Transform player1Slot; // Yeni: 1. Oyuncu yeri
        [SerializeField] private Transform player2Slot; // Yeni: 2. Oyuncu yeri

        [Header("Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI roomCodeText;

        [Header("Ready Button States")]
        [SerializeField] private string readyText = "READY";
        [SerializeField] private string notReadyText = "NOT READY";
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;

        private void OnEnable()
        {
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            AlperKocasalih.Chess.Grid.LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayerState>();
                if (localPlayer != null)
                {
                    UpdateReadyButtonUI(localPlayer.IsReady.Value);
                    return;
                }
            }
            UpdateReadyButtonUI(false);
        }

        private void Start()
        {
            if (readyButton != null)
            {
                readyButton.onClick.AddListener(OnReadyClicked);
                UpdateReadyButtonUI(false);
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
                startButton.gameObject.SetActive(NetworkManager.Singleton.IsServer);
            }

            if (roomCodeText != null)
            {
                roomCodeText.text = $"ROOM CODE: {NetworkBootstrap.JoinCode}";
            }

            UpdatePlayerList();
        }

        private void OnReadyClicked()
        {
            if (NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null)
            {
                Debug.LogWarning("Local player object not found yet!");
                return;
            }

            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayerState>();
            if (localPlayer != null)
            {
                localPlayer.ToggleReady();
                UpdateReadyButtonUI(localPlayer.IsReady.Value);
            }
        }

        private void OnStartClicked()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.StartGame();
            }
        }

        public void UpdatePlayerList()
        {
            // Önce slotların içini temizle
            if (player1Slot != null) foreach (Transform child in player1Slot) Destroy(child.gameObject);
            if (player2Slot != null) foreach (Transform child in player2Slot) Destroy(child.gameObject);

            // Oyuncuları bul
            var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
            
            for (int i = 0; i < players.Length; i++)
            {
                // Hangi slot kullanılacak?
                Transform targetSlot = (i == 0) ? player1Slot : player2Slot;
                if (targetSlot == null) continue;

                GameObject entry = Instantiate(playerEntryPrefab); 
                entry.transform.SetParent(targetSlot, false);
                
                RectTransform rect = entry.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.anchoredPosition = Vector2.zero; // Slot'un tam ortasına yerleş
                    rect.localPosition = Vector3.zero;
                }

                var entryUI = entry.GetComponent<LobbyPlayerEntryUI>();
                if (entryUI != null)
                {
                    entryUI.SetPlayer(players[i]);
                    
                    // Capture local index/variable to be safe
                    var playerState = players[i];
                    playerState.IsReady.OnValueChanged += (oldVal, newVal) => {
                        UpdateStartButton();
                        if (playerState.IsOwner)
                        {
                            UpdateReadyButtonUI(newVal);
                        }
                    };
                }

                if (players[i].IsOwner)
                {
                    UpdateReadyButtonUI(players[i].IsReady.Value);
                }
            }

            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            if (NetworkManager.Singleton.IsServer && startButton != null)
            {
                startButton.interactable = LobbyManager.Instance.AreAllPlayersReady();
            }
        }

        private void UpdateReadyButtonUI(bool isReady)
        {
            if (readyButton == null) return;
            
            var text = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = AlperKocasalih.Chess.Grid.LocalizationManager.GetTranslation(isReady ? "READY" : "NOT READY");
                readyButton.image.color = isReady ? readyColor : notReadyColor;
            }
        }
    }
}
