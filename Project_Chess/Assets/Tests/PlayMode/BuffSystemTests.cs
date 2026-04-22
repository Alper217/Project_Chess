using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using AlperKocasalih.Chess.Grid;

namespace Tests.PlayMode
{
    public class BuffSystemTests
    {
        private GameObject _networkManagerGo;
        private NetworkManager _networkManager;

        private GameObject _pawnObject;
        private Pawn _pawn;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManagerGo = new GameObject("TestNetworkManager");
            _networkManager = _networkManagerGo.AddComponent<NetworkManager>();
            var transport = _networkManagerGo.AddComponent<UnityTransport>();
            _networkManager.NetworkConfig = new NetworkConfig() { NetworkTransport = transport };
            _networkManager.StartHost();
            yield return null;

            _pawnObject = new GameObject("TestPawn");
            _pawnObject.AddComponent<NetworkObject>();
            _pawn = _pawnObject.AddComponent<Pawn>();

            var dummyData = ScriptableObject.CreateInstance<PawnData>();
            dummyData.maxHealth = 100;
            dummyData.damage = 10;
            typeof(Pawn).GetField("pawnData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_pawn, dummyData);

            _pawnObject.GetComponent<NetworkObject>().Spawn();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_networkManager != null && _networkManager.IsListening) _networkManager.Shutdown();
            yield return null;
            if (_pawnObject != null) Object.DestroyImmediate(_pawnObject);
            if (_networkManagerGo != null) Object.DestroyImmediate(_networkManagerGo);
        }

        [UnityTest]
        public IEnumerator BuffSystem_TimedBuff_ShouldPersistAcrossSynergyReset()
        {
            BuffData debuff = ScriptableObject.CreateInstance<BuffData>();
            debuff.durationTurns = 2;
            debuff.isPositiveEffect = false;
            debuff.effectType = EffectType.OutgoingDamageModifier;
            debuff.amount = -5;

            _pawn.ApplyRuntimeBuffsServer(new List<BuffData> { debuff });
            _pawn.ResetSynergyServer();

            Assert.AreEqual(1, _pawn.activeBuffs.Count, "ResetSynergyServer timed buff'ı silmemeli.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuffSystem_ZeroDurationBuff_ShouldExpireOnFirstTick()
        {
            // Bu test artık 'Sınırsız değil' kuralını doğruluyor.
            BuffData zeroBuff = ScriptableObject.CreateInstance<BuffData>();
            zeroBuff.durationTurns = 0; 
            zeroBuff.isPositiveEffect = true;
            zeroBuff.effectType = EffectType.MovementRangeModifier;

            _pawn.ApplyRuntimeBuffsServer(new List<BuffData> { zeroBuff });
            Assert.AreEqual(1, _pawn.activeBuffs.Count);

            // Act: Bir kez tur geç
            _pawn.TickBuffsServer();

            // Assert: Silinmiş olmalı (Artık sınırsız değil)
            Assert.AreEqual(0, _pawn.activeBuffs.Count, "HATA: Duration 0 olan buff tur sonunda silinmedi (Sınırsız kalma hatası).");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuffSystem_TimedBuff_ShouldExpireCorrectly()
        {
            BuffData timedBuff = ScriptableObject.CreateInstance<BuffData>();
            timedBuff.durationTurns = 1;
            timedBuff.effectType = EffectType.Stun;

            _pawn.ApplyRuntimeBuffsServer(new List<BuffData> { timedBuff });
            _pawn.TickBuffsServer();

            Assert.AreEqual(0, _pawn.activeBuffs.Count, "HATA: 1 turluk buff tur sonunda silinmedi.");
            yield return null;
        }
    }
}
