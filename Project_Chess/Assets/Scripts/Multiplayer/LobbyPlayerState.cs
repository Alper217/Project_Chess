using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            
        public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<ulong> PlayerSteamId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                // Steam açıksa gerçek ismi çek, değilse varsayılan ata
                string finalName = (SteamManager.Instance != null && SteamManager.Instance.IsSteamRunning) 
                    ? SteamManager.Instance.PlayerName 
                    : (IsHost ? "Host Player" : "Client Player");
                
                ulong finalSteamId = 0;
                if (SteamManager.Instance != null && SteamManager.Instance.IsSteamRunning)
                {
                    finalSteamId = SteamManager.Instance.PlayerSteamId;
                }

                SetPlayerDataServerRpc(finalName, finalSteamId);
            }
            
            // Lobi UI'sını güncellemek için LobbyManager'a haber ver
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.RefreshLobbyUI();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetPlayerDataServerRpc(FixedString32Bytes name, ulong steamId)
        {
            PlayerName.Value = name;
            PlayerSteamId.Value = steamId;
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
                ToggleReadyServerRpc(!IsReady.Value);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleReadyServerRpc(bool newReadyState)
        {
            IsReady.Value = newReadyState;
            Debug.Log($"Player {PlayerName.Value} ready status updated on server: {IsReady.Value}");
        }
    }
}
