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
            currentState = SelectionState.CardSelected;

            int localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;

            foreach (var pObj in GameObject.FindObjectsOfType<Pawn>())
            {
                if (pObj.PlayerID != localPlayerID) continue;
                
                pObj.VisualHighlight(pawnHighlightColor);
                highlightedPawns.Add(pObj);
            }
            
            Debug.Log($"PawnMovementManager: Card '{card.cardName}' selected. Select a pawn.");
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
                
                pObj.VisualHighlight(pawnHighlightColor);
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
    }
}

