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

        [TextArea(2, 5)]
        public string description;
    }
}
