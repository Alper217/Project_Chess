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
        [SerializeField] private Color moveHighlightColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color combatHighlightColor = Color.red;

        [Header("Test Patterns (Inspector)")]
        [SerializeField] private MovementPattern testPatternA;
        [SerializeField] private MovementPattern testPatternB;

        [Header("State")]
        [SerializeField, ReadOnly] private SelectionState currentState = SelectionState.None;
        [SerializeField, ReadOnly] private MovementPattern activePattern;
        [SerializeField, ReadOnly] private CardData activeCardData;
        [SerializeField, ReadOnly] private int activeCardRemainingUses = 1;
        [SerializeField, ReadOnly] private int initialCardUses = 1;
        [SerializeField, ReadOnly] private Pawn selectedPawn;

        private readonly List<HexCell> highlightedCells = new List<HexCell>();
        private readonly List<Pawn> highlightedPawns = new List<Pawn>();
        private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();

        public bool IsActive => currentState != SelectionState.None;
        public bool IsMultiActionInProgress => activeCardData != null && activeCardRemainingUses > 0 && activeCardRemainingUses < initialCardUses;

        public event System.Action OnSelectionCancelled;

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
            
            if (IsMultiActionInProgress)
            {
                Debug.LogWarning("Multi-action in progress. You must finish your current card's actions first.");
                return;
            }

            CancelSelection();
            activeCardData = card;
            activePattern = card.pattern;
            
            activeCardRemainingUses = 1;
            if (card.runtimeBuffs != null)
            {
                foreach(var buff in card.runtimeBuffs)
                {
                    if (buff != null && buff.effectType == EffectType.DoubleUse)
                    {
                        activeCardRemainingUses = 2; // Or buff.amount if we want variable uses
                    }
                }
            }
            initialCardUses = activeCardRemainingUses;

            if (card.isObstacleCard)
            {
                if (card.obstaclePattern == null)
                {
                    Debug.LogWarning($"PlayerInputController: Obstacle Card '{card.cardName}' has no ObstaclePattern assigned.");
                    CancelSelection();
                    return;
                }
                currentState = SelectionState.ObstacleTargeting;
                Debug.Log($"PlayerInputController: Obstacle Card '{card.cardName}' selected. Select an empty cell to place the pattern.");
            }
            else
            {
                if (card.pattern == null)
                {
                    Debug.LogWarning($"PlayerInputController: Card '{card.cardName}' has no MovementPattern assigned.");
                    CancelSelection();
                    return;
                }
                currentState = SelectionState.CardSelected;
                int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;

                foreach (var pObj in GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None))
                {
                    if (pObj.PlayerID != localPlayerID || pObj.HasStun()) continue;
                    
                    SetPawnLayer(pObj.gameObject, "Outline_Selectable");
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

            foreach (var pObj in GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None))
            {
                if (pObj.PlayerID != localPlayerID) continue;
                
                SetPawnLayer(pObj.gameObject, "Outline_Selectable");
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
                if (cell == null) 
                {
                    Debug.Log($"PlayerInputController: Hit {hit.collider.name} but no HexCell found.");
                    return;
                }

                Debug.Log($"PlayerInputController: Selected cell {cell.Coordinates} in state {currentState}");

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
            else
            {
                Debug.Log("PlayerInputController: Raycast missed cellLayer.");
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

                if (pawn.PlayerID != localPlayerID || pawn.HasStun()) return;

                if (pawn.HasStun())
                {
                    Debug.Log("Pawn is stunned and cannot move or attack!");
                    return;
                }

                selectedPawn = pawn;
                currentState = SelectionState.PawnSelected;
                
                ClearPawnHighlights();
                SetPawnLayer(selectedPawn.gameObject, "Outline");

                MovementPattern resolvedPattern = ResolveMovementPatternForPawn(selectedPawn);
                if (resolvedPattern == null)
                {
                    Debug.LogWarning("PlayerInputController: No MovementPattern resolved for selected pawn.");
                    CancelSelection();
                    return;
                }
                activePattern = resolvedPattern;

                ShowValidMoves(selectedPawn);
            }
        }

        private void HandleCellSelection(HexCell cell)
        {
            if (highlightedCells.Contains(cell))
            {
                Pawn enemy = FindPawnOnCell(cell);

                // Determine if this action should end the turn
                bool shouldEndTurn = true;
                if (activeCardData != null)
                {
                    // If we have a card with multiple uses, only end turn on the last use
                    shouldEndTurn = (activeCardRemainingUses <= 1);
                }

                if (enemy != null)
                {
                    if (selectedPawn != null && enemy.PlayerID == selectedPawn.PlayerID)
                    {
                        Debug.LogWarning("PlayerInputController: Friendly pawn selected as target. Move ignored.");
                        return;
                    }
                    AttackHandler attackHandler = selectedPawn != null ? selectedPawn.GetComponent<AttackHandler>() : null;
                    if (attackHandler != null && !attackHandler.CanAttack())
                    {
                        Debug.Log("PlayerInputController: Attack on cooldown. Action ignored.");
                        return;
                    }
                    if (Core.PawnActionExecutor.Instance != null)
                    {
                        int cardIndex = -1;
                        if (activeCardData != null && DeckManager.Instance != null)
                        {
                            cardIndex = DeckManager.Instance.GetCardIndex(activeCardData);
                        }
                        if (cardIndex >= 0)
                        {
                            Core.PawnActionExecutor.Instance.ApplyCardEffectServerRpc(selectedPawn.NetworkObjectId, cardIndex);
                        }

                        Core.PawnActionExecutor.Instance.ExecuteCombatServerRpc(selectedPawn.NetworkObjectId, enemy.NetworkObjectId, cell.Coordinates, shouldEndTurn);
                    }
                }
                else
                {
                    if (Core.PawnActionExecutor.Instance != null)
                    {
                        int cardIndex = -1;
                        if (activeCardData != null && DeckManager.Instance != null)
                        {
                            cardIndex = DeckManager.Instance.GetCardIndex(activeCardData);
                        }
                        if (cardIndex >= 0)
                        {
                            Core.PawnActionExecutor.Instance.ApplyCardEffectServerRpc(selectedPawn.NetworkObjectId, cardIndex);
                        }
                        Core.PawnActionExecutor.Instance.ExecuteMoveServerRpc(selectedPawn.NetworkObjectId, cell.Coordinates, shouldEndTurn);
                    }
                }
                
                // Manage Card Uses
                if (activeCardData != null)
                {
                    activeCardRemainingUses--;
                    if (activeCardRemainingUses <= 0)
                    {
                        if (DraftManager.Instance != null)
                        {
                            int localPlayerID = 1;
                            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                            {
                                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                            }
                            DraftManager.Instance.RemoveCardFromHand(localPlayerID, activeCardData);
                        }
                        
                        ClearCellHighlights();
                        CancelSelection();
                    }
                    else
                    {
                        // Double Use: Reset pawn selection to allow selecting another pawn
                        Debug.Log("Tanrının Eli aktif: Kart halen elde, başka bir birim seçin.");
                        
                        int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                        SetPawnLayer(selectedPawn.gameObject, "Default");
                        
                        selectedPawn = null;
                        currentState = SelectionState.CardSelected;
                        ClearCellHighlights();
                        
                        foreach (var pObj in GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None))
                        {
                            if (pObj.PlayerID == localPlayerID)
                            {
                                SetPawnLayer(pObj.gameObject, "Outline_Selectable");
                                highlightedPawns.Add(pObj);
                            }
                        }

                        if (HandUI.Instance != null)
                        {
                            HandUI.Instance.SetHandVisibility(true);
                        }
                    }
                }
                else
                {
                    ClearCellHighlights();
                    CancelSelection();
                }
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
            if (IsMultiActionInProgress)
            {
                // During multi-action, Escape only clears the pawn selection, not the card
                ClearCellHighlights();
                ClearPawnHighlights();
                if (selectedPawn != null) SetPawnLayer(selectedPawn.gameObject, "Default");
                selectedPawn = null;
                currentState = SelectionState.CardSelected;
                
                // Redraw outlines for selectable pawns
                int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
                foreach (var pObj in GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None))
                {
                    if (pObj.PlayerID == localPlayerID && !pObj.HasStun())
                    {
                        SetPawnLayer(pObj.gameObject, "Outline_Selectable");
                        highlightedPawns.Add(pObj);
                    }
                }
                return;
            }

            ClearCellHighlights();
            ClearPawnHighlights();
            if (selectedPawn != null) 
            {
                SetPawnLayer(selectedPawn.gameObject, "Default");
            }
            selectedPawn = null;
            activePattern = null;
            activeCardData = null;
            initialCardUses = 1;
            activeCardRemainingUses = 1;
            currentState = SelectionState.None;
            OnSelectionCancelled?.Invoke();
        }

        #endregion

        #region Logic

        private void ShowValidMoves(Pawn pawn)
        {
            ClearCellHighlights();
            Vector2Int currentCoords = pawn.OccupiedCell.Coordinates;
            
            if (activePattern == null) return;
            
            int rangeMod = pawn.GetMovementRangeModifier();
            List<Vector2Int> offsets = activePattern.GetValidOffsets(currentCoords, pawn.PlayerID == 2, rangeMod);

            if (offsets == null || offsets.Count == 0) return;

            Vector3Int startCube = HexGridMath.OffsetToCube(currentCoords);

            HighlightMovementTargets(pawn, currentCoords, startCube, offsets);
            HighlightAttackTargets(pawn, currentCoords, startCube);
        }

        private void HighlightMovementTargets(Pawn pawn, Vector2Int currentCoords, Vector3Int startCube, List<Vector2Int> offsets)
        {
            bool ignoresObstacles = pawn.PawnData.type == Type.Ninja;
            foreach (var offset in offsets)
            {
                Vector2Int targetCoords = currentCoords + offset;
                if (!ignoresObstacles && IsPathBlocked(startCube, targetCoords)) continue;

                if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
                {
                    if (!ignoresObstacles && targetCell.IsObstacle) continue;
                    
                    Pawn occupant = FindPawnOnCell(targetCell);
                    if (occupant != null)
                    {
                        if (occupant.PlayerID != pawn.PlayerID)
                        {
                            // If forceAttackPattern is true, don't allow attacking through movement blocks
                            if (!pawn.forceAttackPattern.Value)
                            {
                                AddClickableCell(targetCell);
                            }
                        }
                    }
                    else
                    {
                        HighlightCell(targetCell, moveHighlightColor);
                    }
                }
            }
        }

        private void HighlightAttackTargets(Pawn pawn, Vector2Int currentCoords, Vector3Int startCube)
        {
            if (pawn == null || pawn.PawnData == null) return;

            MovementPattern attackPattern = pawn.PawnData.attackPattern;
            if (attackPattern == null) return;

            List<Vector2Int> attackOffsets = attackPattern.GetValidOffsets(currentCoords, pawn.PlayerID == 2);
            if (attackOffsets == null || attackOffsets.Count == 0) return;

            foreach (var offset in attackOffsets)
            {
                Vector2Int targetCoords = currentCoords + offset;
                if (IsPathBlocked(startCube, targetCoords)) continue;

                if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
                {
                    Pawn occupant = FindPawnOnCell(targetCell);
                    if (occupant != null && occupant.PlayerID != pawn.PlayerID)
                    {
                        AddClickableCell(targetCell);
                    }
                }
            }
        }

        private bool IsPathBlocked(Vector3Int startCube, Vector2Int targetCoords)
        {
            Vector3Int targetCube = HexGridMath.OffsetToCube(targetCoords);
            int dist = HexGridMath.CubeDistance(startCube, targetCube);

            for (int i = 1; i <= dist; i++)
            {
                Vector3 cubeFloat = HexGridMath.CubeLerp(startCube, targetCube, 1f / dist * i);
                Vector3Int cubePoint = HexGridMath.CubeRound(cubeFloat);
                Vector2Int pathCoord = HexGridMath.CubeToOffset(cubePoint);

                if (gridLookup.TryGetValue(pathCoord, out HexCell pathCell))
                {
                    if (pathCell.IsObstacle)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private void HighlightCell(HexCell cell, Color color)
        {
            if (cell == null) return;
            if (!highlightedCells.Contains(cell))
            {
                highlightedCells.Add(cell);
            }
            cell.Highlight(color);
        }

        private void AddClickableCell(HexCell cell)
        {
            if (cell == null) return;
            if (!highlightedCells.Contains(cell))
            {
                highlightedCells.Add(cell);
            }
        }

        private MovementPattern ResolveMovementPatternForPawn(Pawn pawn)
        {
            if (activeCardData == null) return activePattern;
            if (activeCardData.isObstacleCard) return null;

            if (pawn == null || pawn.PawnData == null)
            {
                return activeCardData.pattern;
            }

            bool isMatch = pawn.PawnData.type == activeCardData.pawnClass;
            return isMatch ? activeCardData.pattern : activeCardData.mismatchPattern;
        }
        
        // Network Actions moved to PawnActionExecutor
        // CheckWinCondition moved to GameManager

        private Pawn FindPawnOnCell(HexCell cell)
        {
            Pawn[] allPawns = GameObject.FindObjectsByType<Pawn>(FindObjectsSortMode.None);
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
            foreach (var p in highlightedPawns)
            {
                if (p != null && selectedPawn != p)
                {
                    SetPawnLayer(p.gameObject, "Default");
                }
            }
            highlightedPawns.Clear();
        }

        private void SetPawnLayer(GameObject pawnObj, string layerName)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex == -1) layerIndex = 0;

            pawnObj.layer = layerIndex;
            foreach (Transform child in pawnObj.transform)
            {
                child.gameObject.layer = layerIndex;
            }
        }

        #endregion
    }
}

