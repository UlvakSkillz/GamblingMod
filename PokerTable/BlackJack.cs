using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppTMPro;
using MelonLoader;
using System.Collections;
using UnityEngine;


namespace GamblingMod
{
    public class BlackJack : MonoBehaviour
    {
        private int betAmount = 1, betHeldAmount = 0;
        private bool gameLoopRunning = false;
        private TextMeshPro betTextComponent = null;
        private GameObject storedBetsMenu = null, storedOptionsMenu = null;
        private bool betAccepted = false, userQuits = false;
        private List<int> deck, dealerHand;
        private List<List<int>> hand;
        public object gameLoopCoroutine = null;
        private Transform storedGamePartsTransform = null, activeGamePartsTransform = null;
        private Table tableInstance;
        private GameObject cardSpots = null;
        private bool has21Immediately = false;

        public BlackJack(Table tableInstance)
        {
            this.tableInstance = tableInstance;
        }

        public static void Log(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Table.Log($"BlackJack - {msg}", sendMsg);
            }
        }

        public static void Warn(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Table.Warn($"BlackJack - {msg}", sendMsg);
            }
        }

        public static void Error(string msg)
        {
            Table.Error($"BlackJack - {msg}");
        }

        public void ShowSplash()
        {
            storedGamePartsTransform = tableInstance.transform.GetChild(1);
            activeGamePartsTransform = tableInstance.transform.GetChild(2);
            GameObject splashScreen = new GameObject("SplashScreen");
            splashScreen.transform.SetParent(activeGamePartsTransform);
            splashScreen.transform.localPosition = new Vector3(0, 0.9454f, 0);
            splashScreen.transform.localRotation = Quaternion.identity;
            splashScreen.transform.localScale = Vector3.one;
            GameObject splashScreenText = tableInstance.SpawnText(splashScreen.transform,
                /*Title*/"BlackJack",
                /*position*/ new Vector3(0f, 0f, 0.4f),
                /*rotation*/Quaternion.Euler(90f, 180f, 0f),
                /*scale*/ new Vector3(1.25f, 1.25f, 1.25f));
            TextMeshPro splashScreenTextTMP = splashScreenText.GetComponent<TextMeshPro>();
            splashScreenTextTMP.alignment = TextAlignmentOptions.Center;
            splashScreenTextTMP.enableWordWrapping = false;
        }

        public IEnumerator Run()
        {
            Log("Run Running", Preferences.debugging.Value);
            SetupStart();
            gameLoopCoroutine = MelonCoroutines.Start(GameLoop());
            yield return gameLoopCoroutine;
            Log("Run Completed", Preferences.debugging.Value);
            yield break;
        }

        private void SetupStart()
        {
            Log("SetupStart Running", Preferences.debugging.Value);
            gameLoopRunning = false;
            RandomizeDeck();
            SetupBetMenu();
            SetupOptionsMenu();
            Log("SetupStart Completed", Preferences.debugging.Value);
        }

        private void SetupCardSpots()
        {
            cardSpots = GameObject.Instantiate(storedGamePartsTransform.GetChild(1).gameObject);
            cardSpots.name = "CardSpots";
            cardSpots.transform.SetParent(activeGamePartsTransform);
            cardSpots.transform.localPosition = Vector3.zero;
            cardSpots.transform.localRotation = Quaternion.identity;
            cardSpots.transform.localScale = Vector3.one;
        }

        private int coinsGained = 0;
        private IEnumerator GameLoop()
        {
            Log("GameLoop Running", Preferences.debugging.Value);
            gameLoopRunning = true;
            while (gameLoopRunning)
            {
                //reshuffle if needed
                if (deck.Count <= 26 * Preferences.deckCount.Value)
                {
                    RandomizeDeck(false);
                    yield return new WaitForSeconds(1f);
                }
                //check for coins
                betAmount = 1;
                if (!tableInstance.freePlay && Main.GetPlayerCoinCount() < betAmount)
                {
                    GameObject youBrokeAsFuck = tableInstance.SpawnText(activeGamePartsTransform.transform,
                        /*Title*/$"Too Few Coins. Try Free Play or{Environment.NewLine}Complete More Poses to Earn Coins",
                        /*position*/ new Vector3(0f, tableInstance.TABLEHEIGHT - 0.025f, 0.65f),
                        /*rotation*/Quaternion.Euler(90, 180, 0),
                        /*scale*/ new Vector3(1f, 1f, 1f));
                    youBrokeAsFuck.name = "You Broke As Fuck";
                    TextMeshPro endGameTextTMP = youBrokeAsFuck.GetComponent<TextMeshPro>();
                    endGameTextTMP.alignment = TextAlignmentOptions.Center;
                    endGameTextTMP.enableWordWrapping = false;
                    continueShuffling = false;
                    Log("Not Enough Coins, Exiting BlackJack", Preferences.debugging.Value);
                    yield return new WaitForSeconds(1.5f);
                    gameLoopRunning = false;
                    break;
                }
                //starting bet
                tableInstance.ClearActiveObjects();
                yield return MelonCoroutines.Start(RunBetMenu());
                tableInstance.ClearActiveObjects();
                yield return new WaitForSeconds(1.5f);
                //-user quits
                if (userQuits)
                {
                    gameLoopRunning = false;
                }
                //bet accepted
                if (betAccepted)
                {
                    Log("Player Bet Accepted: " + betAmount, true);
                    Main.Payout(-betAmount, 1f, tableInstance);
                    betHeldAmount = betAmount;
                    coinsGained = 0;
                    //players turn
                    yield return MelonCoroutines.Start(PlayHands());
                    //dealers turn
                    yield return MelonCoroutines.Start(DealersTurn());
                    //check hands compared to dealer
                    List<int> playerHandTotals = GetPlayerHandTotal();
                    int dealerHandTotal = GetDealerHandTotal();
                    for (int i = playerHandTotals.Count - 1; i >= 0; i--)
                    {
                        if (!gameLoopRunning) { break; }
                        //clear active cards
                        ClearVisualCardsFromAHand(true);
                        ClearVisualCardsFromAHand(false);
                        //spawn that hands cards
                        List<GameObject> handCards = SpawnHandCards(i);
                        List<GameObject> dealerCards = SpawnDealerCards();
                        yield return new WaitForSeconds(0.25f);
                        object endGameCoroutine0 = null;
                        object endGameCoroutine1 = null;
                        string cardString0 = "";
                        foreach (int card in hand[i])
                        {
                            cardString0 += tableInstance.CardString[card] + " ";
                        }
                        string cardString1 = "";
                        foreach (int card in dealerHand)
                        {
                            cardString1 += tableInstance.CardString[card] + " ";
                        }
                        Log($"Checking Hand {i}: Player {playerHandTotals[i]} ( {cardString0}) vs Dealer {dealerHandTotal} ( {cardString1})", true);
                        yield return new WaitForSeconds(0.5f);
                        //if blackjack
                        if (has21Immediately)
                        {
                            Log($"Player Got 21 Immediately! Gained {betAmount * 2} Coins", true);
                            coinsGained += Main.Payout(betAmount, 2f, tableInstance);
                            endGameCoroutine0 = MelonCoroutines.Start(PlayPlayerWin(handCards, dealerCards));
                        }
                        //player Bust
                        else if (playerHandTotals[i] > 21)
                        {
                            Log($"Player Bust, Lost {betAmount} Coins", true);
                            betHeldAmount -= betAmount;
                            coinsGained -= betAmount;
                            endGameCoroutine0 = MelonCoroutines.Start(PlayPlayerBust(handCards));
                        }
                        //if player beat dealer or if dealer bust
                        else if ((playerHandTotals[i] > dealerHandTotal) || ((dealerHandTotal > 21)))
                        {
                            Log($"Player Beat Dealer! Gained {(int)(((float)betAmount) * 1.5f)} Coins", true);
                            coinsGained += Main.Payout(betAmount, 1.5f, tableInstance);
                            endGameCoroutine0 = MelonCoroutines.Start(PlayPlayerWin(handCards, dealerCards));
                        }
                        //if player didnt beat dealer
                        else
                        {
                            Log($"Player Didn't Beat Dealer, Lost {betAmount} Coins", true);
                            betHeldAmount -= betAmount;
                            coinsGained -= betAmount;
                            endGameCoroutine0 = MelonCoroutines.Start(PlayDealerWin(handCards, dealerCards));
                        }
                        if (dealerHandTotal > 21)
                        {
                            endGameCoroutine1 = MelonCoroutines.Start(PlayDealerBust(dealerCards));
                        }
                        yield return endGameCoroutine0;
                        yield return endGameCoroutine1;
                        yield return new WaitForSeconds(0.25f);
                        //destroy the cards
                        foreach (GameObject handCard in handCards) { GameObject.Destroy(handCard); }
                        foreach (GameObject dealerCard in dealerCards) { GameObject.Destroy(dealerCard); }
                    }
                    Main.Payout(betHeldAmount, 1f, tableInstance);
                    bool continuePressed = false;
                    string coinLine = coinsGained < 0 ? $"Coins Lost: {-coinsGained}" : $"Coins Gained: {coinsGained}";
                    //load end game text
                    GameObject endGameText = tableInstance.SpawnText(activeGamePartsTransform.transform,
                        /*Title*/coinLine,
                        /*position*/ new Vector3(0f, tableInstance.TABLEHEIGHT - 0.025f, 0.5f),
                        /*rotation*/Quaternion.Euler(90, 180, 0),
                        /*scale*/ new Vector3(1f, 1f, 1f));
                    endGameText.name = "End Game Text";
                    TextMeshPro endGameTextTMP = endGameText.GetComponent<TextMeshPro>();
                    endGameTextTMP.alignment = TextAlignmentOptions.Center;
                    endGameTextTMP.enableWordWrapping = false;
                    //load continue button
                    GameObject continueButton = tableInstance.LoadMenuButton("Continue",
                        /*position*/ new Vector3(0f, tableInstance.TABLEHEIGHT - 0.025f, 0.96f),
                        /*rotation*/Quaternion.Euler(0, 0, 0),
                        /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                        () => { continuePressed = true; });
                    continueButton.name = "Continue";
                    //wait for continue to be pressed
                    while (!continuePressed && gameLoopRunning)
                    {
                        yield return new WaitForFixedUpdate();
                    }
                }
                tableInstance.ClearActiveObjects();
            }
            Log("GameLoop Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayPlayerWin(List<GameObject> handCards, List<GameObject> dealerCards)
        {
            Log("PlayPlayerWin Started", Preferences.debugging.Value);
            List<object> cardsReacting = new List<object>();
            for (int i = 0; i < handCards.Count; i++)
            {
                cardsReacting.Add(PlayCardReact(handCards[i], true, true));
            }
            for (int i = 0; i < dealerCards.Count; i++)
            {
                cardsReacting.Add(PlayCardReact(dealerCards[i], false, false));
            }
            foreach (object card in cardsReacting)
            {
                yield return card;
            }
            Log("PlayPlayerWin Completed", Preferences.debugging.Value);
            yield break;
        }

        private object PlayCardReact(GameObject card, bool win, bool isPlayer)
        {
            return MelonCoroutines.Start(PlayCardReactCoroutine(card, win, isPlayer));
        }

        private IEnumerator PlayCardReactCoroutine(GameObject card, bool win, bool isPlayer)
        {
            Vector3 startingPostion = card.transform.localPosition;
            Vector3 movePerTick = ((isPlayer ? Vector3.back : Vector3.forward) / 50f) * (win ? 0.5f : -0.25f);
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition += movePerTick;
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition -= movePerTick;
                yield return new WaitForFixedUpdate();
            }
            card.transform.localPosition = startingPostion;
            yield break;
        }

        private IEnumerator PlayPlayerBust(List<GameObject> handCards)
        {
            Log("PlayPlayerBust Started", Preferences.debugging.Value);
            List<object> cardsReacting = new List<object>();
            for (int i = 0; i < handCards.Count; i++)
            {
                cardsReacting.Add(MelonCoroutines.Start(PlayCardBustCoroutine(handCards[i])));
            }
            foreach (object card in cardsReacting)
            {
                yield return card;
            }
            Log("PlayPlayerBust Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayDealerBust(List<GameObject> handCards)
        {
            Log("PlayDealerBust Started", Preferences.debugging.Value);
            List<object> cardsReacting = new List<object>();
            for (int i = 0; i < handCards.Count; i++)
            {
                cardsReacting.Add(MelonCoroutines.Start(PlayCardBustCoroutine(handCards[i])));
            }
            foreach (object card in cardsReacting)
            {
                yield return card;
            }
            Log("PlayDealerBust Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayCardBustCoroutine(GameObject card)
        {
            Log("PlayCardBustCoroutine Started", Preferences.debugging.Value);
            Vector3 startingScale = card.transform.localScale;
            Vector3 scalePerTick = startingScale / 50f;
            for (int i = 0; i < 50; i++)
            {
                card.transform.localScale -= scalePerTick;
                yield return new WaitForFixedUpdate();
            }
            Log("PlayCardBustCoroutine Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayDealerWin(List<GameObject> handCards, List<GameObject> dealerCards)
        {
            Log("PlayDealerWin Started", Preferences.debugging.Value);
            List<object> cardsReacting = new List<object>();
            for (int i = 0; i < handCards.Count; i++)
            {
                cardsReacting.Add(PlayCardReact(handCards[i], false, true));
            }
            for (int i = 0; i < dealerCards.Count; i++)
            {
                cardsReacting.Add(PlayCardReact(dealerCards[i], true, false));
            }
            foreach (object card in cardsReacting)
            {
                yield return card;
            }
            Log("PlayDealerWin Completed", Preferences.debugging.Value);
            yield break;
        }

        private List<GameObject> SpawnHandCards(int thisHand)
        {
            List<GameObject> handsCards = new List<GameObject>();
            for (int i = 0; i < hand[thisHand].Count; i++)
            {
                GameObject aCard = GameObject.Instantiate(tableInstance.storedDeckOfCards.transform.GetChild(hand[thisHand][i]).gameObject);
                aCard.transform.SetParent(cardSpots.transform.GetChild(0).GetChild(i));
                aCard.transform.localPosition = Vector3.zero;
                aCard.transform.localRotation = Quaternion.identity;
                handsCards.Add(aCard);
            }
            return handsCards;
        }

        private List<GameObject> SpawnDealerCards()
        {
            List<GameObject> dealerCards = new List<GameObject>();
            for (int i = 0; i < dealerHand.Count; i++)
            {
                GameObject aCard = GameObject.Instantiate(tableInstance.storedDeckOfCards.transform.GetChild(dealerHand[i]).gameObject);
                aCard.transform.SetParent(cardSpots.transform.GetChild(1).GetChild(i));
                aCard.transform.localPosition = Vector3.zero;
                aCard.transform.localRotation = Quaternion.identity;
                dealerCards.Add(aCard);
            }
            return dealerCards;
        }

        private IEnumerator RevealDealerCard()
        {
            Log("RevealDealerCard Started", Preferences.debugging.Value);
            float rotationPerTick = 180f / 25f;
            Transform dealerCard = cardSpots.transform.GetChild(1).GetChild(1).GetChild(0);
            GameObject newCard = GameObject.Instantiate(tableInstance.storedDeckOfCards.transform.GetChild(dealerHand[1]).gameObject);
            newCard.transform.SetParent(dealerCard.transform.parent);
            newCard.transform.position = dealerCard.transform.position;
            newCard.transform.rotation = dealerCard.transform.rotation;
            newCard.transform.localScale = dealerCard.transform.localScale;
            float currentRotationX = newCard.transform.localRotation.eulerAngles.x;
            float currentRotationY = newCard.transform.localRotation.eulerAngles.y;
            float currentRotationZ = newCard.transform.localRotation.eulerAngles.z;
            GameObject.Destroy(dealerCard.gameObject);
            for (int i = 0; i < 25; i++)
            {
                currentRotationX -= rotationPerTick;
                newCard.transform.localRotation = Quaternion.Euler(currentRotationX, currentRotationY, currentRotationZ);
                yield return new WaitForFixedUpdate();
            }
            Log("Completed Rotation", Preferences.debugging.Value);
            Log("RevealDealerCard Completed", Preferences.debugging.Value);
            yield break;
        }

        private List<int> GetPlayerHandTotal()
        {
            Log("GetPlayerHandTotal Started", Preferences.debugging.Value);
            List<int> hands = new List<int>();
            int total = 0;
            bool hasAce = false;
            foreach (List<int> i in hand)
            {
                foreach (int j in i)
                {
                    int card = j;
                    while (card >= 13)
                    {
                        card -= 13;
                    }
                    card = Math.Clamp(card + 1, 1, 10);
                    if (card == 1)
                    {
                        hasAce = true;
                    }
                    total += card;
                }
                if (hasAce && total <= 11)
                {
                    total += 10;
                }
                hands.Add(total);
                total = 0;
            }
            Log("GetPlayerHandTotal Completed", Preferences.debugging.Value);
            return hands;
        }

        private int GetDealerHandTotal()
        {
            Log("GetDealerHandTotal Started", Preferences.debugging.Value);
            int total = 0;
            bool hasAce = false;
            foreach (int i in dealerHand)
            {
                int card = i;
                while (card >= 13)
                {
                    card -= 13;
                }
                card = Math.Clamp(card + 1, 1, 10);
                if (card == 1)
                {
                    hasAce = true;
                }
                total += card;
            }
            if (hasAce && total <= 11)
            {
                total += 10;
            }
            Log("GetDealerHandTotal Completed", Preferences.debugging.Value);
            return total;
        }

        private int GetCardsTotal(List<int> cards)
        {
            Log("GetCardsTotal Started", Preferences.debugging.Value);
            int total = 0;
            bool hasAce = false;
            foreach (int i in cards)
            {
                int card = i;
                while (card >= 13)
                {
                    card -= 13;
                }
                card = Math.Clamp(card + 1, 1, 10);
                if (card == 1)
                {
                    hasAce = true;
                }
                total += card;
                Log($"Card {tableInstance.CardString[i]}: {card}", Preferences.debugging.Value);
            }
            if (hasAce && total <= 11)
            {
                total += 10;
                Log($"Ace able to be 11", Preferences.debugging.Value);
            }
            Log("GetCardsTotal Completed: " + total, Preferences.debugging.Value);
            return total;
        }

        private IEnumerator PlayHands()
        {
            Log("PlayHands Running", Preferences.debugging.Value);
            hand = new List<List<int>>();
            dealerHand = new List<int>();
            has21Immediately = false;
            SetupCardSpots();
            int thisHand = hand.Count;
            hand.Add(new List<int>());
            hand[hand.Count - 1].Add(DrawCard());
            Log("Player Card 1: " + tableInstance.CardString[hand[hand.Count - 1][0]], Preferences.debugging.Value);
            object playDrawAnimationCoroutine = PlayDrawCardAnimation(hand[hand.Count - 1][0], cardSpots.transform.GetChild(0).GetChild(0), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            yield return playDrawAnimationCoroutine;

            dealerHand.Add(DrawCard());
            Log("Dealer Card 1: " + tableInstance.CardString[dealerHand[0]], Preferences.debugging.Value);
            object playDrawAnimationCoroutine2 = PlayDrawCardAnimation(dealerHand[0], cardSpots.transform.GetChild(1).GetChild(0), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            yield return playDrawAnimationCoroutine2;

            hand[hand.Count - 1].Add(DrawCard());
            Log("Player Card 2: " + tableInstance.CardString[hand[hand.Count - 1][1]], Preferences.debugging.Value);
            object playDrawAnimationCoroutine3 = PlayDrawCardAnimation(hand[hand.Count - 1][1], cardSpots.transform.GetChild(0).GetChild(1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            yield return playDrawAnimationCoroutine3;
            
            dealerHand.Add(DrawCard());
            Log("Dealer Card 2: -_-", Preferences.debugging.Value);
            object playDrawAnimationCoroutine4 = PlayDrawCardAnimation(52, cardSpots.transform.GetChild(1).GetChild(1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0), false);
            yield return playDrawAnimationCoroutine4;
            //starting hands have been dealt
            if (GetCardsTotal(hand[thisHand]) == 21)
            {
                has21Immediately = true;
            }
            else
            {
                //offer options
                object optionsMenuCoroutine = MelonCoroutines.Start(RunOptionsMenu(thisHand));
                yield return optionsMenuCoroutine;
            }
            Log("PlayHands Completed", Preferences.debugging.Value);
            yield break;
        }

        //Note: position, not localPosition. This sets the linear path to localPosition 0,0 in animation
        private object PlayDrawCardAnimation(int CardToDraw, Transform parent, Vector3 position, Quaternion localRotation, bool playRotate = true)
        {
            Log("PlayDrawCardAnimation Started", Preferences.debugging.Value);
            GameObject card = GameObject.Instantiate(tableInstance.storedDeckOfCards.transform.GetChild(CardToDraw).gameObject);
            card.transform.SetParent(parent);
            card.transform.position = position;
            card.transform.localRotation = localRotation;
            Log("PlayDrawCardAnimation Completed", Preferences.debugging.Value);
            return MelonCoroutines.Start(PlayDrawAnimation(card, 25, playRotate));
        }

        private int DrawCard()
        {
            if (deck.Count == 0) { RandomizeDeck(); }
            int card = deck[0];
            deck.RemoveAt(0);
            tableInstance.dealerDeck.transform.localScale = new Vector3(1, ((float)deck.Count) / 52f, 1);
            return card;
        }

        //moves it to localPosition 0. flips 180 unless specified (dealer card 2)
        private IEnumerator PlayDrawAnimation(GameObject card, int ticks = 25, bool playRotate = true)
        {
            Log("PlayDrawAnimation Started", Preferences.debugging.Value);
            Vector3 distancePerTick = (card.transform.localPosition) / ticks;
            float rotationPerTick = 180f / ((float)ticks);
            float currentRotationX = card.transform.localRotation.eulerAngles.x;
            float currentRotationY = card.transform.localRotation.eulerAngles.y;
            float currentRotationZ = card.transform.localRotation.eulerAngles.z;
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition -= distancePerTick;
                if (playRotate)
                {
                    currentRotationX -= rotationPerTick;
                    card.transform.localRotation = Quaternion.Euler(currentRotationX, currentRotationY, currentRotationZ);
                }
                yield return new WaitForFixedUpdate();
            }
            Log("PlayDrawAnimation Completed", Preferences.debugging.Value);
            yield break;
        }

        //spawns bet menu and waits for an option to be pressed
        private IEnumerator RunBetMenu()
        {
            Log("RunBetMenu Started", Preferences.debugging.Value);
            betAmount = 1;
            GameObject betsMenu = SpawnBetsMenu();
            Log("Spawned Bets Menu, Waiting for Accepted Bet", Preferences.debugging.Value);
            while (!betAccepted && !userQuits)
            {
                yield return new WaitForFixedUpdate();
            }
            if (betAccepted)
            {
                betHeldAmount = betAmount;
                Log("Bet Accepted, Bet: " + betAmount, Preferences.debugging.Value);
            }
            Log("RunBetMenu Completed", Preferences.debugging.Value);
            yield break;
        }

        private GameObject SpawnBetsMenu()
        {
            Log("SpawnBetsMenu Started", Preferences.debugging.Value);
            GameObject spawnedBetsMenu = GameObject.Instantiate(storedBetsMenu);
            spawnedBetsMenu.transform.SetParent(activeGamePartsTransform);
            spawnedBetsMenu.transform.localPosition = new Vector3(0, tableInstance.TABLEHEIGHT - 0.052f, 0);
            spawnedBetsMenu.transform.localRotation = Quaternion.identity;
            spawnedBetsMenu.transform.localScale = Vector3.one;

            betTextComponent = spawnedBetsMenu.transform.GetChild(1).GetComponent<TextMeshPro>();
            UpdateBetAmountText();
            //Bet Down
            spawnedBetsMenu.transform.GetChild(2).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Down Pressed", Preferences.debugging.Value);
                if (1 < betAmount)
                {
                    betAmount--;
                    Log("New Bet: " + betAmount, Preferences.debugging.Value);
                    UpdateBetAmountText();
                }
            }));
            //Bet Up
            spawnedBetsMenu.transform.GetChild(3).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Up Pressed", Preferences.debugging.Value);
                if (Main.GetPlayerCoinCount() >= betAmount + 1)
                {
                    betAmount++;
                    Log("New Bet: " + betAmount, Preferences.debugging.Value);
                    UpdateBetAmountText();
                }
            }));
            //Bet Down 10
            spawnedBetsMenu.transform.GetChild(4).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Down Pressed", Preferences.debugging.Value);
                if (10 < betAmount)
                {
                    betAmount -= 10;
                    Log("New Bet: " + betAmount, Preferences.debugging.Value);
                    UpdateBetAmountText();
                }
            }));
            //Bet Up 10
            spawnedBetsMenu.transform.GetChild(5).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Up Pressed", Preferences.debugging.Value);
                if (Main.GetPlayerCoinCount() >= betAmount + 10)
                {
                    betAmount += 10;
                    Log("New Bet: " + betAmount, Preferences.debugging.Value);
                    UpdateBetAmountText();
                }
            }));
            //Bet Accepted
            spawnedBetsMenu.transform.GetChild(6).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Accepted: " + betAmount, Preferences.debugging.Value);
                betAccepted = true;
                continueShuffling = false;
            }));
            //Quit
            spawnedBetsMenu.transform.GetChild(7).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("User Quit During Betting", Preferences.debugging.Value);
                userQuits = true;
                continueShuffling = false;
            }));
            //Bet Min
            spawnedBetsMenu.transform.GetChild(8).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Min Pressed", Preferences.debugging.Value);
                betAmount = 1;
                Log("New Bet: " + betAmount, Preferences.debugging.Value);
                UpdateBetAmountText();
            }));
            //Bet Max
            spawnedBetsMenu.transform.GetChild(9).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Bet Max Pressed", Preferences.debugging.Value);
                betAmount = Main.GetPlayerCoinCount();
                Log("New Bet: " + betAmount, Preferences.debugging.Value);
                UpdateBetAmountText();
            }));
            spawnedBetsMenu.transform.GetChild(9).GetChild(1).localPosition = new Vector3(0f, 0f, -0.25f); //fixes weirdness about Text not being correct

            betAccepted = false;
            userQuits = false;
            Log("SpawnBetsMenu Completed", Preferences.debugging.Value);
            return spawnedBetsMenu;
        }

        private void RandomizeDeck(bool continueShuffling = true)
        {
            Log("RandomizeDeck Running", Preferences.debugging.Value);
            object[] shufflings = tableInstance.ShuffleDealerDeck();
            if (continueShuffling)
            {
                ContinueShufflings(shufflings);
            }
            deck = new List<int>();
            List<int> tempDeck = new List<int>();
            int cardCount = 0;
            for (int i = 0; i < 52 * Preferences.deckCount.Value; i++)
            {
                deck.Add(cardCount);
                cardCount++;
                if (cardCount == 52) { cardCount = 0; }
            }
            Log("Deck Setup Done", Preferences.debugging.Value);
            tableInstance.dealerDeck.transform.localScale = new Vector3(1, ((float)deck.Count) / 52f, 1);
            tempDeck.Clear();
            while (deck.Count > 0)
            {
                int spot = tableInstance.random.Next(deck.Count);
                tempDeck.Add(deck[spot]);
                deck.RemoveAt(spot);
            }
            deck.AddRange(tempDeck);
            Log("RandomizeDeck Complete", Preferences.debugging.Value);
        }

        private bool continueShuffling = true;
        private void ContinueShufflings(object[] shufflings)
        {
            continueShuffling = true;
            Transform cardsParent = tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).GetChild(0);
            for (int i = 0; i < shufflings.Length; i++)
            {
                MelonCoroutines.Start(ShufflingCard(cardsParent.transform.GetChild(i).gameObject, shufflings[i]));
            }
        }

        private IEnumerator ShufflingCard(GameObject card, object spinCoroutine)
        {
            Transform cardsParent = tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).GetChild(0);
            yield return spinCoroutine;
            while (continueShuffling && tableInstance.blackJackInstance != null)
            {
                spinCoroutine = MelonCoroutines.Start(tableInstance.SpinCard(card));
                yield return spinCoroutine;
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        private void SetupBetMenu()
        {
            Log("SetupBetMenu Started", Preferences.debugging.Value);
            if (storedBetsMenu != null) { GameObject.Destroy(storedBetsMenu); }
            storedBetsMenu = new GameObject("BetsMenu");
            storedBetsMenu.transform.SetParent(storedGamePartsTransform);
            storedBetsMenu.transform.localPosition = Vector3.zero;
            storedBetsMenu.transform.localRotation = Quaternion.identity;
            storedBetsMenu.transform.localScale = Vector3.one;

            GameObject betTitleText = tableInstance.SpawnText(storedBetsMenu.transform,
                /*Title*/"How Much To Bet?",
                /*position*/ new Vector3(0f, 0f, 0.35f),
                /*rotation*/Quaternion.Euler(90f, 180f, 0f),
                /*scale*/ new Vector3(1.25f, 1.25f, 1.25f));
            TextMeshPro betTitleTextTMP = betTitleText.GetComponent<TextMeshPro>();
            betTitleTextTMP.alignment = TextAlignmentOptions.Center;
            betTitleTextTMP.enableWordWrapping = false;

            GameObject betText = tableInstance.SpawnText(storedBetsMenu.transform,
                /*Title*/"BetText",
                /*position*/ new Vector3(0f, 0f, 0.5f),
                /*rotation*/Quaternion.Euler(90, 180, 0),
                /*scale*/ new Vector3(1f, 1f, 1f));
            TextMeshPro betTextTMP = betText.GetComponent<TextMeshPro>();
            betTextTMP.alignment = TextAlignmentOptions.Center;
            betTextTMP.enableWordWrapping = false;
            betTextComponent = betText.GetComponent<TextMeshPro>();
            UpdateBetAmountText();

            GameObject betDownButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"-1",
                /*position*/ new Vector3(0.15f, 0f, 0.9f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betUpButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"+1",
                /*position*/ new Vector3(-0.15f, 0f, 0.9f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betDownTenButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"-10",
                /*position*/ new Vector3(0.325f, 0f, 0.85f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betUpTenButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"+10",
                /*position*/ new Vector3(-0.325f, 0f, 0.85f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betAcceptButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"Accept",
                /*position*/ new Vector3(0f, 0f, 0.75f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject UserQuitButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"Exit",
                /*position*/ new Vector3(0.75f, 0f, 0.25f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betMinButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"Min",
                /*position*/ new Vector3(0.5f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject betMaxButton = tableInstance.SpawnButton(storedBetsMenu.transform,
                /*Title*/"Max",
                /*position*/ new Vector3(-0.5f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            Log("SetupBetMenu Completed", Preferences.debugging.Value);
        }

        private bool pressedStay;
        private bool pressedHit;
        private bool pressedSplit;
        private GameObject SpawnOptionsMenu(int thisHand)
        {
            Log("SpawnOptionsMenu Started", Preferences.debugging.Value);
            pressedStay = false;
            pressedHit = false;
            pressedSplit = false;
            GameObject spawnedOptionsMenu = GameObject.Instantiate(storedOptionsMenu);
            spawnedOptionsMenu.transform.SetParent(activeGamePartsTransform);
            spawnedOptionsMenu.transform.localPosition = new Vector3(0, tableInstance.TABLEHEIGHT - 0.052f, 0);
            spawnedOptionsMenu.transform.localRotation = Quaternion.identity;
            spawnedOptionsMenu.transform.localScale = Vector3.one;
            //Stay
            spawnedOptionsMenu.transform.GetChild(0).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Stay Pressed", Preferences.debugging.Value);
                pressedStay = true;
            }));
            //Hit
            spawnedOptionsMenu.transform.GetChild(1).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Hit Pressed", Preferences.debugging.Value);
                pressedHit = true;
            }));
            spawnedOptionsMenu.transform.GetChild(1).GetChild(1).localPosition = new Vector3(0f, 0f, -0.25f); //fixes weirdness about Text not being correct
            //Split
            if (SplitIsPossible(hand[thisHand]))
            {
                spawnedOptionsMenu.transform.GetChild(2).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                    Log("Split Pressed", Preferences.debugging.Value);
                    pressedSplit = true;
                }));
                spawnedOptionsMenu.transform.GetChild(2).GetChild(1).localPosition = new Vector3(0f, 0f, -0.25f); //fixes weirdness about Text not being correct
                spawnedOptionsMenu.transform.GetChild(2).gameObject.SetActive(true);
            }
            //Hand Text
            spawnedOptionsMenu.transform.GetChild(3).GetComponent<TextMeshPro>().text = (GetPlayerHandTotal()[thisHand]).ToString();
            spawnedOptionsMenu.transform.GetChild(3).localPosition = new Vector3(-0.15f, 0f, 0.6f); //fixes weirdness about Text not being correct
            spawnedOptionsMenu.transform.GetChild(3).gameObject.SetActive(Preferences.showHandCount.Value);

            //Dealer Hand Text
            string total;
            int card = dealerHand[0];
            while (card >= 13) { card -= 13; }
            total = Math.Min(card + 1, 10).ToString();
            spawnedOptionsMenu.transform.GetChild(4).GetComponent<TextMeshPro>().text = total;
            spawnedOptionsMenu.transform.GetChild(4).gameObject.SetActive(Preferences.showHandCount.Value);
            spawnedOptionsMenu.transform.GetChild(4).localPosition = new Vector3(-0.15f, 0f, 0.55f); //fixes weirdness about Text not being correct
            spawnedOptionsMenu.transform.GetChild(4).gameObject.SetActive(Preferences.showHandCount.Value);
            Log("SpawnOptionsMenu Completed", Preferences.debugging.Value);
            return spawnedOptionsMenu;
        }

        //spawns optionsMenu menu and waits for an option to be pressed
        private IEnumerator RunOptionsMenu(int thisHand)
        {
            Log("RunOptionsMenu Started", Preferences.debugging.Value);
            GameObject optionsMenu = SpawnOptionsMenu(thisHand);
            Log("Spawned Options Menu, Waiting for Selection", Preferences.debugging.Value);
            while (!pressedStay && !pressedHit && !pressedSplit)
            {
                yield return new WaitForFixedUpdate();
            }
            GameObject.Destroy(optionsMenu);
            if (pressedHit)
            {
                Log("Pressed Hit", Preferences.debugging.Value);
                hand[thisHand].Add(DrawCard());
                //add drawing card visual
                Log($"Player Card {hand[thisHand].Count}: {tableInstance.CardString[hand[thisHand][hand[thisHand].Count - 1]]}", Preferences.debugging.Value);
                object playDrawAnimationCoroutine = PlayDrawCardAnimation(hand[thisHand][hand[thisHand].Count - 1], cardSpots.transform.GetChild(0).GetChild(hand[thisHand].Count - 1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
                yield return playDrawAnimationCoroutine;
                if (GetCardsTotal(hand[thisHand]) < 21)
                {
                    object optionsInOptionsMenuCoroutine = MelonCoroutines.Start(RunOptionsMenu(thisHand));
                    yield return optionsInOptionsMenuCoroutine;
                }
                else
                {
                    if (GetCardsTotal(hand[thisHand]) > 21) { Log("Player Bust, Total: " + GetCardsTotal(hand[thisHand]), Preferences.debugging.Value); }
                    else { Log("Player Got 21, Stopping Hand", Preferences.debugging.Value); }
                }
            }
            else if (pressedStay)
            {
                Log("Pressed Stay", Preferences.debugging.Value);
                //all hands need to conclude first
            }
            else if (pressedSplit)
            {
                Log("Pressed Split", Preferences.debugging.Value);
                Log("Player Split Bet Accepted: " + betAmount, true);
                Main.Payout(-betAmount, 1f, tableInstance);
                betHeldAmount += betAmount;
                //split 1 hand in 2, then play options for each hand
                List<int> secondHand = new List<int>();
                secondHand.Add(-1);
                secondHand.Add(hand[thisHand][1]);
                hand[thisHand][1] = -1;
                string cardsString = "";
                foreach (int i in hand[thisHand])
                {
                    if (i != -1)
                    {
                        cardsString += tableInstance.CardString[i];
                    }
                    else
                    {
                        cardsString += "-1";
                    }
                    cardsString += " ";
                }
                Log($"Hand {thisHand}: {cardsString}", Preferences.debugging.Value);
                cardsString = "";
                foreach (int i in secondHand)
                {
                    if (i != -1)
                    {
                        cardsString += tableInstance.CardString[i];
                    }
                    else
                    {
                        cardsString += "-1";
                    }
                    cardsString += " ";
                }
                Log($"Next Hand: {cardsString}", Preferences.debugging.Value);
                ClearVisualCardsFromAHand(true);
                //hand 1
                Log($"Playing First Hand", Preferences.debugging.Value);
                object drawSplitCoroutine = MelonCoroutines.Start(DrawSplit(thisHand));
                yield return drawSplitCoroutine;
                if (GetCardsTotal(hand[thisHand]) < 21)
                {
                    object runOptionsMenuCoroutine = MelonCoroutines.Start(RunOptionsMenu(thisHand));
                    yield return runOptionsMenuCoroutine;
                }
                else
                yield return new WaitForSeconds(1f);
                //setup for next hand
                Log($"Setting Up For Next Hand", Preferences.debugging.Value);
                ClearVisualCardsFromAHand(true);
                hand.Add(secondHand);
                int secondHandSpot = hand.Count - 1;
                //next hand
                Log($"Playing Next Hand", Preferences.debugging.Value);
                drawSplitCoroutine = MelonCoroutines.Start(DrawSplit(secondHandSpot, true));
                yield return drawSplitCoroutine;
                if (GetCardsTotal(hand[secondHandSpot]) <= 21)
                {
                    object runOptionsMenuCoroutine = MelonCoroutines.Start(RunOptionsMenu(secondHandSpot));
                    yield return runOptionsMenuCoroutine;
                }
                yield return new WaitForSeconds(1f);
            }
            Log("RunOptionsMenu Completed", Preferences.debugging.Value);
            yield break;
        }

        private void ClearVisualCardsFromAHand(bool isPlayer)
        {
            Log($"Clearing {(isPlayer ? "Player's" : "Dealer's")} Cards", Preferences.debugging.Value);
            int spot = isPlayer ? 0 : 1;
            for (int i = 0; i < cardSpots.transform.GetChild(spot).GetChildCount(); i++)
            {
                for (int x = cardSpots.transform.GetChild(spot).GetChild(i).childCount - 1; x >= 0; x--)
                {
                    GameObject.Destroy(cardSpots.transform.GetChild(spot).GetChild(i).GetChild(x).gameObject);
                }
            }
        }

        private IEnumerator DrawSplit(int thisHand, bool recreateOriginal = false)
        {
            bool drawFirstSpot = false;
            if (hand[thisHand][0] == -1)
            {
                hand[thisHand][0] = DrawCard();
                drawFirstSpot = true;
            }
            else
            {
                PlayDrawCardAnimation(hand[thisHand][0], cardSpots.transform.GetChild(0).GetChild(0), cardSpots.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(0, 0, 0), false);
            }
            if (hand[thisHand][1] == -1)
            {
                hand[thisHand][1] = DrawCard();
                drawFirstSpot = false;
            }
            else
            {
                PlayDrawCardAnimation(hand[thisHand][1], cardSpots.transform.GetChild(0).GetChild(1), cardSpots.transform.GetChild(0).GetChild(1).position, Quaternion.Euler(0, 0, 0), false);
            }
            object playDrawAnimationCoroutine = PlayDrawCardAnimation(hand[thisHand][drawFirstSpot ? 0 : 1], cardSpots.transform.GetChild(0).GetChild(drawFirstSpot ? 0 : 1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            yield return playDrawAnimationCoroutine;
            yield break;
        }

        private IEnumerator DealersTurn()
        {
            Log("DealersTurn Started", Preferences.debugging.Value);
            //flip card
            object revealDealerCardCoroutine = MelonCoroutines.Start(RevealDealerCard());
            yield return revealDealerCardCoroutine;
            //check total
            bool hasAce = false;
            bool aceIs11 = false;
            int dealerTotal = GetDealerHandTotal();
            bool hasSoft17 = (dealerTotal == 17) && hasAce && aceIs11;
            //hit on 16 or below or soft 17 (has ace as 11)
            while ((dealerTotal < 17) || hasSoft17)
            {
                object dealerDrawCorooutine = MelonCoroutines.Start(DealerDraw());
                yield return dealerDrawCorooutine;
                dealerTotal = GetDealerHandTotal();
                hasSoft17 = (dealerTotal == 17) && hasAce && aceIs11;
                yield return new WaitForSeconds(0.25f);
            }
            Log("DealersTurn Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator DealerDraw()
        {
            dealerHand.Add(DrawCard());
            Log($"Dealer Card {dealerHand.Count}: " + tableInstance.CardString[dealerHand[dealerHand.Count - 1]], Preferences.debugging.Value);
            object playDrawAnimationCoroutine = PlayDrawCardAnimation(dealerHand[dealerHand.Count - 1], cardSpots.transform.GetChild(1).GetChild(dealerHand.Count - 1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            yield return playDrawAnimationCoroutine;
            yield break;
        }

        private void SetupOptionsMenu()
        {
            Log("SetupOptionsMenu Started", Preferences.debugging.Value);
            if (storedOptionsMenu != null) { GameObject.Destroy(storedOptionsMenu); }
            storedOptionsMenu = new GameObject("OptionsMenu");
            storedOptionsMenu.transform.SetParent(storedGamePartsTransform);
            storedOptionsMenu.transform.localPosition = Vector3.zero;
            storedOptionsMenu.transform.localRotation = Quaternion.identity;
            storedOptionsMenu.transform.localScale = Vector3.one;

            GameObject stayButton = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Stay",
                /*position*/ new Vector3(0.25f, 0f, 0.9f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject hitButton = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Hit",
                /*position*/ new Vector3(-0.25f, 0f, 0.9f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject splitButton = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Split",
                /*position*/ new Vector3(0f, 0f, 0.95f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            splitButton.SetActive(false);

            GameObject handText = tableInstance.SpawnText(storedOptionsMenu.transform,
                /*Title*/"Hand",
                /*position*/ new Vector3(-0.15f, 0f, 0.6f),
                /*rotation*/Quaternion.Euler(90f, 180f, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            GameObject dealerHandText = tableInstance.SpawnText(storedOptionsMenu.transform,
                /*Title*/"Hand",
                /*position*/ new Vector3(-0.15f, 0f, 0.5f),
                /*rotation*/Quaternion.Euler(90f, 180f, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.55f));
            dealerHandText.SetActive(false);
            Log("SetupOptionsMenu Completed", Preferences.debugging.Value);
        }

        private bool SplitIsPossible(List<int> thisHand)
        {
            int card1 = thisHand[0];
            int card2 = thisHand[1];
            while (card1 >= 13) { card1 -= 13; }
            while (card2 >= 13) { card2 -= 13; }
            Log("Is Split Possible: " + ((card1 == card2) && (tableInstance.freePlay || (Main.GetPlayerCoinCount() >= (betAmount + betHeldAmount)))), Preferences.debugging.Value);
            return ((card1 == card2) && (Main.GetPlayerCoinCount() >= (betAmount + betHeldAmount)));
        }

        private void UpdateBetAmountText()
        {
            betTextComponent.text = betAmount.ToString() + " of " + Main.GetPlayerCoinCount();
        }
    }
}
