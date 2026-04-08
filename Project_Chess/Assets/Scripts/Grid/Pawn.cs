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
        public NetworkVariable<int> maxHealth = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> currentHealth = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> damage = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        
        [Header("Sync Data")]
        private NetworkVariable<int> netPlayerID = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<int> netTypeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<Vector2Int> netCellCoords = new NetworkVariable<Vector2Int>(new Vector2Int(-999, -999), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Active Buffs (Server-Side)")]
        public System.Collections.Generic.List<ServerActiveBuff> activeBuffs = new System.Collections.Generic.List<ServerActiveBuff>();

        [Header("Visuals")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField, Min(0)] private int highlightMaterialIndex = 0;
        [SerializeField] private Color player1Color = Color.white;
        [SerializeField] private Color player2Color = Color.black;
        private Material[] originalMaterials;
        private bool isInitialized = false;
        [SerializeField] private bool debugBuffs = true;
        [Header("Debug (Runtime)")]
        [SerializeField, ReadOnly] private int debugMaxHealth;
        [SerializeField, ReadOnly] private int debugCurrentHealth;
        [SerializeField, ReadOnly] private int debugDamage;

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
            netPlayerID.OnValueChanged += OnPlayerIdChanged;
            maxHealth.OnValueChanged += OnStatsChanged;
            currentHealth.OnValueChanged += OnStatsChanged;
            damage.OnValueChanged += OnStatsChanged;
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
            ApplyPlayerVisuals(netPlayerID.Value);
            if (TurnManager.Instance != null && IsServer)
            {
                TurnManager.Instance.OnTurnChanged += OnTurnChanged;
            }
            UpdateDebugStats();
        }

        public override void OnNetworkDespawn()
        {
            netCellCoords.OnValueChanged -= OnCellCoordsChanged;
            netPlayerID.OnValueChanged -= OnPlayerIdChanged;
            maxHealth.OnValueChanged -= OnStatsChanged;
            currentHealth.OnValueChanged -= OnStatsChanged;
            damage.OnValueChanged -= OnStatsChanged;
            if (TurnManager.Instance != null && IsServer)
            {
                TurnManager.Instance.OnTurnChanged -= OnTurnChanged;
            }
        }

        private void OnTurnChanged(int activePlayerID)
        {
            if (activePlayerID == PlayerID)
            {
                TickBuffsServer();
            }
        }

        private void OnCellCoordsChanged(Vector2Int previousValue, Vector2Int newValue)
        {
            if (newValue.x != -999)
            {
                AttachToGridLocally(newValue);
            }
        }

        private void OnStatsChanged(int previousValue, int newValue)
        {
            UpdateDebugStats();
        }

        private void OnPlayerIdChanged(int previousValue, int newValue)
        {
            ApplyPlayerVisuals(newValue);
        }

        private void UpdateDebugStats()
        {
            debugMaxHealth = maxHealth.Value;
            debugCurrentHealth = currentHealth.Value;
            debugDamage = damage.Value;
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

        private void ApplyPlayerVisuals(int playerId)
        {
            if (meshRenderer == null) return;
            if (playerId != 1 && playerId != 2) return;

            Color targetColor = playerId == 2 ? player2Color : player1Color;
            Material[] mats = meshRenderer.materials;
            if (mats == null || mats.Length == 0) return;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
                if (mat.HasProperty("_Color")) mat.color = targetColor;
            }

            meshRenderer.materials = mats;
            originalMaterials = mats;
        }

        public void ResetSynergyServer()
        {
            if(!IsServer) return;
            if (debugBuffs)
            {
                Debug.Log($"Pawn: ResetSynergyServer pre max={maxHealth.Value} dmg={damage.Value} cur={currentHealth.Value}");
            }
            hasSynergy.Value = false;
            maxHealth.Value = pawnData.maxHealth;
            damage.Value = pawnData.damage;

            if (currentHealth.Value > maxHealth.Value)
            {
                currentHealth.Value = maxHealth.Value;
            }
            activeBuffs.Clear();
            if (debugBuffs)
            {
                Debug.Log($"Pawn: ResetSynergyServer post max={maxHealth.Value} dmg={damage.Value} cur={currentHealth.Value}");
            }
        }

        public void ApplyBuffsServer(int bonusHealth, int bonusDamage)
        {
            if (!IsServer) return;
            if (debugBuffs)
            {
                Debug.Log($"Pawn: ApplyBuffsServer bonusH={bonusHealth} bonusD={bonusDamage} pre max={maxHealth.Value} dmg={damage.Value} cur={currentHealth.Value}");
            }
            hasSynergy.Value = true;
            
            maxHealth.Value += bonusHealth;
            damage.Value += bonusDamage;
            currentHealth.Value += bonusHealth;
            if (debugBuffs)
            {
                Debug.Log($"Pawn: ApplyBuffsServer post max={maxHealth.Value} dmg={damage.Value} cur={currentHealth.Value}");
            }
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

        public void ApplyRuntimeBuffsServer(System.Collections.Generic.List<BuffData> buffs)
        {
            if (!IsServer) return;
            if (buffs == null) return;

            bool immuneToDebuffs = HasDebuffImmunity();
            bool isAmplified = HasDebuffAmplifier();

            foreach (var buff in buffs)
            {
                if (buff == null) continue;

                if (immuneToDebuffs && !buff.isPositiveEffect)
                {
                    Debug.Log($"Debuff {buff.effectType} blocked due to Immunity!");
                    continue;
                }

                if (buff.durationTurns == 0)
                {
                    // Instant effects
                    float multiplier = (!buff.isPositiveEffect && isAmplified) ? 2f : 1f;

                    if (buff.effectType == EffectType.CurrentHealth)
                    {
                        int change = buff.isPercentage ? Mathf.RoundToInt(currentHealth.Value * (buff.amount / 100f)) : (int)buff.amount;
                        change = Mathf.RoundToInt(change * multiplier);
                        currentHealth.Value += change;
                        if (currentHealth.Value > maxHealth.Value) currentHealth.Value = maxHealth.Value;
                    }
                    else if (buff.effectType == EffectType.MaxHealth)
                    {
                        int change = buff.isPercentage ? Mathf.RoundToInt(maxHealth.Value * (buff.amount / 100f)) : (int)buff.amount;
                        change = Mathf.RoundToInt(change * multiplier);
                        maxHealth.Value += change;
                        currentHealth.Value += change;
                        if (currentHealth.Value > maxHealth.Value) currentHealth.Value = maxHealth.Value;
                    }
                }
                else
                {
                    activeBuffs.Add(new ServerActiveBuff(buff));
                }
            }
        }

        public void TickBuffsServer()
        {
            if (!IsServer) return;
            
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                activeBuffs[i].remainingTurns--;
                if (activeBuffs[i].remainingTurns <= 0)
                {
                    activeBuffs.RemoveAt(i);
                }
            }
        }

        #region Buff Helper Methods

        public bool HasDebuffAmplifier()
        {
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.DebuffAmplifier) return true;
            }
            return false;
        }

        public float GetOutgoingDamageMultiplier()
        {
            float multiplier = 1f;
            bool isAmplified = HasDebuffAmplifier();
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.OutgoingDamageModifier)
                {
                    float val = buff.buffData.isPercentage ? buff.buffData.amount / 100f : buff.buffData.amount;
                    if (!buff.buffData.isPositiveEffect && isAmplified) val *= 2f;
                    multiplier += val;
                }
            }
            return Mathf.Max(0f, multiplier);
        }

        public float GetIncomingDamageMultiplier()
        {
            float multiplier = 1f;
            bool isAmplified = HasDebuffAmplifier();
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.IncomingDamageModifier)
                {
                    // Remember: For IncomingDamageModifier, decreasing incoming damage is a Buff (isPositiveEffect = true, negative amount)
                    // So increasing incoming damage is a Debuff (isPositiveEffect = false, positive amount).
                    float val = buff.buffData.isPercentage ? buff.buffData.amount / 100f : buff.buffData.amount;
                    if (!buff.buffData.isPositiveEffect && isAmplified) val *= 2f;
                    multiplier += val;
                }
            }
            return Mathf.Max(0f, multiplier);
        }

        public float GetLifestealPercentage()
        {
            float lifesteal = 0f;
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.Lifesteal)
                {
                    float val = buff.buffData.isPercentage ? buff.buffData.amount / 100f : buff.buffData.amount;
                    lifesteal += val;
                }
            }
            return lifesteal;
        }

        public float GetRecoilPercentage()
        {
            float recoil = 0f;
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.Recoil)
                {
                    float val = buff.buffData.isPercentage ? buff.buffData.amount / 100f : buff.buffData.amount;
                    recoil += val;
                }
            }
            return recoil;
        }

        public bool ConsumeDamageBlock()
        {
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (activeBuffs[i].buffData.effectType == EffectType.DamageBlock)
                {
                    activeBuffs.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool HasStun()
        {
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.Stun) return true;
            }
            return false;
        }

        public int GetMovementRangeModifier()
        {
            int modifier = 0;
            bool isAmplified = HasDebuffAmplifier();
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.MovementRangeModifier)
                {
                    float val = buff.buffData.amount;
                    if (!buff.buffData.isPositiveEffect && isAmplified) val *= 2f;
                    modifier += (int)val;
                }
            }
            return modifier;
        }

        public bool HasDebuffImmunity()
        {
            foreach (var buff in activeBuffs)
            {
                if (buff.buffData.effectType == EffectType.DebuffImmunity) return true;
            }
            return false;
        }

        #endregion
     
        #endregion
    }
}

