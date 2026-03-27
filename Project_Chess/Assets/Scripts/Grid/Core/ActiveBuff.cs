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

        public ServerActiveBuff(BuffData data)
        {
            buffData = data;
            remainingTurns = data.durationTurns;
        }
    }
}
