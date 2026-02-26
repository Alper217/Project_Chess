using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    public class PawnPlacementManager : NetworkBehaviour
    {
        public static PawnPlacementManager Instance { get; private set; }

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
        
        private bool p1Confirmed = false;
        private bool p2Confirmed = false;
        
        private Camera mainCamera;

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
            InitializeGridLookup();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // If we are Player 2 (Client), flip the camera 180 degrees so they see their pawns at the bottom
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            if (localPlayerID == 2 && mainCamera != null)
            {
                // Rotate 180 degrees around the Y axis
                mainCamera.transform.RotateAround(Vector3.zero, Vector3.up, 180f);
                Debug.Log("PawnPlacementManager: Flipped camera for Player 2.");
            }
        }

        private void Update()
        {
            // Only allow placement during Setup phase
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Setup)
                return;

            // Do not handle placement if movement mode is active
            if (PawnMovementManager.Instance != null && PawnMovementManager.Instance.IsActive)
                return;

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

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // This is now handled via FinishSetup which checks for both confirmations
                Debug.Log("PawnPlacementManager: Space pressed. Use UI buttons to confirm for each player.");
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

            gridLookup.Clear();
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
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            int rowCheck = cell.Coordinates.y;
            bool isP1Region = rowCheck >= 0 && rowCheck <= 2;
            bool isP2Region = rowCheck >= 7 && rowCheck <= 9;

            if (!isP1Region && !isP2Region)
            {
                Debug.LogWarning($"PawnPlacementManager: Row {rowCheck} is outside valid placement zones!");
                return;
            }

            if (isP1Region && localPlayerID != 1)
            {
                Debug.LogWarning("PawnPlacementManager: You can only interact with your own region (Player 1).");
                return;
            }
            if (isP2Region && localPlayerID != 2)
            {
                Debug.LogWarning("PawnPlacementManager: You can only interact with your own region (Player 2).");
                return;
            }

            // Client validates locally, then tells server to spawn
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                TryPlacePawnServerRpc(cell.Coordinates, selectedPawnIndex, localPlayerID);
                return;
            }

            // Server-side validation and spawn
            SpawnPawnOnServer(cell, selectedPawnIndex, localPlayerID);
        }

        [ServerRpc(RequireOwnership = false)]
        private void TryPlacePawnServerRpc(Vector2Int coordinates, int pawnIndex, int playerID)
        {
            HexCell cell = GetCellByCoords(coordinates);
            if (cell != null)
            {
                SpawnPawnOnServer(cell, pawnIndex, playerID);
            }
            else
            {
                Debug.LogError($"PawnPlacementManager: Server could not find cell at coordinates {coordinates}!");
            }
        }

        private void SpawnPawnOnServer(HexCell cell, int pawnIndex, int playerID)
        {
            int rowCheck = cell.Coordinates.y;
            bool isP1Region = rowCheck >= 0 && rowCheck <= 2;
            bool isP2Region = rowCheck >= 7 && rowCheck <= 9;

            // Network validation reporting mapping
            ulong targetClientId = playerID == 1 ? 0UL : 1UL;
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
            };

            // Only Server runs this logic
            // Validation 1: Already occupied?
            if (cell.IsOccupied)
            {
                // Repositioning logic: If the player clicks their own pawn, remove it
                Pawn existingPawn = cell.GetComponentInChildren<Pawn>();
                if (existingPawn == null) existingPawn = FindPawnOnCell(cell);

                if (existingPawn != null)
                {
                    // Only allow picking up if it's the correct region and player hasn't confirmed
                    if (isP1Region && !p1Confirmed)
                    {
                        p1SpawnedTypes.Remove(existingPawn.TypeIndex); // Need to ensure Pawn has TypeIndex
                        cell.ClearOccupiedPawn(); // Assuming HexCell has this or we manage it
                        
                        NetworkObject existingNetObj = existingPawn.GetComponent<NetworkObject>();
                        if (existingNetObj != null && existingNetObj.IsSpawned) existingNetObj.Despawn();
                        else Destroy(existingPawn.gameObject);
                        
                        Debug.Log($"PawnPlacementManager: Player 1 picked up pawn type {existingPawn.TypeIndex}.");
                        return;
                    }
                    else if (isP2Region && !p2Confirmed)
                    {
                        p2SpawnedTypes.Remove(existingPawn.TypeIndex);
                        cell.ClearOccupiedPawn();
                        
                        NetworkObject existingNetObj = existingPawn.GetComponent<NetworkObject>();
                        if (existingNetObj != null && existingNetObj.IsSpawned) existingNetObj.Despawn();
                        else Destroy(existingPawn.gameObject);
                        
                        Debug.Log($"PawnPlacementManager: Player 2 picked up pawn type {existingPawn.TypeIndex}.");
                        return;
                    }
                }
                
                string msg = $"PawnPlacementManager: Cell at {cell.Coordinates} is already occupied!";
                Debug.LogWarning(msg);
                SendWarningToClientRpc(msg, clientRpcParams);
                return;
            }

            // Validation 2: Region check & Per-player Uniqueness
            if (isP1Region)
            {
                if (p1Confirmed) return;
                if (p1SpawnedTypes.Count >= pawnPrefabs.Length)
                {
                    string msg = $"PawnPlacementManager: Player 1 has already placed {pawnPrefabs.Length} pawns!";
                    Debug.LogWarning(msg);
                    SendWarningToClientRpc(msg, clientRpcParams);
                    return;
                }
                if (p1SpawnedTypes.Contains(pawnIndex))
                {
                    string msg = $"PawnPlacementManager: Player 1 has already spawned pawn type {pawnIndex}!";
                    Debug.LogWarning(msg);
                    SendWarningToClientRpc(msg, clientRpcParams);
                    return;
                }
            }
            else if (isP2Region)
            {
                if (p2Confirmed) return;
                if (p2SpawnedTypes.Count >= pawnPrefabs.Length)
                {
                    string msg = $"PawnPlacementManager: Player 2 has already placed {pawnPrefabs.Length} pawns!";
                    Debug.LogWarning(msg);
                    SendWarningToClientRpc(msg, clientRpcParams);
                    return;
                }
                if (p2SpawnedTypes.Contains(pawnIndex))
                {
                    string msg = $"PawnPlacementManager: Player 2 has already spawned pawn type {pawnIndex}!";
                    Debug.LogWarning(msg);
                    SendWarningToClientRpc(msg, clientRpcParams);
                    return;
                }
            }
            else
            {
                string msg = $"PawnPlacementManager: Row {rowCheck} is outside valid placement zones!";
                Debug.LogWarning(msg);
                SendWarningToClientRpc(msg, clientRpcParams);
                return;
            }

            GameObject prefab = pawnPrefabs[pawnIndex];
            if (prefab == null) 
            {
                Debug.LogError($"PawnPlacementManager: No prefab assigned for index {pawnIndex}!");
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
            // PlayerID and TypeIndex will be set via SetNetworkData after Spawn
            
            // Track uniqueness per player
            if (isP1Region) p1SpawnedTypes.Add(pawnIndex);
            else if (isP2Region) p2SpawnedTypes.Add(pawnIndex);

            // Network spawn for pawn if server
            NetworkObject netObj = pawnObj.GetComponent<NetworkObject>();
            if (netObj != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn(true);
            }

            // Sync state via NetworkVariables
            pawn.SetNetworkData(isP1Region ? 1 : 2, pawnIndex, cell.Coordinates);

            StartCoroutine(AnimatePawnDrop(pawnObj, targetPos));
            
            string successMsg = $"PawnPlacementManager: Successfully placed pawn type {pawnIndex} for Player {(isP1Region ? "1" : "2")}.";
            Debug.Log(successMsg);
            SendLogToClientRpc(successMsg, clientRpcParams);
        }

        [ClientRpc]
        private void SendWarningToClientRpc(string msg, ClientRpcParams clientRpcParams = default)
        {
            Debug.LogWarning(msg);
        }

        [ClientRpc]
        private void SendLogToClientRpc(string msg, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log(msg);
        }

        public HexCell GetCellByCoords(Vector2Int coords)
        {
            if (gridLookup == null || gridLookup.Count == 0)
            {
                InitializeGridLookup();
            }

            if (gridLookup.TryGetValue(coords, out HexCell cell)) return cell;
            return null;
        }

        public void RegisterPawnLocally(Pawn pawn)
        {
            if (pawn.PlayerID == 1) p1SpawnedTypes.Add(pawn.TypeIndex);
            else if (pawn.PlayerID == 2) p2SpawnedTypes.Add(pawn.TypeIndex);

            if (pawn.OccupiedCell != null)
            {
                Vector3 targetPos = pawn.OccupiedCell.transform.position + pawnVisualOffset;
                StartCoroutine(AnimatePawnDrop(pawn.gameObject, targetPos));
            }
        }

        private Pawn FindPawnOnCell(HexCell cell)
        {
            // Fallback to find pawn if not easily accessible
            Pawn[] allPawns = FindObjectsOfType<Pawn>();
            foreach (var p in allPawns)
            {
                if (p.OccupiedCell == cell) return p;
            }
            return null;
        }

        public void ConfirmLocalPlayerPlacement()
        {
            int localPlayerID = 1;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                localPlayerID = NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }
            ConfirmPlayerPlacementServerRpc(localPlayerID);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ConfirmPlayerPlacementServerRpc(int playerID)
        {
            ConfirmPlayerPlacement(playerID);
        }

        public void ConfirmPlayerPlacement(int playerID)
        {
            if (playerID == 1)
            {
                if (p1SpawnedTypes.Count == pawnPrefabs.Length)
                {
                    p1Confirmed = true;
                    Debug.Log("PawnPlacementManager: Player 1 confirmed placement.");
                }
                else Debug.LogWarning($"PawnPlacementManager: Player 1 must place {pawnPrefabs.Length} pawns first!");
            }
            else if (playerID == 2)
            {
                if (p2SpawnedTypes.Count == pawnPrefabs.Length)
                {
                    p2Confirmed = true;
                    Debug.Log("PawnPlacementManager: Player 2 confirmed placement.");
                }
                else Debug.LogWarning($"PawnPlacementManager: Player 2 must place {pawnPrefabs.Length} pawns first!");
            }

            if (p1Confirmed && p2Confirmed)
            {
                // Setup finished logic
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    FinishSetupServer();
                }
            }
        }

        private void FinishSetupServer()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Setup)
            {
                Debug.Log("PawnPlacementManager: Setup finished. Moving to RollDice state.");
                GameManager.Instance.ChangeState(GameState.RollDice);
            }
        }

        [ContextMenu("Finish Setup")]
        public void FinishSetup()
        {
            if (!p1Confirmed || !p2Confirmed)
            {
                Debug.LogWarning("PawnPlacementManager: Both players must confirm before finishing setup.");
                return;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                FinishSetupServer();
            }
        }

        /// <summary>
        /// Resets the internal tracking of spawned pawns. Call this if you clear the grid.
        /// </summary>
        [ContextMenu("Reset Placement Tracking")]
        public void ResetTracking()
        {
            p1SpawnedTypes.Clear();
            p2SpawnedTypes.Clear();
            p1Confirmed = false;
            p2Confirmed = false;
            Debug.Log("PawnPlacementManager: Placement tracking reset.");
        }

        private IEnumerator AnimatePawnDrop(GameObject pawn, Vector3 targetPos)
        {
            float elapsed = 0;
            Vector3 startPos = pawn.transform.position;

            while (elapsed < animationDuration)
            {
                if (pawn == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                
                // Smooth Step for a nicer feel
                t = t * t * (3f - 2f * t);
                
                pawn.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            if (pawn != null)
            {
                pawn.transform.position = targetPos;
            }
        }

        #endregion
    }
}
