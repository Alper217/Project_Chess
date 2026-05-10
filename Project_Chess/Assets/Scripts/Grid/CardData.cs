using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    /// <summary>
    /// ScriptableObject representing a movement card.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "Chess/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string cardName;
        public Sprite cardSprite;
        public Sprite cardDesign; // NEW: The full design/background of the card

        [Header("Effect")]
        public MovementPattern pattern;
        

        public int healthToAdd; // Legacy, kept for compatibility
        public int damageToAdd; // Legacy, kept for compatibility

        [Header("Dynamic Effects (Runtime)")]
        [Tooltip("These buffs are generated and injected dynamically at runtime by the DeckManager.")]
        public System.Collections.Generic.List<BuffData> runtimeBuffs = new System.Collections.Generic.List<BuffData>();

        [Tooltip("Class")]
        public Type pawnClass;
        
        [Header("Type Mismatch")]
        public MovementPattern mismatchPattern;
        
        [Header("Obstacle Settings")]
        [Tooltip("If true, playing this card places an obstacle instead of moving a pawn.")]
        public bool isObstacleCard;
        [Tooltip("The shape of the obstacle placed on the grid.")]
        public ObstaclePattern obstaclePattern;

        [TextArea(2, 5)]
        public string description;

        /// <summary>
        /// Gets a formatted string of the applied runtime buffs to display on the UI.
        /// </summary>
        public string GetBuffsText()
        {
            if (runtimeBuffs == null || runtimeBuffs.Count == 0) return "";
            string desc = "";
            foreach (var buff in runtimeBuffs)
            {
                if (buff == null) continue;
                string sign = buff.amount > 0 ? "+" : "";
                string percent = buff.isPercentage ? "%" : "";
                string color = buff.isPositiveEffect ? "#0b6d0eff" : "#c90000ff";
                
                string displayName = !string.IsNullOrEmpty(buff.effectDescription) ? buff.effectDescription : buff.effectType.ToString();
                
                if (desc != "") desc += "\n";
                desc += $"<color={color}><size=80%>{displayName}</size></color>";
            }
            return desc;
        }
    }
}

