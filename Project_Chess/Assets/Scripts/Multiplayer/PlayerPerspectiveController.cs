using Unity.Netcode;
using UnityEngine;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class PlayerPerspectiveController : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsLocalPlayer) return;

            // Player IDs are usually 0 for Host, 1 for Client in simple setups
            // Or we check NetworkManager.Singleton.LocalClientId
            
            // If we are Player 2 (Client), rotate camera
            if (NetworkManager.Singleton.LocalClientId > 0)
            {
                RotateCameraForPlayer2();
            }
        }

        private void RotateCameraForPlayer2()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.Log("Rotating camera for Player 2 perspective.");
                mainCam.transform.rotation = Quaternion.Euler(45, 180, 0); 
                // Note: Adjust 45 based on your original camera angle. 
                // Default often is (45, 0, 0)
            }
        }
    }
}
