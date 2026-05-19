using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;
using AlperKocasalih.Chess.Grid.Utils;
using Unity.Netcode;
using UnityEngine;
using MoreMountains.Feedbacks;

public class PawnHealthController : NetworkBehaviour, IHoverable
{
    private Pawn _pawn;
    public GameObject pawn;
    private bool isHovered = false;
    [SerializeField] private Color attackHighlightColor = Color.blue;
    private readonly List<HexCell> highlightedCells = new List<HexCell>();
    private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();
    public MMF_Player targetPlayer;

    private Vector3 _originalScale;

    private void Awake()
    {
        _pawn = GetComponent<Pawn>();
        _originalScale = transform.localScale;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _pawn.currentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (_pawn != null)
            _pawn.currentHealth.OnValueChanged -= OnHealthChanged;

        ClearAttackRange();
        if (isHovered && HealthUIManager.Instance != null)
        {
            HealthUIManager.Instance.HideHealthBar();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        ClearAttackRange();
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (isHovered && HealthUIManager.Instance != null)
        {
            ShowPawnHoverUI(newValue);
        }
    }

    public void OnHoverEnter()
    {
        if (_pawn == null) return;

        isHovered = true;
        if (HealthUIManager.Instance != null)
        {
            ShowPawnHoverUI(_pawn.currentHealth.Value);
        }

        ShowAttackRange();
    }

    public void OnHoverExit()
    {
        if (_pawn == null) return;

        isHovered = false;
        if (HealthUIManager.Instance != null)
        {
            HealthUIManager.Instance.HideHealthBar();
        }

        ClearAttackRange();
    }

    private void ShowPawnHoverUI(int currentHealth)
    {
        if (_pawn == null || HealthUIManager.Instance == null)
        {
            return;
        }

        string buffsText = _pawn.BuffSummary;

        HealthUIManager.Instance.ShowHealthBar(
            transform,
            currentHealth,
            _pawn.maxHealth.Value,
            _pawn.PawnData.pawnName,
            buffsText,
            _pawn.DebuffSummary);
    }

    void Update()
    {
        if (isHovered && HealthUIManager.Instance != null && _pawn != null)
        {
            ShowPawnHoverUI(_pawn.currentHealth.Value);
        }

        // Toggle Force Attack Pattern with 'T' key when hovered (only for own pawns)
        if (Input.GetKeyDown(KeyCode.T) && isHovered && _pawn != null)
        {
            int localPlayerID = 1;
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                localPlayerID = Unity.Netcode.NetworkManager.Singleton.LocalClientId == 0 ? 1 : 2;
            }

            if (_pawn.PlayerID == localPlayerID)
            {
                _pawn.ToggleForceAttackPattern();
            }
        }

        // Removed debug click-to-damage logic
        /*
        if (Input.GetMouseButtonDown(0) && isHovered)
        {
            TakeDamageServer(_pawn.damage.Value);
        }
        */
    }

    public void TakeDamageServer(int damageAmount)
    {
        if (!IsServer) return;

        _pawn.currentHealth.Value -= damageAmount;
        
        // Trigger damage feedback on all clients
        ShowDamageFeedbackClientRpc(damageAmount, false);
        
        if (_pawn.currentHealth.Value <= 0)
        {
            Die();
        }
    }

    public void HealServer(int amount)
    {
        if (!IsServer) return;

        _pawn.currentHealth.Value += amount;

        if (_pawn.currentHealth.Value > _pawn.maxHealth.Value)
        {
            _pawn.currentHealth.Value = _pawn.maxHealth.Value;
        }

        // Trigger healing feedback on all clients
        ShowDamageFeedbackClientRpc(amount, true);

        Debug.Log($"{_pawn.PawnData.pawnName} healed. New health: {_pawn.currentHealth.Value}");
    }

    [ClientRpc]
    private void ShowDamageFeedbackClientRpc(int amount, bool isHeal)
    {
        if (targetPlayer == null) return;

        // Stop any running feedbacks and restore original scale to prevent cumulative scaling bugs
        targetPlayer.StopFeedbacks();
        transform.localScale = _originalScale;

        MMF_FloatingText floatingTextFeedback = targetPlayer.GetFeedbackOfType<MMF_FloatingText>();
        if (floatingTextFeedback != null)
        {
            // Simplified: Just set the color. Outline should be handled in the TMP Material settings.
            string color = isHeal ? "#00FF00" : "#FF0000"; 
            string prefix = isHeal ? "+" : "-";
            
            if (amount == 0 && !isHeal)
            {
                floatingTextFeedback.Value = "<color=#FFFFFF>0</color>";
            }
            else
            {
                floatingTextFeedback.Value = $"<color={color}>{prefix}{amount}</color>";
            }
        }

        // Professional Movement: A slight arc movement instead of straight up
        // Randomized X offset for a "burst" feel
        float randomX = Random.Range(-0.4f, 0.4f);
        Vector3 spawnPos = this.transform.position + new Vector3(randomX, 1.5f, Random.Range(-0.2f, 0.2f));
        
        targetPlayer.PlayFeedbacks(spawnPos);
    }

