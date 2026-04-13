using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using AlperKocasalih.Chess.Grid;

namespace Tests.PlayMode
{
    public class NetworkedPawnHealthTests
    {
        private GameObject _networkManagerGo;
        private NetworkManager _networkManager;

        private GameObject _pawnObject;
        private Pawn _pawn;
        private PawnHealthController _healthController;

        // [UnitySetUp], testten önce asenkron işlemler yapmamıza (sunucunun açılmasını beklemeye) izin verir.
        [UnitySetUp]
        public IEnumerator Setup()
        {
            // 1. Canlı Sunucu (NetworkManager) Altyapısını Kur
            _networkManagerGo = new GameObject("TestNetworkManager");
            _networkManager = _networkManagerGo.AddComponent<NetworkManager>();
            
            // Ağ İletişimi için UnityTransport ekle
            var transport = _networkManagerGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777);
            
            _networkManager.NetworkConfig = new NetworkConfig()
            {
                NetworkTransport = transport,
                PlayerPrefab = null // Oyuncu prefabı olmadan çalışabilmesi için
            };

            // 2. SUNUCUYU BAŞLAT (Host olarak)
            _networkManager.StartHost();
            
            yield return null; // Sunucunun ayağa kalkması için 1 frame bekle

            // 3. Test Edilecek Piyonumuzu Ağ Üzerinde Oluştur (Spawn)
            _pawnObject = new GameObject("TestPawn");
            var networkObject = _pawnObject.AddComponent<NetworkObject>();
            _pawn = _pawnObject.AddComponent<Pawn>();
            _healthController = _pawnObject.AddComponent<PawnHealthController>();

            // SAHTE VERİ (MOCK) YARATILMASI:
            // Pawn sınıfı, Spawn olurken "pawnData" scriptable objesinden maxHealth çekmeye çalışıyor. 
            // Biz bir tane sanal veri oluşturup Reflection (Yansıma) ile içine enjekte edeceğiz:
            var dummyData = ScriptableObject.CreateInstance<PawnData>();
            dummyData.maxHealth = 100;
            dummyData.currentHealth = 100;
            dummyData.damage = 10;
            
            // MonoBehaviour üzerindeki private (SerializeField) alana zorla veri sokuyoruz:
            typeof(Pawn).GetField("pawnData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_pawn, dummyData);

            // Default statları ayarla
            _pawn.maxHealth.Value = 100;
            _pawn.currentHealth.Value = 100;

            // Piyonu Sunucuda Canlandır (IS SERVER = TRUE OLACAK!)
            networkObject.Spawn();
            
            yield return null; // Ağdaki Spawn işleminin senkronize olması için bekle
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            // Test bitince Sunucuyu ve Objeleri Temizle
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }
            
            yield return null;
            Object.DestroyImmediate(_pawnObject);
            Object.DestroyImmediate(_networkManagerGo);
        }

        // --- CANLI SUNUCU TESTİ ---
        [UnityTest]
        public IEnumerator LiveServer_TakeDamage_ShouldReduceHealth()
        {
            // Arrange: Başlangıç kontrolü
            Assert.IsTrue(_networkManager.IsServer, "HATA: Sunucu (Server) ayağa kalkmadı!");
            Assert.IsTrue(_healthController.IsServer, "HATA: Piyon sunucuda SPAWN olamadı, IsServer=false");
            
            int initialHealth = _pawn.currentHealth.Value;
            
            // Act: Artık koddaki Orijinal "TakeDamageServer" metodunu kullanabiliriz!
            // Çünkü arkada canlı bir sunucu var.
            _healthController.TakeDamageServer(30);

            yield return null; // Ağ değişkenlerinin güncellenmesi için ufak bir esneme payı

            // Assert: Sonucun doğrulanması
            Assert.AreEqual(70, _pawn.currentHealth.Value, "Canlı sunucu üzerinde piyon 30 hasar aldı fakat canı 70'e düşmedi!");
        }
    }
}
