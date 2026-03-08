using UnityEngine;
using Unity.Netcode;

public class PawnHealthController : NetworkBehaviour, IHoverable
{
    public int maxHealth = 100;
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int damage = 10;
    public GameObject pawn;

    private bool isHovered = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (isHovered && HealthUIManager.Instance != null)
        {
            // Eğer UI açıksa yeni can değeri ile güncellenmesini sağla
            HealthUIManager.Instance.ShowHealthBar(transform, newValue, maxHealth);
        }
    }

    // Fare piyonun üstüne gelince
    public void OnHoverEnter()
    {
        isHovered = true;
        if (HealthUIManager.Instance != null)
            HealthUIManager.Instance.ShowHealthBar(transform, currentHealth.Value, maxHealth);
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
            TakeDamageServerRpc(damage);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int damageAmount)
    {
        if (!IsServer) return;

        currentHealth.Value -= damageAmount;

        if (currentHealth.Value <= 0)
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
