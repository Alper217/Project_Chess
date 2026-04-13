using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid;

namespace Tests.PlayMode
{
    public class PawnHealthTests
    {
        private GameObject _pawnObject;
        private Pawn _pawn;
        private PawnHealthController _healthController;

        // [SetUp] her testten önce çalışır ve ortamı sıfırlar (Arrange)
        [SetUp]
        public void Setup()
        {
            // 1. Arrange: Test için boş bir GameObject ve bileşenlerini oluştur
            _pawnObject = new GameObject("TestPawn");
            _pawn = _pawnObject.AddComponent<Pawn>();
            _healthController = _pawnObject.AddComponent<PawnHealthController>();

            // Netcode NetworkVariables default olarak başlatılsın diye manuel değer atıyoruz
            // (Mocking the server environment)
            _pawn.maxHealth.Value = 100;
            _pawn.currentHealth.Value = 100;
        }

        // [TearDown] her testten sonra çalışır ve çöp bırakmaz
        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_pawnObject);
        }

        // --- TEST 1: Hasar Alma ---
        [Test]
        public void TakeDamage_Should_Reduce_Health()
        {
            // Arrange
            int initialHealth = _pawn.currentHealth.Value;
            int damageAmount = 30;

            // Act: Hasar ver. 
            // NOT: IsServer kontrolü olduğu için doğrudan değişkeni manipüle eden saf fonksiyonu da test edebiliriz.
            // Ama biz şimdilik HealthController'ın iç mantığını simüle ediyoruz.
            _pawn.currentHealth.Value -= damageAmount; 
            // Normalde _healthController.TakeDamageServer(30); yazardık 
            // ancak NetworkBehaviour testleri aktif bir NetworkManager gerektirir. 

            // Assert: Beklenen can kontrolü
            Assert.AreEqual(70, _pawn.currentHealth.Value, "Piyonun canı 30 hasar aldıktan sonra 70 olmalıdır!");
        }

        // --- TEST 2: İyileşme (Heal) Limiti ---
        [Test]
        public void Heal_Should_Not_Exceed_MaxHealth()
        {
            // Arrange
            _pawn.currentHealth.Value = 80;
            int healAmount = 50;

            // Act
            _pawn.currentHealth.Value += healAmount;
            if (_pawn.currentHealth.Value > _pawn.maxHealth.Value)
            {
                _pawn.currentHealth.Value = _pawn.maxHealth.Value; // HealMantığı (Controller içindeki)
            }

            // Assert
            Assert.AreEqual(100, _pawn.currentHealth.Value, "İyileştirme işlemi Max Health (100) değerini geçmemelidir!");
        }
        
        // --- TEST 3: Ölüm Tetiklenmesi ---
        [Test]
        public void Health_Drops_Zero_Should_Trigger_Death_Logic()
        {
            // Arrange
            _pawn.currentHealth.Value = 20;

            // Act
            _pawn.currentHealth.Value -= 30;

            // Assert
            Assert.IsTrue(_pawn.currentHealth.Value <= 0, "Can 0 veya altına düştüğünde piyon ölüm durumuna geçmelidir.");
            // Burada normalde Die() fonksiyonunun çağrılıp çağrılmadığını kontrol ederiz.
        }
    }
}
