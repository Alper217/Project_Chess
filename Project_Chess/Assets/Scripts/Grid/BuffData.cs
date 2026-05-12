using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public enum EffectType
    {
        None,
        MaxHealth,
        CurrentHealth,
        OutgoingDamageModifier,
        IncomingDamageModifier,
        MovementRangeModifier,
        Lifesteal,
        Recoil,
        DamageBlock,
        DebuffImmunity,
        Stun,
        DoubleUse,
        DebuffAmplifier
    }

    [CreateAssetMenu(fileName = "NewBuffData", menuName = "Chess/Buff Data")]
    public class BuffData : ScriptableObject
    {
        [Header("Buff Identity")]
        public string buffName;
        public Sprite icon;
        public EffectType effectType;

        [Tooltip("Human-readable description of the effect (e.g., 'Outgoing Damage', 'Can Calma').")]
        public string effectDescription;
        
        [Tooltip("Amount of the effect. Can be flat (+5) or percentage (+15%).")]
        public float amount;
        
        [Tooltip("If true, the amount is treated as a percentage (e.g., 20 = 20%).")]
        public bool isPercentage;

        [Tooltip("How many turns this buff will last. 0 means it's an instant/permanent effect.")]
        public int durationTurns = 1;

        [Header("Neutralization")]
        [Tooltip("The ID or tag of the buff that neutralizes this one. For example, HealthBoost has neutralizingTag 'Health', HealthDebuff also has 'Health'. If two cards have the same polarizing tag, they cancel out.")]
        public string neutralizationTag;
        
        [Tooltip("Is this buff positive (+) or negative (-)? Used to check neutralization. A positive and negative buff with the same neutralization tag will cancel out.")]
        public bool isPositiveEffect;

        // Optionally, support explicitly opposite buffs instead of string tags
        // public List<BuffData> explicitlyOpposingBuffs;
    }
}
