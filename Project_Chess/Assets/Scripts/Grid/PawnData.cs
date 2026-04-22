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
    public PawnGroup pawnGroup;
    
    [Header("Pawn Data")]
    public string pawnName;
    public int damage;
    public int maxHealth;
    public int currentHealth;
    public int pointValue; // Added this line

    [Header("Attack Pattern")] 
    public MovementPattern attackPattern;
    public bool startWithForceAttackPattern;

    public int attackCooldown;
    [Header("AOE Effects")]
    public bool isAoE;
    public int AoERadius;
    public int AoEDamageFallOff;
    
    [Header("Aura Settings")]
    public bool hasAura;
    public int auraRadius = 2;
    public int damageBuff;
    public int healthbuff;

    [Header("Heal Effects")] 
    public bool isHealer;
    public int healAmount;


}
