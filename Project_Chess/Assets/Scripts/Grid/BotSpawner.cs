using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// GameScene içinde bu bileşen NetworkObject üzerinde çalışır.
    /// OnNetworkSpawn'da PlayerPrefs'te "BotMode" == 1 ise
    /// BotAIController prefabını spawn ederek botu devreye sokar.
    ///
    /// KURULUM:
    ///   1. GameScene'deki herhangi bir (ya da yeni) NetworkObject'e ekle.
    ///   2. Inspector'dan botControllerPrefab alanına BotAIController prefabını ata.
    /// </summary>
    public class BotSpawner : NetworkBehaviour
    {
        [Tooltip("BotAIController bileşeni olan NetworkObject prefabı.")]
        [SerializeField] private GameObject botControllerPrefab;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Sadece server tarafında ve yalnızca bot modundaysa spawn et
            if (!IsServer) return;
            if (PlayerPrefs.GetInt("BotMode", 0) != 1) return;

            // Flag'i temizle — bir sonraki normal oyunda devreye girmesin
            PlayerPrefs.DeleteKey("BotMode");
            PlayerPrefs.Save();

            if (botControllerPrefab == null)
            {
                Debug.LogError("[BotSpawner] botControllerPrefab atanmamış! Inspector'ı kontrol et.");
                return;
            }

            GameObject go = Instantiate(botControllerPrefab);
            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[BotSpawner] Prefab üzerinde NetworkObject bileşeni yok!");
                Destroy(go);
                return;
            }

            netObj.Spawn();
            Debug.Log("[BotSpawner] BotAIController spawn edildi — Bot modu aktif.");
        }
    }
}
