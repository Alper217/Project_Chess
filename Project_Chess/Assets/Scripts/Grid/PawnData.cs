using AlperKocasalih.Chess.Grid;
using UnityEngine;
public enum Type
{
    None,
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
    public Type type;
    
    [Header("Pawn Data")]
    public string pawnName;
    public int damage;
    public int maxHealth;
    public int currentHealth;

    [Header("Attack Pattern")] 
    public MovementPattern attackPattern;

    public int attackCooldown;
    public bool isAoE;
    public int AoERadius;
    public int AoEDamageFallOff;

}
