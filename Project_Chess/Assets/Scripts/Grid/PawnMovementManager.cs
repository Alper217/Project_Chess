using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace AlperKocasalih.Chess.Grid
{
    public class PawnMovementManager : MonoBehaviour
    {
        public static PawnMovementManager Instance { get; private set; }
        public enum SelectionState { None, CardSelected, PawnSelected }

        #region Fields

        [Header("Settings")]
        [SerializeField] private LayerMask cellLayer;
        [SerializeField] private Color pawnHighlightColor = Color.cyan;
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
        private Camera mainCamera;

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
            mainCamera = Camera.main;
            // gridLookup will be initialized when needed or on start
            InitializeGrid();
        }

        private void Update()
        {
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

        /// <summary>
        /// Simulates card selection. Call this from UI buttons.
        /// </summary>
        public void SelectMovementCard(CardData card)
        {
            if (gridLookup.Count == 0) InitializeGrid();
            
            CancelSelection();
            activeCardData = card;
            activePattern = card.pattern;
            currentState = SelectionState.CardSelected;

            // Highlight all pawns that could move (simplified: all pawns)
            foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
            {
                if (TurnManager.Instance != null && pObj.PlayerID != TurnManager.Instance.ActivePlayerID) continue;
                
                pObj.VisualHighlight(pawnHighlightColor);
                highlightedPawns.Add(pObj);
            }
            
            Debug.Log($"PawnMovementManager: Card '{card.cardName}' selected. Select a pawn.");
        }

        public void SelectMovementPattern(MovementPattern pattern)
        {
            if (gridLookup.Count == 0) InitializeGrid();
            
            CancelSelection();
            activeCardData = null; // No card associated with test patterns
            activePattern = pattern;
            currentState = SelectionState.CardSelected;

            // Highlight all pawns that could move (simplified: all pawns)
            foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
            {
                if (TurnManager.Instance != null && pObj.PlayerID != TurnManager.Instance.ActivePlayerID) continue;
                
                pObj.VisualHighlight(pawnHighlightColor);
                highlightedPawns.Add(pObj);
            }
            
            Debug.Log($"PawnMovementManager: Pattern '{pattern.patternName}' selected (Test Mode). Select a pawn.");
        }

        [ContextMenu("Test Pattern A")]
        public void TestPatternA() => SelectMovementPattern(testPatternA);

        [ContextMenu("Test Pattern B")]
        public void TestPatternB() => SelectMovementPattern(testPatternB);

        private void HandleSelection()
        {
            // Only allow input during ActionPhase
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.ActionPhase)
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, cellLayer))
            {
                HexCell cell = hit.collider.GetComponent<HexCell>();
                if (cell == null) return;

                if (currentState == SelectionState.CardSelected)
                {
                    HandlePawnSelection(cell);
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
                // Restrict to Active Player's pawn
                if (TurnManager.Instance != null && pawn.PlayerID != TurnManager.Instance.ActivePlayerID)
                {
                    Debug.Log($"PawnMovementManager: It's Player {TurnManager.Instance.ActivePlayerID}'s turn!");
                    return;
                }

                selectedPawn = pawn;
                currentState = SelectionState.PawnSelected;
                
                // Clear pawn highlights, highlight selected one
                ClearPawnHighlights();
                selectedPawn.VisualHighlight(Color.yellow);
                
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
                    ExecuteCombat(selectedPawn, enemy, cell);
                }
                else
                {
                    ExecuteMove(selectedPawn, cell);
                }
            }
            else
            {
                CancelSelection();
            }
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

            foreach (var offset in offsets)
            {
                Vector2Int finalOffset = offset;
                if (pawn.PlayerID == 2)
                {
                    finalOffset = GetMirroredOffset(offset, currentCoords.x);
                }

                Vector2Int targetCoords = currentCoords + finalOffset;
                if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
                {
                    highlightedCells.Add(targetCell);
                    
                    Pawn occupant = FindPawnOnCell(targetCell);
                    if (occupant != null)
                    {
                        if (occupant.PlayerID != pawn.PlayerID)
                            targetCell.Highlight(combatHighlightColor);
                    }
                    else
                    {
                        targetCell.Highlight(moveHighlightColor);
                    }
                }
            }
        }

        private void ExecuteMove(Pawn pawn, HexCell targetCell)
        {
            // Clear highlights immediately so the UI feels responsive
            ClearCellHighlights();
            pawn.ResetHighlight();

            HexCell oldCell = pawn.OccupiedCell;
            oldCell.IsOccupied = false;
            
            pawn.SetCell(targetCell);
            targetCell.IsOccupied = true;

            pawn.transform.DOMove(targetCell.transform.position + pawnVisualOffset, moveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    if (activeCardData != null && DraftManager.Instance != null && TurnManager.Instance != null)
                    {
                        DraftManager.Instance.RemoveCardFromHand(TurnManager.Instance.ActivePlayerID, activeCardData);
                    }
                    
                    CancelSelection();
                    if (TurnManager.Instance != null) TurnManager.Instance.NextTurn();
                    Debug.Log("PawnMovementManager: Move completed and turn ended.");
                });
        }

        private void ExecuteCombat(Pawn attacker, Pawn defender, HexCell targetCell)
        {
            Debug.Log($"PawnMovementManager: Combat! {attacker.PawnType} vs {defender.PawnType}");
            
            int loserID = defender.PlayerID;
            defender.OccupiedCell.IsOccupied = false;
            Destroy(defender.gameObject);
            
            ExecuteMove(attacker, targetCell);

            // Check if loser has any pawns left (ignoring the one just destroyed)
            CheckWinCondition(loserID, defender);
        }

        private Vector2Int GetMirroredOffset(Vector2Int offset, int startingQ)
        {
            // Vertical reflection in Odd-Q Hex Grid mapping:
            // 1. Horizontal offset (dq) stays the same.
            // 2. Vertical offset (dr) is negated.
            // 3. If dq is odd, the staggering shift depends on the starting column parity.
            
            int newDQ = offset.x;
            int newDR = -offset.y;

            if (Mathf.Abs(newDQ) % 2 != 0)
            {
                // If we cross an odd number of columns, we must account for the staggering flip
                if (startingQ % 2 == 0) newDR -= 1; // Even -> Odd shift in mirror is Down
                else newDR += 1;                  // Odd -> Even shift in mirror is Up
            }

            return new Vector2Int(newDQ, newDR);
        }

        private void CheckWinCondition(int loserID, Pawn ignoringPawn = null)
        {
            Pawn[] allPawns = GameObject.FindObjectsOfType<Pawn>();
            bool hasPawnsLeft = false;
            foreach (var p in allPawns)
            {
                // Unity's Destroy doesn't happen instantly, so we need to skip the pawn being destroyed
                if (p != null && p != ignoringPawn && p.PlayerID == loserID)
                {
                    hasPawnsLeft = true;
                    break;
                }
            }

            if (!hasPawnsLeft)
            {
                int winnerID = loserID == 1 ? 2 : 1;
                Debug.Log($"PawnMovementManager: Player {winnerID} wins!");
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
    }
}
