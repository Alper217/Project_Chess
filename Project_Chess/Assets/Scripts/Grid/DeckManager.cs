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
        [SerializeField] private int initialDeckSize = 200;

        [Header("Runtime")]
        [SerializeField, ReadOnly] private List<CardData> deck = new List<CardData>();

        #endregion

        #region Unity Methods

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            InitializeDeck();
        }

        #endregion

        #region Methods

        public void InitializeDeck()
        {
            if (allAvailableCards == null || allAvailableCards.Count == 0)
            {
                Debug.LogWarning("DeckManager: No available cards to populate the deck!");
                return;
            }

            deck.Clear();
            for (int i = 0; i < initialDeckSize; i++)
            {
                // Randomly pick from the pool to populate 200 cards
                CardData randomCard = allAvailableCards[Random.Range(0, allAvailableCards.Count)];
                deck.Add(randomCard);
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

        #endregion
    }
}