    [ClientRpc]
    public void ShowBlockedFeedbackClientRpc()
    {
        if (targetPlayer == null) return;

        // Stop any running feedbacks and restore original scale to prevent cumulative scaling bugs
        targetPlayer.StopFeedbacks();
        transform.localScale = _originalScale;

        MMF_FloatingText floatingTextFeedback = targetPlayer.GetFeedbackOfType<MMF_FloatingText>();
        if (floatingTextFeedback != null)
        {
            floatingTextFeedback.Value = "<color=#00CCFF>BLOCKED</color>";
        }
        float randomX = Random.Range(-0.3f, 0.3f);
        Vector3 spawnPos = this.transform.position + new Vector3(randomX, 1.8f, 0);
        targetPlayer.PlayFeedbacks(spawnPos);
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc()
    {
        if (_pawn != null && _pawn.PawnData != null && _pawn.PawnData.deathSound != null && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySfx(_pawn.PawnData.deathSound);
        }
    }

    private void ShowAttackRange()
    {
        if (_pawn == null || _pawn.PawnData == null) return;
        MovementPattern pattern = _pawn.PawnData.attackPattern;
        if (pattern == null) return;
        if (_pawn.OccupiedCell == null) return;
        if (!EnsureGridLookup()) return;

        ClearAttackRange();

        Vector2Int currentPos = _pawn.OccupiedCell.Coordinates;
        List<Vector2Int> offsets = pattern.GetValidOffsets(currentPos, _pawn.PlayerID == 2);
        if (offsets == null || offsets.Count == 0) return;

        Vector3Int startCube = HexGridMath.OffsetToCube(currentPos);

        bool canAttackThroughObstacles = _pawn.PawnData.type == Type.Archer || 
                                         _pawn.PawnData.type == Type.Cannon || 
                                         _pawn.PawnData.type == Type.Cheriff;

        foreach (var offset in offsets)
        {
            Vector2Int targetCoords = currentPos + offset;

            bool isBlocked = false;

            if (!canAttackThroughObstacles)
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
                            isBlocked = true;
                            break;
                        }
                    }
                    else
                    {
                        isBlocked = true;
                        break;
                    }
                }
            }

            if (isBlocked) continue;

            if (gridLookup.TryGetValue(targetCoords, out HexCell targetCell))
            {
                highlightedCells.Add(targetCell);
                targetCell.Highlight(attackHighlightColor);
            }
        }
    }

    private void ClearAttackRange()
    {
        foreach (var cell in highlightedCells)
        {
            if (cell != null) cell.ResetHighlight();
        }
        highlightedCells.Clear();
    }

    private bool EnsureGridLookup()
    {
        if (gridLookup != null && gridLookup.Count > 0) return true;
        if (GridGenerator.Instance == null) return false;

        gridLookup.Clear();
        foreach (var hex in GridGenerator.Instance.SpawnedHexes)
        {
            if (hex == null) continue;
            HexCell cell = hex.GetComponent<HexCell>();
            if (cell != null)
            {
                gridLookup[cell.Coordinates] = cell;
            }
        }

        return gridLookup.Count > 0;
    }

    private void Die()
    {
        if (!IsServer) return;

        int loserID = _pawn.PlayerID;

        // Play death sound on all clients before despawning
        PlayDeathSoundClientRpc();

        if (_pawn != null && _pawn.PawnData != null && GameManager.Instance != null)
        {
            int opponentID = loserID == 1 ? 2 : 1;
            int points = _pawn.PawnData.pointValue;
            Debug.Log($"PawnHealthController: Pawn of Player {loserID} died. Awarding {points} points to Player {opponentID}.");
            GameManager.Instance.AddScore(opponentID, points);

            if (DraftManager.Instance != null)
            {
                DraftManager.Instance.AddReRollsServer(opponentID, DraftManager.Instance.ReRollsPerKill);
            }
        }

        if (pawn != null)
        {
            NetworkObject pawnNetworkObject = pawn.GetComponent<NetworkObject>();
            if (pawnNetworkObject != null && pawnNetworkObject.IsSpawned)
            {
                pawnNetworkObject.Despawn();
            }
        }
        else if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckWinCondition(loserID);
        }
    }
}
