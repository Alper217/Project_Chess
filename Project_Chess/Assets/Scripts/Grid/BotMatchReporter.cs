using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// Win condition type for the XML report.
    /// </summary>
    public enum WinConditionType
    {
        AllEnemyPawnsEliminated,
        PointAdvantage,
        Draw
    }

    /// <summary>
    /// Generates an XML match report at the end of a Bot vs Bot game.
    /// Attach to a GameObject in the scene; subscribe to GameManager.OnGameEnded.
    /// The report is saved to Application.persistentDataPath.
    /// </summary>
    public class BotMatchReporter : NetworkBehaviour
    {
        public static BotMatchReporter Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Folder name inside Application.persistentDataPath for reports.")]
        [SerializeField] private string reportFolder = "BotVsBotReports";

        [SerializeField] private bool verboseLog = true;

        // ───────────────────── Runtime ─────────────────────

        // Set by external code (e.g. GameManager) before EndGame is called.
        private WinConditionType pendingWinCondition = WinConditionType.AllEnemyPawnsEliminated;

        // References to bot controllers (auto-discovered at runtime)
        private BotAIController bot1;
        private BotAIController bot2;

        // ───────────────────── Unity/Network ─────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameEnded += HandleGameEnded;
        }

        public override void OnNetworkDespawn()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameEnded -= HandleGameEnded;
            base.OnNetworkDespawn();
        }

        // ───────────────────── Public API ─────────────────────

        /// <summary>
        /// Call this before the game ends to set the reason for victory.
        /// </summary>
        public void SetWinCondition(WinConditionType condition)
        {
            pendingWinCondition = condition;
        }

        // ───────────────────── Report Generation ─────────────────────

        private void HandleGameEnded(int winnerID)
        {
            if (!IsServer) return;
            DiscoverBotControllers();
            GenerateReport(winnerID);
        }

        private void DiscoverBotControllers()
        {
            bot1 = null;
            bot2 = null;

            BotAIController[] bots = FindObjectsByType<BotAIController>(FindObjectsSortMode.None);
            foreach (var b in bots)
            {
                // Reflect on field via property pattern — both bots expose playerID through log tag.
                // We use a helper method on BotAIController instead.
                if (b.BotPlayerID == 1) bot1 = b;
                else if (b.BotPlayerID == 2) bot2 = b;
            }
        }

        private void GenerateReport(int winnerID)
        {
            try
            {
                // ── Collect data ──
                string gameID    = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                int p1Score = GameManager.Instance != null ? GameManager.Instance.player1Score.Value : 0;
                int p2Score = GameManager.Instance != null ? GameManager.Instance.player2Score.Value : 0;
                int turnCount = TurnManager.Instance != null ? TurnManager.Instance.TurnCount : 0;

                // ── Build XML ──
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent      = true,
                    Encoding    = Encoding.UTF8,
                    NewLineChars = Environment.NewLine
                };

                string dirPath  = Path.Combine(Application.persistentDataPath, reportFolder);
                Directory.CreateDirectory(dirPath);
                string filePath = Path.Combine(dirPath, $"Report_{gameID}_{DateTime.Now:yyyyMMdd_HHmmss}.xml");

                using (XmlWriter writer = XmlWriter.Create(filePath, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("BotVsBotReport");

                    // ── Header ──
                    writer.WriteElementString("GameID",    gameID);
                    writer.WriteElementString("Timestamp", timestamp);
                    writer.WriteElementString("TotalTurns", turnCount.ToString());

                    // ── Player 1 ──
                    WritePlayerSection(writer, 1, bot1);

                    // ── Player 2 ──
                    WritePlayerSection(writer, 2, bot2);

                    // ── Result ──
                    writer.WriteStartElement("Result");
                    writer.WriteElementString("WinnerID",     winnerID.ToString());
                    writer.WriteElementString("WinCondition", pendingWinCondition.ToString());
                    writer.WriteStartElement("FinalScore");
                    writer.WriteAttributeString("p1", p1Score.ToString());
                    writer.WriteAttributeString("p2", p2Score.ToString());
                    writer.WriteEndElement(); // FinalScore
                    writer.WriteEndElement(); // Result

                    writer.WriteEndElement(); // BotVsBotReport
                    writer.WriteEndDocument();
                }

                if (verboseLog)
                    Debug.Log($"[BotMatchReporter] Report saved: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BotMatchReporter] Failed to generate report: {ex.Message}");
            }
        }

        private void WritePlayerSection(XmlWriter writer, int playerID, BotAIController bot)
        {
            writer.WriteStartElement("Player");
            writer.WriteAttributeString("id", playerID.ToString());

            // Selected pawns
            writer.WriteStartElement("SelectedPawns");
            if (bot != null && bot.SelectedPawnDatas != null)
            {
                foreach (var pd in bot.SelectedPawnDatas)
                {
                    if (pd == null) continue;
                    float powerScore = pd.damage * 2f + pd.maxHealth;

                    writer.WriteStartElement("Pawn");
                    writer.WriteAttributeString("name",       pd.pawnName);
                    writer.WriteAttributeString("type",       pd.type.ToString());
                    writer.WriteAttributeString("powerScore", powerScore.ToString("F0"));
                    writer.WriteAttributeString("damage",     pd.damage.ToString());
                    writer.WriteAttributeString("maxHealth",  pd.maxHealth.ToString());
                    writer.WriteEndElement(); // Pawn
                }
            }
            writer.WriteEndElement(); // SelectedPawns

            // Remaining alive pawns
            writer.WriteStartElement("SurvivingPawns");
            Pawn[] allPawns = FindObjectsByType<Pawn>(FindObjectsSortMode.None);
            int survivorCount = 0;
            foreach (var p in allPawns)
            {
                if (p != null && p.IsSpawned && p.PlayerID == playerID && p.currentHealth.Value > 0)
                {
                    survivorCount++;
                    writer.WriteStartElement("Pawn");
                    writer.WriteAttributeString("name",   p.PawnData?.pawnName ?? "Unknown");
                    writer.WriteAttributeString("hp",     p.currentHealth.Value.ToString());
                    writer.WriteAttributeString("maxHp",  p.maxHealth.Value.ToString());
                    writer.WriteEndElement();
                }
            }
            writer.WriteAttributeString("count", survivorCount.ToString());
            writer.WriteEndElement(); // SurvivingPawns

            // Move count
            int totalMoves = bot != null ? bot.TotalMoves : 0;
            writer.WriteElementString("TotalMoves", totalMoves.ToString());

            writer.WriteEndElement(); // Player
        }
    }
}
