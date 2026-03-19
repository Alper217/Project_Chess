using UnityEngine;
public enum SynergyGroup
{
    Archer,
    Cannon,
    Knight,
    Queen,
    Horseman,
    Commander,
    Ninja,
    Cheriff,
    Wizard
}
[CreateAssetMenu(fileName = "PawnSO", menuName = "Scriptable Objects/PawnSO")]
public class PawnData: ScriptableObject
{
    public SynergyGroup synergyGroup;
    
    
    [Header("Pawn Data")]
    public string pawnName;
    public int damage;
    public int maxHealth;
    public int currentHealth;
}
