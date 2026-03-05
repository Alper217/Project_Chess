using UnityEditor;
using UnityEngine;
using AlperKocasalih.Chess.Grid.Core;

public class EditorAddObstacleManager : MonoBehaviour
{
    [MenuItem("Tools/Add Grid Managers to Scene")]
    public static void AddManager()
    {
        GameObject pawnMovementManager = GameObject.Find("PawnMovementManager");
        if (pawnMovementManager != null)
        {
            if (pawnMovementManager.GetComponent<ObstacleManager>() == null)
            {
                pawnMovementManager.AddComponent<ObstacleManager>();
                Debug.Log("Added ObstacleManager to Object.");
            }
            if (pawnMovementManager.GetComponent<PawnActionExecutor>() == null)
            {
                pawnMovementManager.AddComponent<PawnActionExecutor>();
                Debug.Log("Added PawnActionExecutor to Object.");
            }
        }
        else 
        {
            Debug.LogError("Could not find PawnMovementManager in scene.");
        }
    }
}
