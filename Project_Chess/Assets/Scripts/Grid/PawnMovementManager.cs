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
        public void SelectMovementCard(MovementPattern pattern)
        {
            if (gridLookup.Count == 0) InitializeGrid();
            
            CancelSelection();
            activePattern = pattern;
            currentState = SelectionState.CardSelected;

            // Highlight all pawns that could move (simplified: all pawns)
            foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
            {
                pObj.VisualHighlight(pawnHighlightColor);
                highlightedPawns.Add(pObj);
            }
            
            Debug.Log($"PawnMovementManager: Card '{pattern.patternName}' selected. Select a pawn.");
        }

        [ContextMenu("Test Pattern A")]
        public void TestPatternA() => SelectMovementCard(testPatternA);

        [ContextMenu("Test Pattern B")]
        public void TestPatternB() => SelectMovementCard(testPatternB);

        private void HandleSelection()
        {
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
            selectedPawn = null;
            activePattern = null;
            currentState = SelectionState.None;
        }

        #endregion

        #region Logic

        private void ShowValidMoves(Pawn pawn)
        {
            ClearCellHighlights();
            Vector2Int currentCoords = pawn.CurrentCell.Coordinates;
            List<Vector2Int> offsets = activePattern.GetValidOffsets(currentCoords.x);

            foreach (var offset in offsets)
            {
                Vector2Int targetCoords = currentCoords + offset;
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
            HexCell oldCell = pawn.CurrentCell;
            oldCell.IsOccupied = false;
            
            pawn.SetCell(targetCell);
            targetCell.IsOccupied = true;

            pawn.transform.DOMove(targetCell.transform.position + pawnVisualOffset, moveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => {
                    CancelSelection();
                    Debug.Log("PawnMovementManager: Move completed.");
                });
        }

        private void ExecuteCombat(Pawn attacker, Pawn defender, HexCell targetCell)
        {
            Debug.Log($"PawnMovementManager: Combat! {attacker.PawnType} vs {defender.PawnType}");
            
            defender.CurrentCell.IsOccupied = false;
            Destroy(defender.gameObject);
            
            ExecuteMove(attacker, targetCell);
        }

        private Pawn FindPawnOnCell(HexCell cell)
        {
            Pawn[] allPawns = GameObject.FindObjectsOfType<Pawn>();
            foreach (var p in allPawns)
            {
                if (p.CurrentCell == cell) return p;
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
