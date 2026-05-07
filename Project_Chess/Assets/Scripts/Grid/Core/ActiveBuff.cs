using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace AlperKocasalih.Chess.Grid
{


    [System.Serializable]
    public class ServerActiveBuff
    {
        public BuffData buffData;
        public int remainingTurns;
        public float effectiveness = 1f;

        public ServerActiveBuff(BuffData data, float effectiveness = 1f)
        {
            buffData = data;
            remainingTurns = data.durationTurns;
            this.effectiveness = effectiveness;
        }
    }
}
