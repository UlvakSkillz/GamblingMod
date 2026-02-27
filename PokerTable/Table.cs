using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using UnityEngine;
using Random = System.Random;

namespace GamblingMod
{
    public enum Games : int
    {
        BlackJack,
        JacksOrBetter
    }

    [RegisterTypeInIl2Cpp]
    public class Table : MonoBehaviour
    {
        public string[] CardString = {
        "AS", "2S", "3S", "4S", "5S", "6S", "7S", "8S", "9S", "10S", "JS", "QS", "KS",
        "AH", "2H", "3H", "4H", "5H", "6H", "7H", "8H", "9H", "10H", "JH", "QH", "KH",
        "AD", "2D", "3D", "4D", "5D", "6D", "7D", "8D", "9D", "10D", "JD", "QD", "KD",
        "AC", "2C", "3C", "4C", "5C", "6C", "7C", "8C", "9C", "10C", "JC", "QC", "KC" };

        public float TABLEHEIGHT;
        public Random random = null;
        private int seed = 0;
        public bool freePlay = false;
        public GameObject dealerDeck, storedDeckOfCards;
        public GameObject freePlayButton;

        private Table instance = null;
        public BlackJack blackJackInstance = null;
        public JacksOrBetter jacksOrBetterInstance = null;

        public static void Log(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Main.Log($"Table - {msg}", sendMsg);
            }
        }

