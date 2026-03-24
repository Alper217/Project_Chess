using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Represents a pawn in the game.
    /// Manages its relationship with the HexCell it occupies.
    /// </summary>,
    
    public class Pawn : NetworkBehaviour
    {
        #region Fields

        [Header("Pawn Data")]
        [SerializeField] private PawnData pawnData;
        [SerializeField, ReadOnly] private HexCell currentCell;
        public NetworkVariable<int> maxHealth = new NetworkVariable<int>();
        public NetworkVariable<int> currentHealth = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> damage = new NetworkVariable<int>();
        
        [Header("Sync Data")]
        private NetworkVariable<int> netPlayerID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> netTypeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<Vector2Int> netCellCoords = new NetworkVariable<Vector2Int>(new Vector2Int(-999, -999), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Visuals")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField, Min(0)] private int highlightMaterialIndex = 0;
        private Material[] originalMaterials;
        private bool isInitialized = false;

        #endregion

        #region Properties

        public PawnData PawnData => pawnData;
        public HexCell OccupiedCell => currentCell;
        public int PlayerID { get => netPlayerID.Value; set { if (IsServer) netPlayerID.Value = value; } }
        public int TypeIndex { get => netTypeIndex.Value; set { if (IsServer) netTypeIndex.Value = value; } }
        public NetworkVariable<bool> hasSynergy = new NetworkVariable<bool>(false);
        #endregion

        #region Methods

        public override void OnNetworkSpawn()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            netCellCoords.OnValueChanged += OnCellCoordsChanged;
            // If we are a client joining late, or just receiving the spawn AFTER data is set:
            if (netCellCoords.Value.x != -999)
            {
                AttachToGridLocally(netCellCoords.Value);
            }
            if (IsServer)
            {
                maxHealth.Value = pawnData.maxHealth;
                damage.Value = pawnData.damage;
                currentHealth.Value = pawnData.currentHealth;
                currentHealth.Value = maxHealth.Value;
            }

        }

        public override void OnNetworkDespawn()
        {
            netCellCoords.OnValueChanged -= OnCellCoordsChanged;
        }

        private void OnCellCoordsChanged(Vector2Int previousValue, Vector2Int newValue)
        {
            if (newValue.x != -999)
            {
                AttachToGridLocally(newValue);
            }
        }
        
        

        private void AttachToGridLocally(Vector2Int coords)
        {
            // Do not double initialize if we already did via Server method
            if (isInitialized && currentCell != null && currentCell.Coordinates == coords) return;

            PawnPlacementManager ppm = PawnPlacementManager.Instance;
            if (ppm != null)
            {
                HexCell cell = ppm.GetCellByCoords(coords);
                if (cell != null)
                {
                    Initialize(cell);
                    ppm.RegisterPawnLocally(this);
                }
            }
        }

        /// <summary>
        /// Server only: Initialize data right after spawn.
        /// </summary>
        public void SetNetworkData(int pID, int tIndex, Vector2Int coords)
        {
            if (!IsServer) return;
            netPlayerID.Value = pID;
            netTypeIndex.Value = tIndex;
            netCellCoords.Value = coords;
        }

        /// <summary>
        /// Initializes the pawn and links it to a cell.
        /// </summary>
        public void Initialize(HexCell cell)
        {
            currentCell = cell;
            currentCell.IsOccupied = true;
            isInitialized = true;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();  
        }

        public void SetCell(HexCell cell)
        {
            if (IsServer) netCellCoords.Value = cell.Coordinates;
            currentCell = cell;
        }

        /// <summary>
        /// Highlights the pawn visually.
        /// </summary>
        public void VisualHighlight(Material mat)
        {
            if (meshRenderer == null) return;

            Material[] mats = meshRenderer.materials;
            if (mats == null || mats.Length == 0) return;

            int index = Mathf.Clamp(highlightMaterialIndex, 0, mats.Length - 1);
            mats[index] = mat;
            meshRenderer.materials = mats;
        }

        /// <summary>
        /// Resets the pawn's visual highlight.
        /// </summary>
        public void ResetHighlight()
        {
            if (meshRenderer == null || originalMaterials == null || originalMaterials.Length == 0) return;
            meshRenderer.materials = originalMaterials;
        }

        public void ResetSynergyServer()
        {
            if(!IsServer) return;
            hasSynergy.Value = false;
            maxHealth.Value = pawnData.maxHealth;
            damage.Value = pawnData.damage;

            if (currentHealth.Value > maxHealth.Value)
            {
                currentHealth.Value = maxHealth.Value;
            }
        }

        public void ApplyBuffsServer(int bonusHealth, int bonusDamage)
        {
            if (!IsServer) return;
            hasSynergy.Value = true;
            
            maxHealth.Value += bonusHealth;
            damage.Value += bonusDamage;
            currentHealth.Value += bonusHealth;
        }

        public void ApplyCardEffectServer(int bonusHealth, int bonusDamage)
        {
            if (!IsServer) return;

            if (bonusHealth != 0)
            {
                maxHealth.Value += bonusHealth;
                currentHealth.Value += bonusHealth;
                if (currentHealth.Value > maxHealth.Value)
                {
                    currentHealth.Value = maxHealth.Value;
                }
            }

            if (bonusDamage != 0)
            {
                damage.Value += bonusDamage;
            }
        }

     
        #endregion
    }
}

