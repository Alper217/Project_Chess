using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid.Core
{
    public class ObstacleManager : NetworkBehaviour
    {
        public static ObstacleManager Instance { get; private set; }

        [System.Serializable]
        private struct ObstacleRecord
        {
            public Vector2Int coordinate;
            public int placedTurn;
        }

        private List<ObstacleRecord> activeObstacles = new List<ObstacleRecord>();
        private Dictionary<Vector2Int, HexCell> gridLookup;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged += HandleTurnChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged -= HandleTurnChanged;
            }
        }

        public void InitializeGridReference(Dictionary<Vector2Int, HexCell> lookup)
        {
            gridLookup = lookup;
        }

        [ServerRpc(RequireOwnership = false)]
        public void ExecuteObstaclePlacementServerRpc(Vector2Int[] worldCoords)
        {
            int currentTurn = TurnManager.Instance != null ? TurnManager.Instance.TurnCount : 1;

            foreach (var coord in worldCoords)
            {
                if (gridLookup != null && gridLookup.TryGetValue(coord, out HexCell cell) && !cell.IsOccupied)
                {
                    cell.SetObstacle(true);
                    activeObstacles.Add(new ObstacleRecord { coordinate = coord, placedTurn = currentTurn });
                }
            }
            ExecuteObstaclePlacementClientRpc(worldCoords);
            if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
        }

        [ClientRpc]
        private void ExecuteObstaclePlacementClientRpc(Vector2Int[] worldCoords)
        {
            foreach (var coord in worldCoords)
            {
                if (gridLookup != null && gridLookup.TryGetValue(coord, out HexCell cell) && !cell.IsOccupied)
                {
                    cell.SetObstacle(true);
                }
            }
        }

        private void HandleTurnChanged(int newActivePlayerID)
        {
            if (!IsServer) return;
            if (TurnManager.Instance == null) return;

            int currentTurn = TurnManager.Instance.TurnCount;
            List<Vector2Int> expiredCoords = new List<Vector2Int>();

            // An obstacle placed on Turn X expires when TurnCount > X + 3
            for (int i = activeObstacles.Count - 1; i >= 0; i--)
            {
                if (currentTurn > activeObstacles[i].placedTurn + 3)
                {
                    expiredCoords.Add(activeObstacles[i].coordinate);
                    
                    if (gridLookup != null && gridLookup.TryGetValue(activeObstacles[i].coordinate, out HexCell cell))
                    {
                        cell.SetObstacle(false);
                    }
                    activeObstacles.RemoveAt(i);
                }
            }

            if (expiredCoords.Count > 0)
            {
                RemoveObstaclesClientRpc(expiredCoords.ToArray());
            }
        }

        [ClientRpc]
        private void RemoveObstaclesClientRpc(Vector2Int[] coords)
        {
            foreach (var coord in coords)
            {
                if (gridLookup != null && gridLookup.TryGetValue(coord, out HexCell cell))
                {
                    cell.SetObstacle(false);
                }
            }
        }
    }
}
