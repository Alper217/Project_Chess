using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    [CreateAssetMenu(fileName = "NewBuffPoolData", menuName = "Chess/Buff Pool Data")]
    public class BuffPoolData : ScriptableObject
    {
        [Header("Pool Settings")]
        [Tooltip("The group of pawns this pool belongs to.")]
        public PawnGroup pawnGroup;

        [Tooltip("The 3-4 possible buffs/debuffs that this group can receive.")]
        public List<BuffData> availableBuffs = new List<BuffData>();
    }
}
