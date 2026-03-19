using System;
using AlperKocasalih.Chess.Grid;
using UnityEngine;
using Unity.Netcode;

public class PawnHealthController : NetworkBehaviour, IHoverable
{
    private Pawn _pawn;
    public GameObject pawn;
    private bool isHovered = false;

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
        _pawn.currentHealth.OnValueChanged -= OnHealthChanged;
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
        isHovered = true;
        if (HealthUIManager.Instance != null)
            HealthUIManager.Instance.ShowHealthBar(transform, _pawn.currentHealth.Value, _pawn.maxHealth.Value);
    }

    // Fare piyondan çıkınca
    public void OnHoverExit()
    {
        isHovered = false;
        if (HealthUIManager.Instance != null)
            HealthUIManager.Instance.HideHealthBar();
    }

    void Update()
    {
        // Örnek hasar alma kodu (Yalnızca piyonun üzerine gelindiğinde ve sol tıklandığında test için hasar al)
        if (Input.GetMouseButtonDown(0) && isHovered)
        {
            TakeDamageServerRpc(_pawn.damage.Value);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TakeDamageServerRpc(int damageAmount)
    {
        if (!IsServer) return;

        _pawn.currentHealth.Value -= damageAmount;

        if (_pawn.currentHealth.Value <= 0)
        {
            Die();
        }
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