        public static void Warn(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Main.Warn($"Table - {msg}", sendMsg);
            }
        }

        public static void Error(string msg)
        {
            Main.Error($"Table - {msg}");
        }

        void Start()
        {
            Log("Start Started", (bool)Main.debugging.SavedValue);
            instance = this;
            TABLEHEIGHT = 0.9974f;
            dealerDeck = this.transform.GetChild(0).GetChild(16).gameObject;
            storedDeckOfCards = this.transform.GetChild(1).GetChild(0).gameObject;
            SetupRandom();
            SetupStart();
            Log("Start Completed", (bool)Main.debugging.SavedValue);
        }

        void OnDestroy()
        {
            Log("OnDestroy Started", (bool)Main.debugging.SavedValue);
            if (blackJackInstance != null) { MelonCoroutines.Stop(blackJackInstance); }
            if (jacksOrBetterInstance != null) { MelonCoroutines.Stop(jacksOrBetterInstance); }
            Log("OnDestroy Completed", (bool)Main.debugging.SavedValue);
        }

        public void SetupRandom()
        {
            Log("SetupRandom Started", (bool)Main.debugging.SavedValue);
            string seedString = "0123456789";
            if (!(bool)Main.useSeed.SavedValue)
            {
                Random randomSeed = new Random();
                int randomInt = randomSeed.Next(1, 10);
                string seedCrafted = seedString[randomInt].ToString();
                for (int i = 1; i <= 8; i++)
                {
                    seedCrafted += seedString[randomSeed.Next(0, 10)];
                }
                seed = int.Parse(seedCrafted);
            }
            else
            {
                seed = Math.Min((int)Main.tableSeed.SavedValue, 0);
            }
            Main.tableSeed.Value = seed;
            Main.tableSeed.SavedValue = seed;
            random = new Random(seed);
            Log("SetupRandom Complete", (bool)Main.debugging.SavedValue);
        }

        public void SetupStart()
        {
            Log("SetupStart Started", (bool)Main.debugging.SavedValue);
            if (!(bool)Main.useSeed.SavedValue)
            {
                freePlayButton = LoadMenuButton("FreePlay",
                    /*position*/ new Vector3(0f, TABLEHEIGHT - 0.05f, 0.6f),
                    /*rotation*/Quaternion.Euler(0, 0, 0),
                    /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                    () =>
                    {
                        freePlay = !freePlay;
                        this.gameObject.transform.GetChild(2).FindChild("FreePlay Button/FreePlayText").GetComponent<TextMeshPro>().color = freePlay ? Color.green : Color.red;
                    });
                if (freePlayButton != null)
                {
                    freePlayButton.transform.GetChild(2).GetComponent<TextMeshPro>().color = freePlay ? Color.green : Color.red;
                }
            }
            LoadMenuButton("BlackJack",
                /*position*/ new Vector3(0.55f, TABLEHEIGHT - 0.05f, 0.6f),
                /*rotation*/Quaternion.Euler(0, 0, 0),
                /*scale*/ new Vector3(1, 1, 1),
                () => { LoadGame(Games.BlackJack); });
            LoadMenuButton("Jacks Or Better",
                /*position*/ new Vector3(-0.55f, TABLEHEIGHT - 0.05f, 0.6f), 
                /*rotation*/Quaternion.Euler(0, 0, 0), 
                /*scale*/ new Vector3(1, 1, 1), 
                () => { LoadGame(Games.JacksOrBetter); });
            Log("SetupStart Complete", (bool)Main.debugging.SavedValue);
        }

        private void LoadGame(Games game)
        {
            Log("Loading Game: " + game, (bool)Main.debugging.SavedValue);
            ClearActiveObjects();
            MelonCoroutines.Start(LoadGameCoroutine(game));
        }

        private IEnumerator LoadGameCoroutine(Games game)
        {
            object gameRunningCoroutine = null;
            switch (game)
            {
                case Games.BlackJack:
                    Log("Game BlackJack Started!", (bool)Main.debugging.SavedValue);
                    blackJackInstance = new BlackJack(instance);
                    blackJackInstance.ShowSplash();
                    gameRunningCoroutine = MelonCoroutines.Start(blackJackInstance.Run()); //load game
                    break;
                case Games.JacksOrBetter:
                    Log("Game JacksOrBetter Started!", (bool)Main.debugging.SavedValue);
                    jacksOrBetterInstance = new JacksOrBetter(instance);
                    jacksOrBetterInstance.ShowSplash();
                    gameRunningCoroutine = MelonCoroutines.Start(jacksOrBetterInstance.Run()); //load game
                    break;
                default:
                    yield break;
            }
            yield return gameRunningCoroutine;
            ClearActiveObjects();
            Log("Game Completed!", (bool)Main.debugging.SavedValue);
            SetupStart();
            yield break;
        }

        public void ClearActiveObjects()
        {
            Log("Clearing Active Objects", (bool)Main.debugging.SavedValue);
            Transform activeObjectsSpot = instance.transform.GetChild(2);
            for (int i = activeObjectsSpot.GetChildCount() - 1; i >= 0; i--)
            {
                Log("Clearing Active Object: " + activeObjectsSpot.GetChild(i).gameObject.name, (bool)Main.debugging.SavedValue);
                GameObject.Destroy(activeObjectsSpot.GetChild(i).gameObject);
            }
            Log("Done Clearing Active Objects", (bool)Main.debugging.SavedValue);
        }

        public GameObject LoadMenuButton(string title, Vector3 position, Quaternion rotation, Vector3 localScale, Action listener = null)
        {
            Log("Loading Menu Button: " + title, (bool)Main.debugging.SavedValue);
            GameObject button = (listener != null ? Create.NewButton(listener) : Create.NewButton());
            button.name = title + " Button";
            button.transform.SetParent(this.transform.GetChild(2));
            button.transform.localPosition = new Vector3(position.x, position.y, position.z);
            button.transform.localRotation = rotation;
            button.transform.localScale = localScale;
            LoadText(button, title);
            Log("Done Loading Menu Button: " + title, (bool)Main.debugging.SavedValue);
            return button;
        }

        public object[] ShuffleDealerDeck()
        {
            Transform cardsParent = dealerDeck.transform.GetChild(0).GetChild(0).GetChild(0);
            object[] shufflings = new object[cardsParent.GetChildCount()];
            for (int i = 0; i < cardsParent.GetChildCount(); i++)
            {
                shufflings[i] = MelonCoroutines.Start(SpinCard(cardsParent.GetChild(i).gameObject));
            }
            return shufflings;
        }

        public IEnumerator SpinCard(GameObject card)
        {
            yield return new WaitForSeconds(((float)random.Next(10, 50)) / 100); //adds variation to shuffle start time
            int fixedUpdatesToCompleteSpin = random.Next(25, 50); //adds variation to spin speed
            int updatesLeft = fixedUpdatesToCompleteSpin;
            float amountToSpin = 360f / ((float)fixedUpdatesToCompleteSpin);
            while (updatesLeft >= 0)
            {
                try
                {
                    card.transform.localRotation = Quaternion.Euler(card.transform.localRotation.x, card.transform.localRotation.y, amountToSpin * (fixedUpdatesToCompleteSpin - updatesLeft));
                }
                catch { yield break; }
                updatesLeft--;
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        public GameObject SpawnButton(Transform parent, string title, Vector3 position, Quaternion rotation, Vector3 localScale, Action listener = null)
        {
            Log("Loading Button: " + title, (bool)Main.debugging.SavedValue);
            GameObject button = (listener != null ? Create.NewButton(listener) : Create.NewButton());
            button.name = title + " Button";
            button.transform.SetParent(parent);
            button.transform.localPosition = position;
            button.transform.localRotation = rotation;
            button.transform.localScale = localScale;
            LoadText(button, title);
            Log("Done Loading Button: " + title, (bool)Main.debugging.SavedValue);
            return button;
        }

        public GameObject SpawnText(Transform parent, string title, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Log("Loading Text: " + title, (bool)Main.debugging.SavedValue);
            GameObject text = Create.NewText();
            text.name = title + " Text";
            text.transform.SetParent(parent);
            text.transform.localPosition = position;
            text.transform.localRotation = rotation;
            text.transform.localScale = localScale;
            text.GetComponent<TextMeshPro>().text = title;
            Log("Done Loading Text: " + title, (bool)Main.debugging.SavedValue);
            return text;
        }

        public void LoadText(GameObject button, string title)
        {
            Log("Loading Menu Button Text: " + title, (bool)Main.debugging.SavedValue);
            GameObject text = Create.NewText();
            text.name = title + "Text";
            text.transform.SetParent(button.transform);
            text.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            text.transform.localRotation = Quaternion.Euler(90, 180, 0);
            text.transform.localScale = Vector3.one;
            TextMeshPro textTMP = text.GetComponent<TextMeshPro>();
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.enableWordWrapping = false;
            textTMP.text = title;
            Log("Done Loading Menu Button Text: " + title, (bool)Main.debugging.SavedValue);
        }
    }
}
