using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public class PawnPlacementManager : MonoBehaviour
    {
        #region Fields

        [Header("Prefabs")]
        [SerializeField] private GameObject[] pawnPrefabs;
        [SerializeField] private int selectedPawnIndex = 0;

        [Header("Settings")]
        [SerializeField] private LayerMask cellLayer;
        [SerializeField] private float dropHeight = 5f;
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private Vector3 pawnVisualOffset = new Vector3(0, 0.5f, 0);

        private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();
        private HashSet<int> p1SpawnedTypes = new HashSet<int>(); // P1: r 0-2
        private HashSet<int> p2SpawnedTypes = new HashSet<int>(); // P2: r 7-9
        private Camera mainCamera;

        #endregion

        #region Unity Methods

        private void Start()
        {
            mainCamera = Camera.main;
            InitializeGridLookup();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandlePlacementInput();
            }

            // Quick index selection for testing (1-5 keys)
            for (int i = 0; i < Mathf.Min(pawnPrefabs.Length, 5); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    selectedPawnIndex = i;
                    Debug.Log($"PawnPlacementManager: Selected Pawn Type {i}");
                }
            }
        }

        #endregion

        #region Methods

        private void InitializeGridLookup()
        {
            if (GridGenerator.Instance == null)
            {
                Debug.LogError("PawnPlacementManager: GridGenerator instance not found!");
                return;
            }

            foreach (var hex in GridGenerator.Instance.SpawnedHexes)
            {
                HexCell cell = hex.GetComponent<HexCell>();
                if (cell != null)
                {
                    gridLookup[cell.Coordinates] = cell;
                }
            }
            
            Debug.Log($"PawnPlacementManager: Initialized lookup with {gridLookup.Count} cells.");
        }

        private void HandlePlacementInput()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            // IMPORTANT: Use cellLayer to ignore pawns or other objects blocking the cell
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, cellLayer))
            {
                HexCell cell = hit.collider.GetComponent<HexCell>();
                if (cell != null)
                {
                    TryPlacePawn(cell);
                }
            }
        }

        private void TryPlacePawn(HexCell cell)
        {
            // Validation 1: Already occupied?
            if (cell.IsOccupied)
            {
                Debug.LogWarning($"PawnPlacementManager: Cell at {cell.Coordinates} is already occupied!");
                return;
            }

            // Validation 2: Region check & Per-player Uniqueness
            int row = cell.Coordinates.y;
            bool isP1Region = row >= 0 && row <= 2;
            bool isP2Region = row >= 7 && row <= 9;

            if (isP1Region)
            {
                if (p1SpawnedTypes.Contains(selectedPawnIndex))
                {
                    Debug.LogWarning($"PawnPlacementManager: Player 1 has already spawned pawn type {selectedPawnIndex}!");
                    return;
                }
            }
            else if (isP2Region)
            {
                if (p2SpawnedTypes.Contains(selectedPawnIndex))
                {
                    Debug.LogWarning($"PawnPlacementManager: Player 2 has already spawned pawn type {selectedPawnIndex}!");
                    return;
                }
            }
            else
            {
                Debug.LogWarning($"PawnPlacementManager: Row {row} is outside valid placement zones!");
                return;
            }

            GameObject prefab = pawnPrefabs[selectedPawnIndex];
            if (prefab == null) 
            {
                Debug.LogError($"PawnPlacementManager: No prefab assigned for index {selectedPawnIndex}!");
                return;
            }

            // CRITICAL CHECK: Does the prefab have a Pawn component?
            Pawn pawnInPrefab = prefab.GetComponent<Pawn>();
            if (pawnInPrefab == null)
            {
                Debug.LogError($"PawnPlacementManager: The prefab '{prefab.name}' is missing the 'Pawn' component! " +
                               "Without it, the system cannot track occupation or unique limits.");
                return;
            }

            // Placement
            Vector3 targetPos = cell.transform.position + pawnVisualOffset;
            Vector3 spawnPos = targetPos + Vector3.up * dropHeight;
            
            GameObject pawnObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            Pawn pawn = pawnObj.GetComponent<Pawn>();
            
            pawn.Initialize(cell);
            
            // Track uniqueness per player
            if (isP1Region) p1SpawnedTypes.Add(selectedPawnIndex);
            else if (isP2Region) p2SpawnedTypes.Add(selectedPawnIndex);

            StartCoroutine(AnimatePawnDrop(pawnObj, targetPos));
            Debug.Log($"PawnPlacementManager: Successfully placed pawn type {selectedPawnIndex} for Player {(isP1Region ? "1" : "2")}.");
        }

        /// <summary>
        /// Resets the internal tracking of spawned pawns. Call this if you clear the grid.
        /// </summary>
        [ContextMenu("Reset Placement Tracking")]
        public void ResetTracking()
        {
            p1SpawnedTypes.Clear();
            p2SpawnedTypes.Clear();
            Debug.Log("PawnPlacementManager: Placement tracking reset.");
        }

        private IEnumerator AnimatePawnDrop(GameObject pawn, Vector3 targetPos)
        {
            float elapsed = 0;
            Vector3 startPos = pawn.transform.position;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                
                // Smooth Step for a nicer feel
                t = t * t * (3f - 2f * t);
                
                pawn.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            pawn.transform.position = targetPos;
        }

        #endregion
    }
}
