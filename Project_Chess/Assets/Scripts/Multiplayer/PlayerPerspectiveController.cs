using Unity.Netcode;
using UnityEngine;
using AlperKocasalih.Chess.Grid;

namespace AlperKocasalih.Chess.Multiplayer
{
    public class PlayerPerspectiveController : NetworkBehaviour
    {
        private bool isCameraAssigned = false;

        public override void OnNetworkSpawn()
        {
            if (!IsLocalPlayer) return;

            TryAssignCamera();
        }

        private void Update()
        {
            if (!IsLocalPlayer || isCameraAssigned) return;

            TryAssignCamera();
        }

        private void TryAssignCamera()
        {
            if (Grid.GameManager.Instance == null) return;
            
            bool isHost = NetworkManager.Singleton.IsServer;
            
            if (Grid.GameManager.Instance.player1Camera != null && Grid.GameManager.Instance.player2Camera != null)
            {
                Grid.GameManager.Instance.player1Camera.SetActive(isHost);
                var cam1 = Grid.GameManager.Instance.player1Camera.GetComponent<Camera>();
                if (cam1 != null) cam1.tag = isHost ? "MainCamera" : "Untagged";
                
                Grid.GameManager.Instance.player2Camera.SetActive(!isHost);
                var cam2 = Grid.GameManager.Instance.player2Camera.GetComponent<Camera>();
                if (cam2 != null) cam2.tag = !isHost ? "MainCamera" : "Untagged";
                
                /*
                if (!isHost)
                {
                    // Rotate Player 2 camera to look from the opposite side
                    Transform p2Cam = Grid.GameManager.Instance.player2Camera.transform;
                    Vector3 center = Vector3.zero;
                    
                    if (GridGenerator.Instance != null)
                    {
                        center = GridGenerator.Instance.transform.position; 
                    }
                    
                    // We copy player1Camera's transform to player2Camera, then rotate it 180 degrees.
                    // This guarantees the client has the exact opposite perspective.
                    if (p2Cam != null)
                    {
                        p2Cam.position = Grid.GameManager.Instance.player1Camera.transform.position;
                        p2Cam.rotation = Grid.GameManager.Instance.player1Camera.transform.rotation;
                        p2Cam.localScale = Grid.GameManager.Instance.player1Camera.transform.localScale;

                        // Also sync FOV/Orthographic settings to ensure UI scale parity
                        Camera c1 = Grid.GameManager.Instance.player1Camera.GetComponent<Camera>();
                        Camera c2 = Grid.GameManager.Instance.player2Camera.GetComponent<Camera>();
                        if (c1 != null && c2 != null)
                        {
                            c2.fieldOfView = c1.fieldOfView;
                            c2.orthographic = c1.orthographic;
                            c2.orthographicSize = c1.orthographicSize;
                        }
                    }

                    p2Cam.RotateAround(center, Vector3.up, 180f);
                }
*/ 
                isCameraAssigned = true;
                Debug.Log($"Assigned Camera. IsHost: {isHost}");
            }
        }
    }
}
