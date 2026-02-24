using System.Collections;
using UnityEngine;
using DG.Tweening;
using TMPro;

namespace AlperKocasalih.Chess.Grid
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        #region Fields

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI turnInfoText;
        [SerializeField] private TextMeshProUGUI diceResultText;
        [SerializeField] private CanvasGroup diceUI;

        [Header("Turn State")]
        [SerializeField, ReadOnly] private int activePlayerID = 1;
        [SerializeField, ReadOnly] private int turnCount = 1;

        #endregion

        #region Properties

        public int ActivePlayerID => activePlayerID;

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        #endregion

        #region Turn Logic

        public void RollForTurn()
        {
            StartCoroutine(RollDiceRoutine());
        }

        private IEnumerator RollDiceRoutine()
        {
            if (diceUI != null)
            {
                diceUI.alpha = 0;
                diceUI.gameObject.SetActive(true);
                diceUI.DOFade(1, 0.5f);
            }

            int player1Roll = 0;
            int player2Roll = 0;

            // Simple "animation" with DOTween and random numbers
            for (int i = 0; i < 10; i++)
            {
                player1Roll = Random.Range(1, 101);
                player2Roll = Random.Range(1, 101);
                
                if (diceResultText != null)
                    diceResultText.text = $"P1: {player1Roll} | P2: {player2Roll}";
                
                yield return new WaitForSeconds(0.1f);
            }

            // Tie breaker (very unlikely with 100 sides but just in case)
            while (player1Roll == player2Roll)
            {
                player1Roll = Random.Range(1, 101);
                player2Roll = Random.Range(1, 101);
            }

            if (diceResultText != null)
                diceResultText.text = $"Final - P1: {player1Roll} | P2: {player2Roll}";

            activePlayerID = player1Roll > player2Roll ? 1 : 2;
            UpdateTurnInfoUI();

            yield return new WaitForSeconds(1.5f);

            if (diceUI != null)
            {
                diceUI.DOFade(0, 0.5f).OnComplete(() => diceUI.gameObject.SetActive(false));
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.DraftPhase);
            }
        }

        public void NextTurn()
        {
            activePlayerID = activePlayerID == 1 ? 2 : 1;
            turnCount++;
            
            UpdateTurnInfoUI();
            
            Debug.Log($"TurnManager: Player {activePlayerID}'s turn.");

            // If we have states that repeat every turn, handle them here
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.ActionPhase)
            {
                // ActionPhase within ActionPhase? 
                // In some designs, we might go back to DraftPhase every turn.
                // For now, let's keep it in ActionPhase.
            }
        }

        private void UpdateTurnInfoUI()
        {
            if (turnInfoText != null)
            {
                turnInfoText.text = $"Turn: Player {activePlayerID}";
                turnInfoText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
            }
        }

        #endregion
    }
}
