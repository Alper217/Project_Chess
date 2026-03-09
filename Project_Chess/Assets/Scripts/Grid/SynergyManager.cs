using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using AlperKocasalih.Chess.Grid;

public class SynergyManager : NetworkBehaviour
{
    public static SynergyManager instance;
    [Tooltip("Synergies")]
    public SynergyRule[] synergyRules;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

   public void EvaluateSynergiesOnServer(int playerID)
    {
        if (!IsServer) return;

        List<Pawn> playerPawns = GetAllActivePawnsOfPlayer(playerID);

        Dictionary<SynergyGroup, int> activeGroupCounts = new Dictionary<SynergyGroup, int>();
        
        foreach (Pawn p in playerPawns)
        {
            if (p.PawnType != null)
            {
                SynergyGroup sg = p.PawnType.synergyGroup;

                if (activeGroupCounts.ContainsKey(sg))
                    activeGroupCounts[sg]++;
                else
                    activeGroupCounts[sg] = 1;
            }
            p.ResetSynergyServer(); 
        }

        foreach (SynergyRule rule in synergyRules)
        {
            Dictionary<SynergyGroup, int> requiredCounts = new Dictionary<SynergyGroup, int>();
            foreach (SynergyGroup req in rule.requiredGroups)
            {
                if (requiredCounts.ContainsKey(req)) requiredCounts[req]++;
                else requiredCounts[req] = 1;
            }

            bool isRuleMet = true;

            foreach (var kvp in requiredCounts)
            {
                SynergyGroup reqGroup = kvp.Key;
                int reqAmount = kvp.Value;

                if (!activeGroupCounts.ContainsKey(reqGroup) || activeGroupCounts[reqGroup] < reqAmount)
                {
                    isRuleMet = false;
                    break;
                }
            }
            if (isRuleMet)
            {
                foreach (Pawn p in playerPawns)
                {
                    if (System.Array.Exists(rule.requiredGroups, g => g == p.PawnType.synergyGroup))
                    {
                        p.ApplyBuffsServer(rule.bonusHealth, rule.bonusDamage);
                    }
                }
            }
        }
    }

    private List<Pawn> GetAllActivePawnsOfPlayer(int playerID)
    {
        List<Pawn> result = new List<Pawn>();
        Pawn[] allPawns = FindObjectsOfType<Pawn>();

        foreach (Pawn pawn in allPawns)
        {
            if (pawn.PlayerID == playerID)
            {
                result.Add(pawn);
            }
        }
        return result;
    }
}
