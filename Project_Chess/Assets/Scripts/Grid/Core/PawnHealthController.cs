using System;
using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;
using AlperKocasalih.Chess.Grid.Utils;
using UnityEngine;
using Unity.Netcode;

public class PawnHealthController : NetworkBehaviour, IHoverable
{
    private Pawn _pawn;
    public GameObject pawn;
    private bool isHovered = false;
    [SerializeField] private Color attackHighlightColor = Color.blue;
    private readonly List<HexCell> highlightedCells = new List<HexCell>();
    private Dictionary<Vector2Int, HexCell> gridLookup = new Dictionary<Vector2Int, HexCell>();

    private void Awake()
    {
        _pawn = GetComponent<Pawn>();
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
            // Eğer UI açıksa yeni can değeri ile güncellenmesini sağla
            HealthUIManager.Instance.ShowHealthBar(transform, newValue, _pawn.maxHealth.Value);
        }
    }

    // Fare piyonun üstüne gelince
    public void OnHoverEnter()
    {
        if (_pawn == null) return;
        isHovered = true;
        if (HealthUIManager.Instance != null)
            HealthUIManager.Instance.ShowHealthBar(transform, _pawn.currentHealth.Value, _pawn.maxHealth.Value);
        
        ShowAttackRange();
    }

    // Fare piyondan çıkınca
    public void OnHoverExit()
    {
        if (_pawn == null) return;
        isHovered = false;
        if (HealthUIManager.Instance != null)
            HealthUIManager.Instance.HideHealthBar();
        
        ClearAttackRange();
    }

    void Update()
    {
        // Örnek hasar alma kodu (Yalnızca piyonun üzerine gelindiğinde ve sol tıklandığında test için hasar al)
        if (Input.GetMouseButtonDown(0) && isHovered)
        {
            TakeDamageServer(_pawn.damage.Value);
        }
    }
    
    public void TakeDamageServer(int damageAmount)
    {
        if (!IsServer) return;

        _pawn.currentHealth.Value -= damageAmount;

        if (_pawn.currentHealth.Value <= 0)
        {
            Die();
        }
    }
    public void HealServer(int amount)
    {
        if (!IsServer) return;

        _pawn.currentHealth.Value += amount;

        // Canın, maksimum canı (maxHealth) geçmesini engelle
        if (_pawn.currentHealth.Value > _pawn.maxHealth.Value)
        {
            _pawn.currentHealth.Value = _pawn.maxHealth.Value;
        }
    
        Debug.Log($"{_pawn.PawnData.pawnName} iyileştirildi. Yeni can: {_pawn.currentHealth.Value}");
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

        foreach (var offset in offsets)
        {
            Vector2Int targetCoords = currentPos + offset;

            bool isBlocked = false;
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

        // Piyon öldüğünde UI'yi gizleme (istemcilerde de tetiklenmesi için OnNetworkDespawn kullanılabilir)
        // Objenin ağ üzerindeki varlığını sonlandır
        if (pawn != null)
        {
            NetworkObject pawnNetworkObject = pawn.GetComponent<NetworkObject>();
            if (pawnNetworkObject != null && pawnNetworkObject.IsSpawned)
            {
                pawnNetworkObject.Despawn();
                return;
            }
        }
        
        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
