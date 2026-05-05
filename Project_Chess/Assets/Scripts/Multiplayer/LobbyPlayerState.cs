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

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                // İleride Steam entegrasyonu geldiğinde buradan isim çekilecek.
                string defaultName = IsHost ? "Host Player" : "Client Player";
                PlayerName.Value = defaultName;
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
