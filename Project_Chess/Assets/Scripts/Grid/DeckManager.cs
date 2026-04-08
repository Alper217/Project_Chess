using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlperKocasalih.Chess.Grid
{
    public class DeckManager : MonoBehaviour
    {
        public static DeckManager Instance { get; private set; }

        #region Fields

        [Header("Deck Settings")]
        [SerializeField] private List<CardData> allAvailableCards; // Pool of possible cards
        [SerializeField] private int initialDeckSize = 120; // Defaulting to 120 as the user requested

        [Header("Buff Rules")]
        [Tooltip("Assign all PawnData ScriptableObjects here so the manager can find which group a Type belongs to.")]
        [SerializeField] private List<PawnData> allPawnDatas = new List<PawnData>();
        [Tooltip("Assign all BuffPoolData ScriptableObjects here (one per PawnGroup). These contain BUFFS only.")]
        [SerializeField] private List<BuffPoolData> allBuffPools = new List<BuffPoolData>();

        [Header("Global Debuff Pool")]
        [Tooltip("All debuffs shared across every pawn group. One will be randomly picked per card.")]
        [SerializeField] private List<BuffData> allDebuffs = new List<BuffData>();

        [Header("Runtime")]
        [SerializeField, ReadOnly] private List<CardData> deck = new List<CardData>();
        [Header("Networking Master List")]
        [Tooltip("Contains ALL cards generated at initialization, used to map index across the network.")]
        [SerializeField, ReadOnly] private List<CardData> masterRuntimeCardList = new List<CardData>();

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        #endregion

        #region Methods

        public void InitializeDeckWithSeed(int seed)
        {
            UnityEngine.Random.InitState(seed); // Sync network generation

            if (allAvailableCards == null || allAvailableCards.Count == 0)
            {
                Debug.LogWarning("DeckManager: No available cards to populate the deck!");
                return;
            }

            deck.Clear();
            masterRuntimeCardList.Clear();
            for (int i = 0; i < initialDeckSize; i++)
            {
                // Randomly pick from the pool to populate cards
                CardData randomCardTemplate = allAvailableCards[Random.Range(0, allAvailableCards.Count)];
                
                // Instantiate so we don't modify the original ScriptableObject in the project
                CardData cardInstance = Instantiate(randomCardTemplate);
                cardInstance.name = randomCardTemplate.name; // Remove (Clone) suffix

                // Apply randomized buffs to the instantiated card
                ApplyRandomBuffs(cardInstance);

                deck.Add(cardInstance);
                masterRuntimeCardList.Add(cardInstance); // Add to master list to preserve lookup index!
            }

            Shuffle();
            Debug.Log($"DeckManager: Initialized deck with {deck.Count} cards.");
        }

        public void Shuffle()
        {
            for (int i = 0; i < deck.Count; i++)
            {
                CardData temp = deck[i];
                int randomIndex = Random.Range(i, deck.Count);
                deck[i] = deck[randomIndex];
                deck[randomIndex] = temp;
            }
        }

        public List<CardData> DrawCards(int count)
        {
            List<CardData> drawn = new List<CardData>();
            for (int i = 0; i < count; i++)
            {
                if (deck.Count > 0)
                {
                    drawn.Add(deck[0]);
                    deck.RemoveAt(0);
                }
            }
            return drawn;
        }

        public int GetCardIndex(CardData card)
        {
            if (masterRuntimeCardList == null) return -1;
            return masterRuntimeCardList.IndexOf(card);
        }

        public CardData GetCardByIndex(int index)
        {
            if (masterRuntimeCardList == null || index < 0 || index >= masterRuntimeCardList.Count) return null;
            return masterRuntimeCardList[index];
        }

        private void ApplyRandomBuffs(CardData cardInstance)
        {
            // ── Obstacle cards NEVER get buffs/debuffs ──
            if (cardInstance.isObstacleCard) return;

            if (cardInstance.runtimeBuffs == null)
                cardInstance.runtimeBuffs = new List<BuffData>();

            // ── Step 1: Pick 1 BUFF from group pool (only positive effects) ──
            BuffData selectedBuff = PickGroupBuff(cardInstance.pawnClass);

            // ── Step 2: Pick 1 DEBUFF from global pool (only negative effects) ──
            BuffData selectedDebuff = PickGlobalDebuff(selectedBuff);

            // ── Step 3: Assign both to the card ──
            if (selectedBuff != null)
            {
                cardInstance.runtimeBuffs.Add(selectedBuff);
                Debug.Log($"BUFF: {selectedBuff.buffName} ({selectedBuff.effectType})");
            }

            if (selectedDebuff != null)
            {
                cardInstance.runtimeBuffs.Add(selectedDebuff);
                Debug.Log($"DEBUFF: {selectedDebuff.buffName} ({selectedDebuff.effectType})");
            }
        }

        /// <summary>
        /// Picks 1 random POSITIVE buff from the group-specific pool.
        /// Skips any entries where isPositiveEffect is false.
        /// </summary>
        private BuffData PickGroupBuff(Type pawnClass)
        {
            // Find pawn group
            PawnGroup group = PawnGroup.None;
            if (allPawnDatas != null)
            {
                foreach (var pd in allPawnDatas)
                {
                    if (pd != null && pd.type == pawnClass)
                    {
                        group = pd.pawnGroup;
                        break;
                    }
                }
            }
            if (group == PawnGroup.None) return null;

            // Find buff pool for group
            BuffPoolData pool = null;
            if (allBuffPools != null)
            {
                foreach (var p in allBuffPools)
                {
                    if (p != null && p.pawnGroup == group)
                    {
                        pool = p;
                        break;
                    }
                }
            }
            if (pool == null || pool.availableBuffs == null || pool.availableBuffs.Count == 0) return null;

            // Filter: only pick buffs that are positive
            int maxTries = 10;
            for (int i = 0; i < maxTries; i++)
            {
                BuffData candidate = pool.availableBuffs[Random.Range(0, pool.availableBuffs.Count)];
                if (candidate != null && candidate.isPositiveEffect)
                    return candidate;
            }

            Debug.LogWarning($"DeckManager: Group '{group}' pool has no positive buffs!");
            return null;
        }

        /// <summary>
        /// Picks 1 random NEGATIVE debuff from the global debuff pool.
        /// Ensures it does NOT neutralize the already-selected buff.
        /// </summary>
        private BuffData PickGlobalDebuff(BuffData selectedBuff)
        {
            if (allDebuffs == null || allDebuffs.Count == 0)
            {
                Debug.LogWarning("DeckManager: Global Debuff Pool (allDebuffs) is EMPTY! Drag your debuffs into DeckManager -> All Debuffs.");
                return null;
            }

            int maxTries = 10;
            for (int i = 0; i < maxTries; i++)
            {
                BuffData candidate = allDebuffs[Random.Range(0, allDebuffs.Count)];
                if (candidate == null) continue;

                // Only pick negative effects
                if (candidate.isPositiveEffect) continue;

                // Check neutralization against the selected buff
                if (selectedBuff != null
                    && !string.IsNullOrEmpty(candidate.neutralizationTag)
                    && candidate.neutralizationTag == selectedBuff.neutralizationTag
                    && candidate.isPositiveEffect != selectedBuff.isPositiveEffect)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        #endregion
    }
}

