using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid.Utils;

namespace AlperKocasalih.Chess.Grid
{
    public class PlayerInputController : NetworkBehaviour
    {
        public static PlayerInputController Instance { get; private set; }
        public enum SelectionState { None, CardSelected, PawnSelected, ObstacleTargeting }

        #region Fields

        [Header("Settings")]
        [SerializeField] private LayerMask cellLayer;
        [SerializeField] private Material pawnHighlightMat;
        [SerializeField] private Material selectedPawnHighlightMat;
        [SerializeField] private Color moveHighlightColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color combatHighlightColor = Color.red;

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

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
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

            if (Core.ObstacleManager.Instance != null)
            {
                Core.ObstacleManager.Instance.InitializeGridReference(gridLookup);
            }
            if (Core.PawnActionExecutor.Instance != null)
            {
                Core.PawnActionExecutor.Instance.InitializeGridReference(gridLookup);
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
                Debug.Log($"PlayerInputController: Obstacle Card '{card.cardName}' selected. Select an empty cell to place the pattern.");
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
                
                Debug.Log($"PlayerInputController: Card '{card.cardName}' selected. Select a pawn.");
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
                        Debug.LogWarning("PlayerInputController: Friendly pawn selected as target. Move ignored.");
                        return;
                    }
                    if (Core.PawnActionExecutor.Instance != null)
                    {
                        Core.PawnActionExecutor.Instance.ExecuteCombatServerRpc(selectedPawn.NetworkObjectId, enemy.NetworkObjectId, cell.Coordinates);
                    }
                }
                else
                {
                    if (Core.PawnActionExecutor.Instance != null)
                    {
                        Core.PawnActionExecutor.Instance.ExecuteMoveServerRpc(selectedPawn.NetworkObjectId, cell.Coordinates);
                    }
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
                Debug.Log("PlayerInputController: Cannot place obstacle with its center on an occupied or obstructed cell.");
                return; // Optionally we could still allow placement but not on center. For now, strict center empty.
            }

            int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            List<Vector2Int> localOffsets = activeCardData.obstaclePattern.GetObstacleOffsets(cell.Coordinates.x);
            
            bool isPlayer2 = (localPlayerID == 2);
            List<Vector2Int> absoluteWorldOffsets = HexGridMath.GenerateAccurateWorldOffsetsFromPattern(cell.Coordinates, localOffsets, isPlayer2);

            // Execute placement over network via ObstacleManager
            if (Core.ObstacleManager.Instance != null)
            {
                Core.ObstacleManager.Instance.ExecuteObstaclePlacementServerRpc(absoluteWorldOffsets.ToArray());
            }

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

            Vector3Int startCube = HexGridMath.OffsetToCube(currentCoords);

            foreach (var offset in offsets)
            {
                Vector2Int finalOffset = offset;
                if (pawn.PlayerID == 2)
                {
                    finalOffset = HexGridMath.GetMirroredOffset(offset, currentCoords.x);
                }

                Vector2Int targetCoords = currentCoords + finalOffset;
                
                // --- LINE OF SIGHT CHECK ---
                bool isBlocked = false;
                Vector3Int targetCube = HexGridMath.OffsetToCube(targetCoords);
                int dist = HexGridMath.CubeDistance(startCube, targetCube);
                
                for (int i = 1; i <= dist; i++)
                {
                    // LERP to find all hexes exactly on the line
                    Vector3 cubeFloat = HexGridMath.CubeLerp(startCube, targetCube, 1f / dist * i);
                    Vector3Int cubePoint = HexGridMath.CubeRound(cubeFloat);
                    Vector2Int pathCoord = HexGridMath.CubeToOffset(cubePoint);
                    
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
        
        // Network Actions moved to PawnActionExecutor
        // CheckWinCondition moved to GameManager

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
    }
}

