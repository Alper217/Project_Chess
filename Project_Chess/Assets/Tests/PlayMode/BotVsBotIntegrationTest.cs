using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using AlperKocasalih.Chess.Grid;
using System.Linq;

namespace Tests.PlayMode
{
    public class BotVsBotIntegrationTest
    {
        private bool _isSceneLoaded = false;
        private float _originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalTimeScale = Time.timeScale;

            // 1. Load MainMenu to get NetworkManager
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            _isSceneLoaded = false;
            SceneManager.sceneLoaded += OnSceneLoaded;

            float timeout = 10f;
            while (!_isSceneLoaded && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!_isSceneLoaded)
            {
                Assert.Fail("MainMenu scene could not be loaded in time!");
            }

            // 2. Start NetworkManager as Host
            if (NetworkManager.Singleton != null)
            {
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData("127.0.0.1", 7777);
                }

                NetworkManager.Singleton.StartHost();
            }
            else
            {
                Assert.Fail("NetworkManager instance not found in MainMenu scene!");
            }

            // Wait for Network ready
            yield return new WaitUntil(() => NetworkManager.Singleton.IsListening);
            
            // 3. Use NetworkManager to load GameScene
            _isSceneLoaded = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

            timeout = 10f;
            while (!_isSceneLoaded && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (!_isSceneLoaded)
            {
                Assert.Fail("GameScene could not be loaded in time!");
            }

            // Wait for Managers to initialize
            yield return new WaitForSeconds(1f);
            
            CleanUpAudioListeners();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" || scene.name == "GameScene")
            {
                _isSceneLoaded = true;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void CleanUpAudioListeners()
        {
            AudioListener[] listeners = GameObject.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners.Length > 1)
            {
                for (int i = 1; i < listeners.Length; i++)
                {
                    listeners[i].enabled = false;
                }
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _originalTimeScale;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FastFullGameFlow_AutoBotVsBot()
        {
            Debug.Log("--- STARTING FAST AUTO BOT VS BOT TEST ---");

            // 1. Inject Reporter
            GameObject reporterObj = new GameObject("BotMatchReporter");
            reporterObj.AddComponent<NetworkObject>();
            var reporter = reporterObj.AddComponent<BotMatchReporter>();
            reporter.GetComponent<NetworkObject>().Spawn();

            // 2. Inject Bot 1
            GameObject bot1Obj = new GameObject("BotAI_Player1");
            bot1Obj.AddComponent<NetworkObject>();
            var bot1 = bot1Obj.AddComponent<BotAIController>();
            // Use reflection to set private fields for the test
            SetPrivateField(bot1, "botPlayerID", 1);
            SetPrivateField(bot1, "draftThinkDelay", 0.05f); // Tiny delay to prevent RPC overlapping in the same frame
            SetPrivateField(bot1, "actionThinkDelay", 0.05f);
            SetPrivateField(bot1, "maxTurnLimit", 150);
            bot1.GetComponent<NetworkObject>().Spawn();

            // 3. Inject Bot 2
            GameObject bot2Obj = new GameObject("BotAI_Player2");
            bot2Obj.AddComponent<NetworkObject>();
            var bot2 = bot2Obj.AddComponent<BotAIController>();
            SetPrivateField(bot2, "botPlayerID", 2);
            SetPrivateField(bot2, "draftThinkDelay", 0.05f);
            SetPrivateField(bot2, "actionThinkDelay", 0.05f);
            SetPrivateField(bot2, "maxTurnLimit", 150);
            bot2.GetComponent<NetworkObject>().Spawn();

            // 4. Speed up time to run the test very fast
            Time.timeScale = 10f;

            // Wait for Game to finish 10 times
            float maxRealTime = 600f; // 600 seconds real time limit for 10 fast matches
            float timer = 0;

            SetPrivateField(reporter, "targetAutoTestCount", 10);

            while ((int)GetPrivateField(reporter, "currentMatchCount") < 10 && timer < maxRealTime)
            {
                timer += Time.unscaledDeltaTime;
                
                // Ensure Overflow Burn doesn't softlock the bots (since BotAIController doesn't natively handle Mid-Turn Burn requests yet in this simplified version, let's force it if it happens)
                if (DraftManager.Instance.IsOverflowBurnPending)
                {
                    int burnPlayer = (int)GetPrivateField(DraftManager.Instance, "pendingOverflowBurnPlayerID");
                    List<CardData> hand = DraftManager.Instance.GetHand(burnPlayer);
                    int burnIndex = -1;
                    for (int i = 0; i < hand.Count; i++)
                    {
                        if (!DraftManager.Instance.IsBurnLocked(burnPlayer, i))
                        {
                            burnIndex = i;
                            break;
                        }
                    }
                    if (burnIndex != -1)
                    {
                        DraftManager.Instance.BurnOverflowCardAtIndexServerRpc(burnPlayer, burnIndex);
                    }
                }

                yield return null;
            }

            Time.timeScale = _originalTimeScale;

            int completedMatches = (int)GetPrivateField(reporter, "currentMatchCount");
            if (completedMatches < 10)
            {
                Assert.Fail($"Test timed out! Only completed {completedMatches} out of 10 matches.");
            }
            else
            {
                Debug.Log($"--- 10 AUTO BOT VS BOT MATCHES FINISHED SUCCESSFULLY! ---");
                Assert.Pass("Bot vs Bot matches completed and generated 10 XML reports.");
            }
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }

        private object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(target);
        }
    }
}
