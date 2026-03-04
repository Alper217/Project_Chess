using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class PawnMovementManager : NetworkBehaviour
    {
        public static PawnMovementManager Instance { get; private set; }
        public enum SelectionState { None, CardSelected, PawnSelected, ObstacleTargeting }

        #region Fields

        [Header("Settings")]
        [SerializeField] private LayerMask cellLayer;
        [SerializeField] private Material pawnHighlightMat;
        [SerializeField] private Material selectedPawnHighlightMat;
        [SerializeField] private Color moveHighlightColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color combatHighlightColor = Color.red;
        [SerializeField] private float moveDuration = 0.5f;
        [SerializeField] private Vector3 pawnVisualOffset = new Vector3(0, 0.5f, 0);

        [Header("Test Patterns (Inspector)")]
        [SerializeField] private MovementPattern testPatternA;
        [SerializeField] private MovementPattern testPatternB;

        [Header("State")]
        [SerializeField, ReadOnly] private SelectionState currentState = SelectionState.None;
        [SerializeField, ReadOnly] private MovementPattern activePattern;
        [SerializeField, ReadOnly] private CardData activeCardData;
        [SerializeField, ReadOnly] private Pawn selectedPawn;

        private List<HexCell> highlightedCells = new List<HexCell>();
        private List<Pawn> highlightedPawns = new List<Pawn>();
        private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();

        [System.Serializable]
        private struct ObstacleRecord
        {
            public Vector2Int coordinate;
            public int placedTurn;
        }
        private List<ObstacleRecord> activeObstacles = new List<ObstacleRecord>();

        public bool IsActive => currentState != SelectionState.None;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            InitializeGrid();
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

        private void Update()
        {
            if (!IsSpawned) return;

            // Only allow interaction if it's our turn
            if (TurnManager.Instance != null)
            {
                int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                if (TurnManager.Instance.ActivePlayerID != localPlayerID) return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleSelection();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
            }
        }

        #endregion

        #region Initialization

        public void InitializeGrid()
        {
            if (GridGenerator.Instance == null) return;
            
            gridLookup.Clear();
            foreach (var hex in GridGenerator.Instance.SpawnedHexes)
            {
                HexCell cell = hex.GetComponent<HexCell>();
                if (cell != null) gridLookup[cell.Coordinates] = cell;
            }
        }

        #endregion

        #region State Management

        public void SelectMovementCard(CardData card)
        {
            if (gridLookup.Count == 0) InitializeGrid();
            
            CancelSelection();
            activeCardData = card;
            activePattern = card.pattern;

            if (card.isObstacleCard)
            {
                currentState = SelectionState.ObstacleTargeting;
                Debug.Log($"PawnMovementManager: Obstacle Card '{card.cardName}' selected. Select an empty cell to place the pattern.");
            }
            else
            {
                currentState = SelectionState.CardSelected;
                int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;

                foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
                {
                    if (pObj.PlayerID != localPlayerID) continue;
                    
                    pObj.VisualHighlight(pawnHighlightMat);
                    highlightedPawns.Add(pObj);
                }
                
                Debug.Log($"PawnMovementManager: Card '{card.cardName}' selected. Select a pawn.");
            }
        }

        public void SelectMovementPattern(MovementPattern pattern)
        {
            if (gridLookup.Count == 0) InitializeGrid();
            
            CancelSelection();
            activeCardData = null; 
            activePattern = pattern;
            currentState = SelectionState.CardSelected;

            int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;

            foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
            {
                if (pObj.PlayerID != localPlayerID) continue;
                
                pObj.VisualHighlight(pawnHighlightMat);
                highlightedPawns.Add(pObj);
            }
        }

        private void HandleSelection()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.ActionPhase)
                return;

            if (Camera.main == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            // Check UI blocks before placing/selecting etc.
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) 
                return;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, cellLayer))
            {
                HexCell cell = hit.collider.GetComponent<HexCell>();
                if (cell == null) return;

                if (currentState == SelectionState.CardSelected)
                {
                    HandlePawnSelection(cell);
                }
                else if (currentState == SelectionState.ObstacleTargeting)
                {
                    HandleObstaclePlacement(cell);
                }
                else if (currentState == SelectionState.PawnSelected)
                {
                    HandleCellSelection(cell);
                }
            }
        }

        private void HandlePawnSelection(HexCell cell)
        {
            Pawn pawn = FindPawnOnCell(cell);
            if (pawn != null)
            {
                int localPlayerID = 1;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                }

                if (pawn.PlayerID != localPlayerID) return;

                selectedPawn = pawn;
                currentState = SelectionState.PawnSelected;
                
                ClearPawnHighlights();
                selectedPawn.VisualHighlight(selectedPawnHighlightMat);
                
                ShowValidMoves(selectedPawn);
            }
        }

        private void HandleCellSelection(HexCell cell)
        {
            if (highlightedCells.Contains(cell))
            {
                Pawn enemy = FindPawnOnCell(cell);
                if (enemy != null)
                {
                    if (selectedPawn != null && enemy.PlayerID == selectedPawn.PlayerID)
                    {
                        Debug.LogWarning("PawnMovementManager: Friendly pawn selected as target. Move ignored.");
                        return;
                    }
                    ExecuteCombatServerRpc(selectedPawn.NetworkObjectId, enemy.NetworkObjectId, cell.Coordinates);
                }
                else
                {
                    ExecuteMoveServerRpc(selectedPawn.NetworkObjectId, cell.Coordinates);
                }
                
                // Discard the played card locally (and sync via DraftManager)
                if (activeCardData != null && DraftManager.Instance != null)
                {
                    int localPlayerID = 1;
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    {
                        localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                    }
                    DraftManager.Instance.RemoveCardFromHand(localPlayerID, activeCardData);
                }

                // Clear locally immediately for responsiveness
                ClearCellHighlights();
                selectedPawn.ResetHighlight();
                CancelSelection();
            }
            else
            {
                CancelSelection();
            }
        }

        private void HandleObstaclePlacement(HexCell cell)
        {
            if (activeCardData == null || activeCardData.obstaclePattern == null) return;
            
            if (cell.IsOccupied || cell.IsObstacle)
            {
                Debug.Log("PawnMovementManager: Cannot place obstacle with its center on an occupied or obstructed cell.");
                return; // Optionally we could still allow placement but not on center. For now, strict center empty.
            }

            int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            List<Vector2Int> localOffsets = activeCardData.obstaclePattern.GetObstacleOffsets(cell.Coordinates.x);
            
            bool isPlayer2 = (localPlayerID == 2);
            List<Vector2Int> absoluteWorldOffsets = GenerateAccurateWorldOffsetsFromPattern(cell.Coordinates, localOffsets, isPlayer2);

            // Execute placement over network
            ExecuteObstaclePlacementServerRpc(absoluteWorldOffsets.ToArray());

            // Discard card
            if (activeCardData != null && DraftManager.Instance != null)
            {
                DraftManager.Instance.RemoveCardFromHand(localPlayerID, activeCardData);
            }

            CancelSelection();
        }

        private void CancelSelection()
        {
            ClearCellHighlights();
            ClearPawnHighlights();
            if (selectedPawn != null) selectedPawn.ResetHighlight();
            selectedPawn = null;
            activePattern = null;
            activeCardData = null;
            currentState = SelectionState.None;
        }

        #endregion

        #region Logic

        private void ShowValidMoves(Pawn pawn)
        {
            ClearCellHighlights();
            Vector2Int currentCoords = pawn.OccupiedCell.Coordinates;
            List<Vector2Int> offsets = activePattern.GetValidOffsets(currentCoords.x);

            Vector3Int startCube = OffsetToCube(currentCoords);

            foreach (var offset in offsets)
            {
                Vector2Int finalOffset = offset;
                if (pawn.PlayerID == 2)
                {
                    finalOffset = GetMirroredOffset(offset, currentCoords.x);
                }

                Vector2Int targetCoords = currentCoords + finalOffset;
                
                // --- LINE OF SIGHT CHECK ---
                bool isBlocked = false;
                Vector3Int targetCube = OffsetToCube(targetCoords);
                int dist = CubeDistance(startCube, targetCube);
                
                for (int i = 1; i <= dist; i++)
                {
                    // LERP to find all hexes exactly on the line
                    Vector3 cubeFloat = CubeLerp(startCube, targetCube, 1f / dist * i);
                    Vector3Int cubePoint = CubeRound(cubeFloat);
                    Vector2Int pathCoord = CubeToOffset(cubePoint);
                    
                    if (gridLookup.TryGetValue(pathCoord, out HexCell pathCell))
                    {
                        if (pathCell.IsObstacle)
                        {
                            isBlocked = true;
                            break;
                        }
                    }
                    else
                    {
                        isBlocked = true; // Path goes out of bounds
                        break;
                    }
                }
                
                if (isBlocked) continue; // Skip to next target option
                // --------------------------

                if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
                {
                    Pawn occupant = FindPawnOnCell(targetCell);
                    if (occupant != null)
                    {
                        if (occupant.PlayerID != pawn.PlayerID)
                        {
                            highlightedCells.Add(targetCell);
                            targetCell.Highlight(combatHighlightColor);
                        }
                    }
                    else
                    {
                        highlightedCells.Add(targetCell);
                        targetCell.Highlight(moveHighlightColor);
                    }
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExecuteMoveServerRpc(ulong pawnNetworkID, Vector2Int targetCoords)
        {
            NetworkObject pawnObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pawnNetworkID];
            if (pawnObj == null) return;

            Pawn pawn = pawnObj.GetComponent<Pawn>();
            if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
            {
                HexCell oldCell = pawn.OccupiedCell;
                if (oldCell != null) oldCell.IsOccupied = false;
                
                pawn.SetCell(targetCell);
                targetCell.IsOccupied = true;
                
                ExecuteMoveClientRpc(pawnNetworkID, targetCoords);
                
                if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
            }
        }

        [ClientRpc]
        private void ExecuteMoveClientRpc(ulong pawnNetworkID, Vector2Int targetCoords)
        {
            NetworkObject pawnObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pawnNetworkID];
            if (pawnObj == null) return;

            Pawn pawn = pawnObj.GetComponent<Pawn>();
            if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
            {
                HexCell oldCell = pawn.OccupiedCell;
                if (oldCell != null) oldCell.IsOccupied = false;
                
                pawn.SetCell(targetCell);
                targetCell.IsOccupied = true;

                pawn.transform.DOMove(targetCell.transform.position + pawnVisualOffset, moveDuration)
                    .SetEase(Ease.OutQuad);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExecuteObstaclePlacementServerRpc(Vector2Int[] worldCoords)
        {
            int currentTurn = TurnManager.Instance != null ? TurnManager.Instance.TurnCount : 1;

            foreach (var coord in worldCoords)
            {
                if (gridLookup.TryGetValue(coord, out HexCell cell) && !cell.IsOccupied)
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
                if (gridLookup.TryGetValue(coord, out HexCell cell) && !cell.IsOccupied)
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
                    
                    if (gridLookup.TryGetValue(activeObstacles[i].coordinate, out HexCell cell))
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
                if (gridLookup.TryGetValue(coord, out HexCell cell))
                {
                    cell.SetObstacle(false);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ExecuteCombatServerRpc(ulong attackerID, ulong defenderID, Vector2Int targetCoords)
        {
            NetworkObject attackerObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID];
            NetworkObject defenderObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[defenderID];

            if (attackerObj != null && defenderObj != null)
            {
                Pawn attacker = attackerObj.GetComponent<Pawn>();
                Pawn defender = defenderObj.GetComponent<Pawn>();
                if (attacker == null || defender == null) return;
                if (attacker.PlayerID == defender.PlayerID)
                {
                    Debug.LogWarning("PawnMovementManager: Friendly fire attempt blocked on server.");
                    return;
                }
                int loserID = defender.PlayerID;
                
                defenderObj.Despawn(); // This destroys it on all clients

                ExecuteMoveServerRpc(attackerID, targetCoords); // Calls ServerRpc from ServerRpc, safe
                
                // Win check on Server
                CheckWinCondition(loserID);
            }
        }

        private Vector2Int GetMirroredOffset(Vector2Int offset, int startingQ)
        {
            int newDQ = offset.x;
            int newDR = -offset.y;

            if (Mathf.Abs(newDQ) % 2 != 0)
            {
                if (startingQ % 2 == 0) newDR -= 1; 
                else newDR += 1;                  
            }

            return new Vector2Int(newDQ, newDR);
        }

        private void CheckWinCondition(int loserID)
        {
            if (!IsServer) return;

            Pawn[] allPawns = GameObject.FindObjectsOfType<Pawn>();
            bool hasPawnsLeft = false;
            foreach (var p in allPawns)
            {
                if (p != null && p.IsSpawned && p.PlayerID == loserID)
                {
                    hasPawnsLeft = true;
                    break;
                }
            }

            if (!hasPawnsLeft)
            {
                int winnerID = loserID == 1 ? 2 : 1;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.EndGame(winnerID);
                }
            }
        }

        private Pawn FindPawnOnCell(HexCell cell)
        {
            Pawn[] allPawns = GameObject.FindObjectsOfType<Pawn>();
            foreach (var p in allPawns)
            {
                if (p.OccupiedCell == cell) return p;
            }
            return null;
        }

        private void ClearCellHighlights()
        {
            foreach (var cell in highlightedCells) cell.ResetHighlight();
            highlightedCells.Clear();
        }

        private void ClearPawnHighlights()
        {
            foreach (var p in highlightedPawns) if (p != null) p.ResetHighlight();
            highlightedPawns.Clear();
        }

        #endregion

        #region Hex Math (Odd-Q / Cube)

        private List<Vector2Int> GenerateAccurateWorldOffsetsFromPattern(Vector2Int centerWorldCoords, List<Vector2Int> localPatternOffsets, bool rotate180 = false)
        {
            // The localPatternOffsets are generated from an Odd-Q grid with center (3,3).
            // Odd-Q properties means offsets depend on whether the center Q is odd or even.
            // Since the local grid has an ODD center (x=3), if the world target has an EVEN center, 
            // we must properly translate it so the shape stays consistent geometrically.
            // Using Cube Coordinates handles this flawlessly.
            
            List<Vector2Int> worldCoords = new List<Vector2Int>();
            Vector3Int worldCenterCube = OffsetToCube(centerWorldCoords);
            Vector3Int localCenterCube = OffsetToCube(new Vector2Int(3, 3)); // 3,3 is the pattern center

            foreach (var localOffsetCoord in localPatternOffsets)
            {
                Vector2Int absoluteLocalCoord = new Vector2Int(3 + localOffsetCoord.x, 3 + localOffsetCoord.y);
                Vector3Int localCube = OffsetToCube(absoluteLocalCoord);
                
                // Difference in cube space
                Vector3Int diff = localCube - localCenterCube;
                
                if (rotate180)
                {
                    // Rotating 180 degrees in hex cube coords is simply negating the cube vector coords
                    diff = new Vector3Int(-diff.x, -diff.y, -diff.z);
                }

                // Add diff to world center in cube space
                Vector3Int newWorldCube = worldCenterCube + diff;
                
                Vector2Int finalWorldOffset = CubeToOffset(newWorldCube);
                worldCoords.Add(finalWorldOffset);
            }

            return worldCoords;
        }

        private Vector3Int OffsetToCube(Vector2Int hex)
        {
            int q = hex.x;
            int r = hex.y;
            int x = q;
            int z = r - (q - (q & 1)) / 2;
            int y = -x - z;
            return new Vector3Int(x, y, z);
        }

        private Vector2Int CubeToOffset(Vector3Int cube)
        {
            int q = cube.x;
            int r = cube.z + (cube.x - (cube.x & 1)) / 2;
            return new Vector2Int(q, r);
        }

        private int CubeDistance(Vector3Int a, Vector3Int b)
        {
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
        }

        private Vector3 CubeLerp(Vector3Int a, Vector3Int b, float t)
        {
            return new Vector3(
                Mathf.Lerp(a.x, b.x, t),
                Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.z, b.z, t)
            );
        }

        private Vector3Int CubeRound(Vector3 frac)
        {
            int q = Mathf.RoundToInt(frac.x);
            int r = Mathf.RoundToInt(frac.y);
            int s = Mathf.RoundToInt(frac.z);

            float q_diff = Mathf.Abs(q - frac.x);
            float r_diff = Mathf.Abs(r - frac.y);
            float s_diff = Mathf.Abs(s - frac.z);

            if (q_diff > r_diff && q_diff > s_diff)
                q = -r - s;
            else if (r_diff > s_diff)
                r = -q - s;
            else
                s = -q - r;

            return new Vector3Int(q, r, s);
        }

        #endregion
    }
}

