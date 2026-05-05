using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
            
        public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<ulong> PlayerSteamId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                // Steam açıksa gerçek ismi çek, değilse varsayılan ata
                string finalName = (SteamManager.Instance != null && SteamManager.Instance.IsSteamRunning) 
                    ? SteamManager.Instance.PlayerName 
                    : (IsHost ? "Host Player" : "Client Player");
                
                PlayerName.Value = finalName;

                if (SteamManager.Instance != null && SteamManager.Instance.IsSteamRunning)
                {
                    PlayerSteamId.Value = SteamManager.Instance.PlayerSteamId;
                }
            }
            
            // Lobi UI'sını güncellemek için LobbyManager'a haber ver
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.RefreshLobbyUI();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.RefreshLobbyUI();
            }
        }

        public void ToggleReady()
        {
            if (IsOwner)
            {
                IsReady.Value = !IsReady.Value;
                Debug.Log($"Player {PlayerName.Value} ready status: {IsReady.Value}");
            }
        }
    }
}
