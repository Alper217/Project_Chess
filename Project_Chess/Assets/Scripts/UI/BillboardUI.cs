using UnityEngine;

namespace AlperKocasalih.Chess.UI
{
    /// <summary>
    /// Forces the object to always face the camera. 
    /// Useful for World Space UI elements like damage popups.
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                // Align rotation with camera rotation to fix "reversed" or "mirrored" issues
                transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
