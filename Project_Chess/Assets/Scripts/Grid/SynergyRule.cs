using AlperKocasalih.Chess.Grid;
using UnityEngine;

[CreateAssetMenu(fileName = "SynergyRule", menuName = "Scriptable Objects/SynergyRule")]
public class SynergyRule : ScriptableObject
{
    public string synergyName;
    [Tooltip("Required Class")]
    public SynergyGroup[]  requiredGroups;
    
    [Header("Synergy")]
    public int bonusHealth;
    public int bonusDamage;
}
