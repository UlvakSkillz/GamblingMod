using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppTMPro;
using MelonLoader;
using System.Collections;
using UnityEngine;

namespace GamblingMod
{
    public enum PayoutResult : int
    {
        Nothing,
        JacksOrBetter,
        TwoPair,
        ThreeOfAKind,
        Straight,
        Flush,
        FullHouse,
        FourOfAKind,
        StraightFlush,
        RoyalFlush
    }

    public enum CardSuits : int
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs
    }

    public class JacksOrBetter : MonoBehaviour
    {

        private string[] payoutsString = { "Nothing",
            "Jacks or Better",
            "Two Pair",
            "Three of a Kind",
            "Straight",
            "Flush",
            "Full House",
            "Four of a Kind",
            "Straight Flush",
            "Royal Flush" };
        private float[] payoutsAmounts = { 0f, 1f, 2f, 3f, 4f, 6f, 9f, 25f, 50f, 976f };
        private int betAmount = 1;
        private bool gameLoopRunning = false;
        private TextMeshPro betTextComponent = null;
        private GameObject storedBetsMenu = null, storedOptionsMenu = null;
        private bool betAccepted = false, userQuits = false;
        private List<int> deck, hand;
        public object gameLoopCoroutine = null;
        private Transform storedGamePartsTransform = null, activeGamePartsTransform = null;
        private Table tableInstance;

        public JacksOrBetter(Table tableInstance)
        {
            this.tableInstance = tableInstance;
        }

        public static void Log(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Table.Log($"JacksOrBetter - {msg}", sendMsg);
            }
        }

        public static void Warn(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Table.Warn($"JacksOrBetter - {msg}", sendMsg);
            }
        }

        public static void Error(string msg)
        {
            Table.Error($"JacksOrBetter - {msg}");
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
                /*Title*/"Jacks or Better",
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

        private GameObject cardSpots = null;
        private void SetupCardSpots()
        {
            Log("SetupStart Started", Preferences.debugging.Value);
            cardSpots = GameObject.Instantiate(storedGamePartsTransform.GetChild(2).gameObject);
            cardSpots.name = "CardSpots";
            cardSpots.transform.SetParent(activeGamePartsTransform);
            cardSpots.transform.localPosition = Vector3.zero;
            cardSpots.transform.localRotation = Quaternion.identity;
            cardSpots.transform.localScale = Vector3.one;
            Log("SetupStart Completed", Preferences.debugging.Value);
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
                    Log("Not Enough Coins, Exiting Jacks Or Better", Preferences.debugging.Value);
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
                    coinsGained = 0;
                    yield return MelonCoroutines.Start(PlayHand());
                    yield return new WaitForSeconds(0.5f);
                    //check hand for win
                    yield return MelonCoroutines.Start(CheckPayout());
                    //clear active cards
                    ClearCardSpots();
                    bool continuePressed = false;
                    //load end game text
                    GameObject endGameText = tableInstance.SpawnText(activeGamePartsTransform.transform,
                        /*Title*/$"End Game Text",
                        /*position*/ new Vector3(0f, tableInstance.TABLEHEIGHT - 0.025f, 0.5f),
                        /*rotation*/Quaternion.Euler(90, 180, 0),
                        /*scale*/ new Vector3(1f, 1f, 1f));
                    endGameText.name = "End Game Text";
                    TextMeshPro endGameTextTMP = endGameText.GetComponent<TextMeshPro>();
                    endGameTextTMP.alignment = TextAlignmentOptions.Center;
                    endGameTextTMP.enableWordWrapping = false;
                    string coinLine = coinsGained == 0 ? $"Coins Lost: {-betAmount}" : $"Coins Gained: {coinsGained - betAmount}";
                    endGameTextTMP.text = $"Hand Contains: {payoutsString[(int)thisHandsPayout]}{Environment.NewLine}{coinLine}";
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

        private PayoutResult thisHandsPayout = PayoutResult.Nothing;
        private IEnumerator CheckPayout()
        {
            Log("CheckPayout Started", Preferences.debugging.Value);
            thisHandsPayout = GetWinType();
            coinsGained = Main.Payout(betAmount, payoutsAmounts[(int)thisHandsPayout], tableInstance);
            Log($"Hand Contains: {payoutsString[(int)thisHandsPayout]}, Paying Out: {coinsGained}", true);
            object payoutCoroutine = null;
            if (thisHandsPayout == PayoutResult.Nothing)
            {
                payoutCoroutine = MelonCoroutines.Start(PlayPlayerLose());
            }
            else
            {
                payoutCoroutine = MelonCoroutines.Start(PlayPlayerWin());
            }
            yield return payoutCoroutine;
            Log("CheckPayout Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayPlayerWin()
        {
            Log("PlayPlayerWin Started", Preferences.debugging.Value);
            List<object> coroutines = new List<object>();
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < 5; i++)
            {
                coroutines.Add(MelonCoroutines.Start(PlayCardWinCoroutine(cardSpots.transform.GetChild(i).GetChild(0).gameObject, true)));
                yield return new WaitForSeconds(0.1f);
            }
            foreach (object coroutine in coroutines) { yield return coroutine; }
            yield return new WaitForSeconds(0.25f);
            Log("PlayPlayerWin Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator PlayPlayerLose()
        {
            Log("PlayPlayerLose Started", Preferences.debugging.Value);
            List<object> coroutines = new List<object>();
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < 5; i++)
            {
                coroutines.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(i).GetChild(0).gameObject, true)));
            }
            foreach(object coroutine in coroutines) { yield return coroutine; }
            yield return new WaitForSeconds(0.25f);
            Log("PlayPlayerLose Completed", Preferences.debugging.Value);
            yield break;
        }

        private PayoutResult GetWinType()
        {
            if (HasRoyalFlush())
            {
                return PayoutResult.RoyalFlush;
            }
            else if (HasStraightFlush())
            {
                return PayoutResult.StraightFlush;
            }
            else if (HasFourOfAKind())
            {
                return PayoutResult.FourOfAKind;
            }
            else if (HasFullHouse())
            {
                return PayoutResult.FullHouse;
            }
            else if (HasFlush())
            {
                return PayoutResult.Flush;
            }
            else if (HasStraight())
            {
                return PayoutResult.Straight;
            }
            else if (HasThreeOfAKind())
            {
                return PayoutResult.ThreeOfAKind;
            }
            else if (HasTwoPair())
            {
                return PayoutResult.TwoPair;
            }
            else if (HasJacksOrBetter())
            {
                return PayoutResult.JacksOrBetter;
            }
            else { return PayoutResult.Nothing; }
        }

        private bool HasJacksOrBetter()
        {
            List<int> tempHand = new List<int>();
            foreach (int i in hand) { tempHand.Add(getCardValue(i)); }
            List<int> cardCountList = new List<int>();
            for (int i = 0; i < 13; i++) { cardCountList.Add(0); }
            foreach (int card in tempHand) { cardCountList[card - 1]++; }
            return ((cardCountList[0] >= 2) || (cardCountList[10] >= 2) || (cardCountList[11] >= 2) || (cardCountList[12] >= 2));
        }

        private bool HasTwoPair()
        {
            List<int> tempHand = new List<int>();
            foreach (int i in hand) { tempHand.Add(getCardValue(i)); }
            List<int> cardCountList = new List<int>();
            for (int i = 0; i < 13; i++) { cardCountList.Add(0); }
            foreach (int card in tempHand) { cardCountList[card - 1]++; }
            bool hasTwoPair = false;
            if (cardCountList.Contains(2))
            {
                cardCountList.Remove(2);
                if (cardCountList.Contains(2)) { hasTwoPair = true; }
            }
            return hasTwoPair;
        }

        private bool HasThreeOfAKind() { return HasXOfAKind(3); }

        private bool HasFourOfAKind() { return HasXOfAKind(4); }

        private bool HasFullHouse()
        {
            List<int> tempHand = new List<int>();
            foreach (int i in hand) { tempHand.Add(getCardValue(i)); }
            List<int> cardCountList = new List<int>();
            for (int i = 0; i < 13; i++) { cardCountList.Add(0); }
            foreach (int card in tempHand) { cardCountList[card - 1]++; }
            cardCountList.Sort();
            return (cardCountList[12] == 3) && (cardCountList[11] == 2);
        }

        private bool HasStraightFlush()
        {
            return (HasFlush()
                && HasStraight());
        }

        private bool HasRoyalFlush()
        {
            return (HasFlush()
                && DoesHandContainCardValue(1)
                && DoesHandContainCardValue(10)
                && DoesHandContainCardValue(11)
                && DoesHandContainCardValue(12)
                && DoesHandContainCardValue(13));
        }

        private bool HasStraight()
        {
            List<int> tempHand = new List<int>();
            foreach (int i in hand) { tempHand.Add(getCardValue(i)); }
            tempHand.Sort();
            bool hasAce = false;
            bool usedAce = false;
            if (tempHand[0] == 1)
            {
                hasAce = true;
                tempHand.Add(tempHand[0]);
            }
            bool inLine = true;
            for (int i = 0; i < tempHand.Count - 1; i++)
            {
                if (tempHand[i] + 1 != tempHand[i + 1])
                {
                    if (hasAce && !usedAce) { usedAce = true; }
                    else { inLine = false; break; }
                }
            }
            return inLine;
        }

        private bool HasFlush()
        {
            CardSuits[] suits = new CardSuits[5];
            suits[0] = getCardSuit(hand[0]);
            suits[1] = getCardSuit(hand[1]);
            suits[2] = getCardSuit(hand[2]);
            suits[3] = getCardSuit(hand[3]);
            suits[4] = getCardSuit(hand[4]);
            return ((suits[0] == suits[1]) && (suits[0] == suits[2]) && (suits[0] == suits[3]) && (suits[0] == suits[4]));
        }

        private bool DoesHandContainCardValue(int cardNumberToFind)
        {
            return ((getCardValue(hand[0]) == cardNumberToFind)
                || (getCardValue(hand[1]) == cardNumberToFind)
                || (getCardValue(hand[2]) == cardNumberToFind)
                || (getCardValue(hand[3]) == cardNumberToFind)
                || (getCardValue(hand[4]) == cardNumberToFind));
        }

        private bool HasXOfAKind(int x)
        {
            List<int> tempHand = new List<int>();
            foreach (int i in hand) { tempHand.Add(getCardValue(i)); }
            List<int> cardCountList = new List<int>();
            for (int i = 0; i < 13; i++) { cardCountList.Add(0); }
            foreach(int card in tempHand) { cardCountList[card - 1]++; }
            cardCountList.Sort();
            return (x <= cardCountList[12]);
        }

        private int getCardValue(int rawCard)
        {
            int card = rawCard;
            while (card >= 13) { card -= 13; }
            return card + 1;
        }

        private CardSuits getCardSuit(int rawCard)
        {
            int card = rawCard;
            CardSuits suit = 0;
            while (card >= 13)
            {
                card -= 13;
                suit++;
            }
            return suit;
        }

        private IEnumerator PlayCardShrinkCoroutine(GameObject card, bool destroyAfter = false)
        {
            Vector3 startingScale = card.transform.localScale;
            Vector3 scalePerTick = startingScale / 50f;
            for (int i = 0; i < 50; i++)
            {
                card.transform.localScale -= scalePerTick;
                yield return new WaitForFixedUpdate();
            }
            if (destroyAfter) { GameObject.Destroy(card); }
            yield break;
        }

        private IEnumerator PlayCardWinCoroutine(GameObject card, bool destroyAfter = false)
        {
            Vector3 startingPosition = card.transform.localPosition;
            Vector3 positionPerTick = Vector3.up * 0.004f;
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition += positionPerTick;
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition -= positionPerTick;
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition += positionPerTick;
                yield return new WaitForFixedUpdate();
            }
            object winShrinkCoroutine = MelonCoroutines.Start(PlayCardShrinkCoroutine(card));
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition -= positionPerTick;
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < 25; i++)
            {
                card.transform.localPosition += positionPerTick;
                yield return new WaitForFixedUpdate();
            }
            yield return winShrinkCoroutine;
            if (destroyAfter) { GameObject.Destroy(card); }
            yield break;
        }

        private IEnumerator PlayHand()
        {
            Log("PlayHand Running", Preferences.debugging.Value);
            hand = new List<int>();
            SetupCardSpots();
            //deal hand
            yield return MelonCoroutines.Start(DrawStartingHand());
            //offer options
            yield return MelonCoroutines.Start(RunOptionsMenu());
            //replace removed cards
            yield return MelonCoroutines.Start(ReplaceMissingCardsInHand());
            Log("PlayHand Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator DrawStartingHand()
        {
            Log("DrawStartingHand Started", Preferences.debugging.Value);
            hand.Add(DrawCard());
            Log("Player Card 1: " + tableInstance.CardString[hand[0]], Preferences.debugging.Value);
            yield return PlayDrawCardAnimation(hand[0], cardSpots.transform.GetChild(0), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));

            hand.Add(DrawCard());
            Log("Player Card 2: " + tableInstance.CardString[hand[1]], Preferences.debugging.Value);
            yield return PlayDrawCardAnimation(hand[1], cardSpots.transform.GetChild(1), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));

            hand.Add(DrawCard());
            Log("Player Card 3: " + tableInstance.CardString[hand[2]], Preferences.debugging.Value);
            yield return PlayDrawCardAnimation(hand[2], cardSpots.transform.GetChild(2), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));

            hand.Add(DrawCard());
            Log("Player Card 4: " + tableInstance.CardString[hand[3]], Preferences.debugging.Value);
            yield return PlayDrawCardAnimation(hand[3], cardSpots.transform.GetChild(3), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));

            hand.Add(DrawCard());
            Log("Player Card 5: " + tableInstance.CardString[hand[4]], Preferences.debugging.Value);
            yield return PlayDrawCardAnimation(hand[4], cardSpots.transform.GetChild(4), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
            Log("DrawStartingHand Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator ReplaceMissingCardsInHand()
        {
            Log("ReplaceMissingCardsInHand Started", Preferences.debugging.Value);
            for (int i = 0; i < 5; i++)
            {
                if (hand[i] == -1)
                {
                    hand[i] = DrawCard();
                    yield return PlayDrawCardAnimation(hand[i], cardSpots.transform.GetChild(i), tableInstance.dealerDeck.transform.GetChild(0).GetChild(0).position, Quaternion.Euler(-180, 0, 0));
                    yield return new WaitForSeconds(0.25f);
                }
            }
            Log("ReplaceMissingCardsInHand Completed", Preferences.debugging.Value);
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
            while (continueShuffling && tableInstance.jacksOrBetterInstance != null)
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

        private bool pressedContinue, removeCard1 = true, removeCard2 = true, removeCard3 = true, removeCard4 = true, removeCard5 = true;
        private GameObject SpawnOptionsMenu()
        {
            Log("SpawnOptionsMenu Started", Preferences.debugging.Value);
            pressedContinue = false;
            removeCard1 = true;
            removeCard2 = true;
            removeCard3 = true;
            removeCard4 = true;
            removeCard5 = true;
            GameObject spawnedOptionsMenu = GameObject.Instantiate(storedOptionsMenu);
            spawnedOptionsMenu.transform.SetParent(activeGamePartsTransform);
            spawnedOptionsMenu.transform.localPosition = new Vector3(0, tableInstance.TABLEHEIGHT - 0.052f, 0);
            spawnedOptionsMenu.transform.localRotation = Quaternion.identity;
            spawnedOptionsMenu.transform.localScale = Vector3.one;
            //card 1
            spawnedOptionsMenu.transform.GetChild(0).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Card 1 Pressed", Preferences.debugging.Value);
                removeCard1 = !removeCard1;
                spawnedOptionsMenu.transform.GetChild(0).GetChild(1).GetComponent<TextMeshPro>().text = removeCard1 ? "Remove" : "Keep";
            }));
            spawnedOptionsMenu.transform.GetChild(0).GetChild(1).GetComponent<TextMeshPro>().text = "Remove";
            //card 2
            spawnedOptionsMenu.transform.GetChild(1).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Card 2 Pressed", Preferences.debugging.Value);
                removeCard2 = !removeCard2;
                spawnedOptionsMenu.transform.GetChild(1).GetChild(1).GetComponent<TextMeshPro>().text = removeCard2 ? "Remove" : "Keep";
            }));
            spawnedOptionsMenu.transform.GetChild(1).GetChild(1).GetComponent<TextMeshPro>().text = "Remove";
            //card 3
            spawnedOptionsMenu.transform.GetChild(2).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Card 3 Pressed", Preferences.debugging.Value);
                removeCard3 = !removeCard3;
                spawnedOptionsMenu.transform.GetChild(2).GetChild(1).GetComponent<TextMeshPro>().text = removeCard3 ? "Remove" : "Keep";
            }));
            spawnedOptionsMenu.transform.GetChild(2).GetChild(1).GetComponent<TextMeshPro>().text = "Remove";
            //card 4
            spawnedOptionsMenu.transform.GetChild(3).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Card 4 Pressed", Preferences.debugging.Value);
                removeCard4 = !removeCard4;
                spawnedOptionsMenu.transform.GetChild(3).GetChild(1).GetComponent<TextMeshPro>().text = removeCard4 ? "Remove" : "Keep";
            }));
            spawnedOptionsMenu.transform.GetChild(3).GetChild(1).GetComponent<TextMeshPro>().text = "Remove";
            //card 5
            spawnedOptionsMenu.transform.GetChild(4).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Card 5 Pressed", Preferences.debugging.Value);
                removeCard5 = !removeCard5;
                spawnedOptionsMenu.transform.GetChild(4).GetChild(1).GetComponent<TextMeshPro>().text = removeCard5 ? "Remove" : "Keep";
            }));
            spawnedOptionsMenu.transform.GetChild(4).GetChild(1).GetComponent<TextMeshPro>().text = "Remove";
            //continue
            spawnedOptionsMenu.transform.GetChild(5).GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener((Action)(() => {
                Log("Continue Pressed", Preferences.debugging.Value);
                pressedContinue = true;
            }));
            spawnedOptionsMenu.transform.GetChild(5).GetChild(1).localPosition = new Vector3(0f, 0f, -0.2f); //fixes weirdness about Text not being correct
            Log("SpawnOptionsMenu Completed", Preferences.debugging.Value);
            return spawnedOptionsMenu;
        }

        //spawns optionsMenu menu and waits for an option to be pressed
        private IEnumerator RunOptionsMenu()
        {
            Log("RunOptionsMenu Started", Preferences.debugging.Value);
            GameObject optionsMenu = SpawnOptionsMenu();
            Log("Spawned Options Menu, Waiting for Selection", Preferences.debugging.Value);
            while (!pressedContinue)
            {
                yield return new WaitForFixedUpdate();
            }
            GameObject.Destroy(optionsMenu);
            //options done, removing cards selected for removal
            Log($"Keep / Remove List: {removeCard1} {removeCard2} {removeCard3} {removeCard4} {removeCard5}", Preferences.debugging.Value);
            List<object> cardsRemoving = new List<object>();
            if (removeCard1)
            {
                Log("Removing Card 1", Preferences.debugging.Value);
                cardsRemoving.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(0).GetChild(0).gameObject, true)));
                hand[0] = -1;
            }
            if (removeCard2)
            {
                Log("Removing Card 2", Preferences.debugging.Value);
                cardsRemoving.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(1).GetChild(0).gameObject, true)));
                hand[1] = -1;
            }
            if (removeCard3)
            {
                Log("Removing Card 3", Preferences.debugging.Value);
                cardsRemoving.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(2).GetChild(0).gameObject, true)));
                hand[2] = -1;
            }
            if (removeCard4)
            {
                Log("Removing Card 4", Preferences.debugging.Value);
                cardsRemoving.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(3).GetChild(0).gameObject, true)));
                hand[3] = -1;
            }
            if (removeCard5)
            {
                Log("Removing Card 5", Preferences.debugging.Value);
                cardsRemoving.Add(MelonCoroutines.Start(PlayCardShrinkCoroutine(cardSpots.transform.GetChild(4).GetChild(0).gameObject, true)));
                hand[4] = -1;
            }
            //wait for removal of cards to be complete
            foreach (object coroutine in cardsRemoving) { yield return coroutine; }
            Log("RunOptionsMenu Completed", Preferences.debugging.Value);
            yield break;
        }

        private void ClearCardSpots()
        {
            Log("Clearing Card Spots", Preferences.debugging.Value);
            for (int i = 0; i < cardSpots.transform.GetChildCount(); i++)
            {
                for (int x = cardSpots.transform.GetChild(i).childCount - 1; x >= 0; x--)
                {
                    GameObject.Destroy(cardSpots.transform.GetChild(i).GetChild(x).gameObject);
                }
            }
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
            GameObject button;
            TextMeshPro textMeshPro;
            button = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Card1",
                /*position*/ new Vector3(0.1228f * 2f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            button.transform.GetChild(1).localPosition = new Vector3(0, 0, -0.2f);
            textMeshPro = button.transform.GetChild(1).GetComponent<TextMeshPro>();
            textMeshPro.fontSize = 0.75f;
            textMeshPro.text = "Remove";

            button = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Card2",
                /*position*/ new Vector3(0.1228f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            button.transform.GetChild(1).localPosition = new Vector3(0, 0, -0.2f);
            textMeshPro = button.transform.GetChild(1).GetComponent<TextMeshPro>();
            textMeshPro.fontSize = 0.75f;
            textMeshPro.text = "Remove";

            button = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Card3",
                /*position*/ new Vector3(0f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            button.transform.GetChild(1).localPosition = new Vector3(0, 0, -0.2f);
            textMeshPro = button.transform.GetChild(1).GetComponent<TextMeshPro>();
            textMeshPro.fontSize = 0.75f;
            textMeshPro.text = "Remove";

            button = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Card4",
                /*position*/ new Vector3(-0.1228f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            button.transform.GetChild(1).localPosition = new Vector3(0, 0, -0.2f);
            textMeshPro = button.transform.GetChild(1).GetComponent<TextMeshPro>();
            textMeshPro.fontSize = 0.75f;
            textMeshPro.text = "Remove";

            button = tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Card5",
                /*position*/ new Vector3(0.1228f * -2f, 0f, 0.8f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));
            button.transform.GetChild(1).localPosition = new Vector3(0, 0, -0.2f);
            textMeshPro = button.transform.GetChild(1).GetComponent<TextMeshPro>();
            textMeshPro.fontSize = 0.75f;
            textMeshPro.text = "Remove";

            tableInstance.SpawnButton(storedOptionsMenu.transform,
                /*Title*/"Continue",
                /*position*/ new Vector3(-0.43f, 0f, 0.86f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f));

            Log("SetupOptionsMenu Completed", Preferences.debugging.Value);
        }

        private void UpdateBetAmountText()
        {
            betTextComponent.text = betAmount.ToString() + " of " + Main.GetPlayerCoinCount();
        }
    }
}
