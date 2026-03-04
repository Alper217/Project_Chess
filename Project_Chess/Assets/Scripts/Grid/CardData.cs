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

        [Header("Effect")]
        public MovementPattern pattern;
        
        [Header("Obstacle Settings")]
        [Tooltip("If true, playing this card places an obstacle instead of moving a pawn.")]
        public bool isObstacleCard;
        [Tooltip("The shape of the obstacle placed on the grid.")]
        public ObstaclePattern obstaclePattern;

        [TextArea(2, 5)]
        public string description;
    }
}
