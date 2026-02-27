using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

namespace AlperKocasalih.Chess.Grid
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private CanvasGroup gameOverPanel;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private Button restartButton;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameEnded += ShowGameOver;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            // Initially hidden
            if (gameOverPanel != null)
            {
                gameOverPanel.alpha = 0;
                gameOverPanel.gameObject.SetActive(false);
            }
        }

        private void ShowGameOver(int winnerID)
        {
            if (gameOverPanel == null) return;

            if (winnerText != null)
            {
                winnerText.text = $"Player {winnerID} Wins!";
                winnerText.color = winnerID == 1 ? Color.cyan : new Color(1f, 0.5f, 0f);
            }

            gameOverPanel.gameObject.SetActive(true);
            gameOverPanel.DOFade(1, 0.5f);
            
            // Subtle animation for text
            if (winnerText != null)
            {
                winnerText.transform.localScale = Vector3.zero;
                winnerText.transform.DOScale(Vector3.one, 0.8f).SetEase(Ease.OutBack);
            }
        }

        private void OnRestartClicked()
        {
            if (restartButton != null)
            {
                restartButton.interactable = false;
            }

            if (winnerText != null)
            {
                winnerText.text = "Waiting for opponent...";
                winnerText.color = Color.white;
            }

            if (GameManager.Instance != null && GameManager.Instance.NetworkObject.IsSpawned)
            {
                GameManager.Instance.RequestRestartServerRpc();
            }
            else
            {
                // Fallback for single player/offline testing
                if (gameOverPanel != null)
                {
                    gameOverPanel.DOFade(0, 0.3f).OnComplete(() => {
                        gameOverPanel.gameObject.SetActive(false);
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.RestartGame();
                        }
                    });
                }
            }
        }
    }
}
