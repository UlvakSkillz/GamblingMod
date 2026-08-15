using Il2CppPhoton.Pun;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Pools;
using Il2CppRUMBLE.UI;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using UnityEngine;
using Random = System.Random;
using AudioManager = Il2CppRUMBLE.Managers.AudioManager;

namespace GamblingMod
{

    [RegisterTypeInIl2Cpp]
    public class SlotMachine : MonoBehaviourPun
    {
        public Random random = null;
        private int seed = 0;
        public bool freePlay = false;
        public GameObject freePlayButton, activeObjects, rotatingWheels, slotMachineTitleText, coinsIn, coinsOut, topLight, soundsParent;
        InteractionLever lever;
        private GameObject[] betLines = new GameObject[5];
        private int[] wheelsNumber = {0, 0, 0};
        public int[][] slotsOrder = new int[3][];
        private SlotMachine instance = null;
        private List<SlotObjectAlternator> gemRotateActive = new List<SlotObjectAlternator>();
        private bool spinning = false;
        private object[] spinCoroutines = new object[3];
        private object spinBabySpin = null;
        private bool continueWheelSpinSound = false;
        private bool playingWheelSpinSound = false;
        private int amountPutIn = 0, amountGotOut = 0;
        public int betAmount = 1;
        private bool stopWinAnimation = false;
        public int[] setsPayouts = { 1000, 70, 100, 200, 50, 90, 60, 80, 150, 45 };
        private string[] SlotsSpotTypesString =
        { "Rumble",
        "AdamantStone",
        "ChargeStone",
        "FlowStone",
        "GuardStone",
        "StubbornStone",
        "SurgeStone",
        "VigorStone",
        "VolatileStone",
        "Howard" };
        private GameObject[] slotObjectOriginals;

        public static void Log(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Main.Log($"SlotMachine - {msg}", sendMsg);
            }
        }

        public static void Warn(string msg, bool sendMsg = true)
        {
            if (sendMsg)
            {
                Main.Warn($"SlotMachine - {msg}", sendMsg);
            }
        }

        public static void Error(string msg)
        {
            Main.Error($"SlotMachine - {msg}");
        }

        void Start()
        {
            Log("Start Started", Preferences.debugging.Value);
            instance = this;
            activeObjects = instance.transform.GetChild(1).gameObject;
            rotatingWheels = instance.transform.GetChild(2).gameObject;
            rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, 0.017f); //moves the Dials
            betLines[0] = instance.transform.GetChild(0).GetChild(4).GetChild(0).gameObject;
            betLines[1] = instance.transform.GetChild(0).GetChild(4).GetChild(1).gameObject;
            betLines[2] = instance.transform.GetChild(0).GetChild(4).GetChild(2).gameObject;
            betLines[3] = instance.transform.GetChild(0).GetChild(4).GetChild(3).gameObject;
            betLines[4] = instance.transform.GetChild(0).GetChild(4).GetChild(4).gameObject;
            for (int i = 0; i < 3; i++)
            {
                rotatingWheels.transform.GetChild(i).GetComponent<RevolvingNumber>().stepDuration = 0.3f;
            }
            slotObjectOriginals = new GameObject[]{
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(0).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(1).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(2).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(3).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(4).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(5).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(6).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(7).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(8).gameObject,
                rotatingWheels.transform.GetChild(0).GetChild(0).GetChild(9).gameObject };
            topLight = instance.transform.GetChild(0).GetChild(5).gameObject;
            soundsParent = instance.transform.GetChild(0).GetChild(6).gameObject;
            SetupRandom();
            SetupStart();
            Log("Start Completed", Preferences.debugging.Value);
        }

        void OnDestroy()
        {
            Log("OnDestroy Started", Preferences.debugging.Value);
            if (spinCoroutines[2] != null) { MelonCoroutines.Stop(spinCoroutines[2]); }
            if (spinCoroutines[1] != null) { MelonCoroutines.Stop(spinCoroutines[1]); }
            if (spinCoroutines[0] != null) { MelonCoroutines.Stop(spinCoroutines[0]); }
            if (spinBabySpin != null) { MelonCoroutines.Stop(spinBabySpin); }
            while (gemRotateActive.Count > 0)
            {
                Destroy(gemRotateActive[0]);
                gemRotateActive.RemoveAt(0);
            }
            Log("OnDestroy Completed", Preferences.debugging.Value);
        }

        private void FixWheels()
        {
            Log("FixWheels Called", Preferences.debugging.Value);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    rotatingWheels.transform.GetChild(i).GetChild(0).GetChild(slotsOrder[i][j]).localRotation = Quaternion.Euler(0, 0, 270f - (j * 36));
                }
            }
        }

        public void SetupRandom()
        {
            Log("SetupRandom Started", Preferences.debugging.Value);
            string seedString = "0123456789";
            if (!Preferences.useSeed.Value)
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
                seed = Preferences.tableSeed.Value;
            }
            Preferences.slotsSeed.Value = seed;
            random = new Random(seed);
            Log("SetupRandom Complete", Preferences.debugging.Value);
        }

        public void SetupStart()
        {
            Log("SetupStart Started", Preferences.debugging.Value);
            SetupLever();
            SetupButtons();
            SetupSlotMachineSideWall();
            Log("SetupStart Complete", Preferences.debugging.Value);
        }

        private void SetupSlotMachineSideWall()
        {
            Log("SetupSlotMachineSideWall Started", Preferences.debugging.Value);
            GameObject sideMenu = NewGameObject("SideMenu", instance.transform.GetChild(0), new Vector3(18.5282f, 56.9347f, -17.3875f), Quaternion.Euler(0f, 180f, 0), new Vector3(500, 500, 500));

            //line 1: x3 same
            GameObject line1Parent = NewGameObject("Line1", sideMenu.transform, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            GameObject line1Gems1 = NewGameObject("Gems1", line1Parent.transform, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line1Gems1.transform, 0, true);
            GameObject line1Gems2 = NewGameObject("Gems2", line1Parent.transform, new Vector3(0, 0, -0.015f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line1Gems2.transform, 0, true);
            GameObject line1Gems3 = NewGameObject("Gems3", line1Parent.transform, new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line1Gems3.transform, 0, true);
            GameObject line1Texts = NewGameObject("Texts", line1Parent.transform, new Vector3(-0.021f, 0f, -0.047f), Quaternion.Euler(0, 0, 0), new Vector3(0.2f, 0.2f, 0.2f));
            string[] payouts = { "1000", "70", "100", "200", "50", "90", "60", "80", "150", "45" };
            SpawnAllTexts(line1Texts.transform, payouts, new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(0.5f, 0.5f, 0.5f));

            //line 2: howard + x2 same
            GameObject line2Parent = NewGameObject("Line2", sideMenu.transform, new Vector3(0, -0.016f, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            GameObject line2Gems1 = NewGameObject("Gems1", line2Parent.transform, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line2Gems1.transform, 0);
            GameObject line2Gems2 = NewGameObject("Gems2", line2Parent.transform, new Vector3(0, 0, -0.015f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line2Gems2.transform, 0);
            GameObject line2Howard = NewGameObject("Howard", line2Parent.transform, new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnSlotAsChild(line2Howard.transform, 9);
            GameObject line2Texts = NewGameObject("Texts", line2Parent.transform, new Vector3(-0.021f, 0f, -0.047f), Quaternion.Euler(0, 0, 0), new Vector3(0.2f, 0.2f, 0.2f));
            string[] payouts2 = { "100", "7", "10", "20", "5", "9", "6", "8", "15" };
            SpawnAllTexts(line2Texts.transform, payouts2, new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(0.5f, 0.5f, 0.5f));

            //line 3: 2 howards
            GameObject line3Parent = NewGameObject("Line3", sideMenu.transform, new Vector3(0, -0.032f, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            GameObject line3Gems1 = NewGameObject("Gems1", line3Parent.transform, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line3Gems1.transform, 1);
            GameObject line3Howard1 = NewGameObject("Howard", line3Parent.transform, new Vector3(0, 0, -0.015f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnSlotAsChild(line3Howard1.transform, 9);
            GameObject line3Howard2 = NewGameObject("Howard", line3Parent.transform, new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnSlotAsChild(line3Howard2.transform, 9);
            GameObject line3Texts = NewGameObject("Texts", line3Parent.transform, new Vector3(-0.021f, 0f, -0.047f), Quaternion.Euler(0, 0, 0), new Vector3(0.2f, 0.2f, 0.2f));
            SpawnText(line3Texts.transform, "5", new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(0.5f, 0.5f, 0.5f));

            //line 4: 1 howard only
            GameObject line4Parent = NewGameObject("Line4", sideMenu.transform, new Vector3(0, -0.048f, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            GameObject line4Gems1 = NewGameObject("Gems1", line4Parent.transform, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line4Gems1.transform, 2);
            GameObject line4Gems2 = NewGameObject("Gems2", line4Parent.transform, new Vector3(0, 0, -0.015f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnAllSlotsAsChildren(line4Gems2.transform, 3);
            GameObject line4Howard = NewGameObject("Howard", line4Parent.transform, new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), new Vector3(1, 1, 1));
            SpawnSlotAsChild(line4Howard.transform, 9);
            GameObject line4Texts = NewGameObject("Texts", line4Parent.transform, new Vector3(-0.021f, 0f, -0.047f), Quaternion.Euler(0, 0, 0), new Vector3(0.2f, 0.2f, 0.2f));
            SpawnText(line4Texts.transform, "1", new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(0.5f, 0.5f, 0.5f));

            Log("SetupSlotMachineSideWall - Starting Rotating Gems", Preferences.debugging.Value);
            SlotObjectAlternator gemRotate = new SlotObjectAlternator(line1Gems1);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line1Gems2);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line1Gems3);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line1Texts);
            gemRotateActive.Add(gemRotate);

            gemRotate = new SlotObjectAlternator(line2Gems1);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line2Gems2);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line2Texts);
            gemRotateActive.Add(gemRotate);

            gemRotate = new SlotObjectAlternator(line3Gems1);
            gemRotateActive.Add(gemRotate);

            gemRotate = new SlotObjectAlternator(line4Gems1);
            gemRotateActive.Add(gemRotate);
            gemRotate = new SlotObjectAlternator(line4Gems2);
            gemRotateActive.Add(gemRotate);
            Log("SetupSlotMachineSideWall Completed", Preferences.debugging.Value);
        }
        
        private GameObject NewGameObject(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, GameObject objectToInstatiate = null)
        {
            GameObject newGameObject;
            if (objectToInstatiate == null)
            {
                newGameObject = new GameObject(name);
            }
            else
            {
                newGameObject = GameObject.Instantiate(objectToInstatiate);
            }
            newGameObject.name = name;
            newGameObject.transform.SetParent(parent);
            newGameObject.transform.localPosition = localPosition;
            newGameObject.transform.localRotation = localRotation;
            newGameObject.transform.localScale = localScale;
            return newGameObject;
        }

        private void SpawnAllSlotsAsChildren(Transform parent, int startingSpot, bool includeHoward = false)
        {
            for (int i = 0; i < 9 + (includeHoward ? 1 : 0); i++)
            {
                SpawnSlotAsChild(parent, startingSpot);
                startingSpot++;
                if (startingSpot == 9 + (includeHoward ? 1 : 0)) { startingSpot = 0; }
            }
        }

        private void SpawnSlotAsChild(Transform parent, int spot)
        {
            GameObject slotSpot = GameObject.Instantiate(slotObjectOriginals[spot]);
            slotSpot.name = SlotsSpotTypesString[spot];
            slotSpot.transform.SetParent(parent);
            slotSpot.transform.localPosition = Vector3.zero;
            slotSpot.transform.localRotation = Quaternion.Euler(0f, 0f, 0);
            slotSpot.transform.localScale = Vector3.one;
        }

        public void SpawnAllTexts(Transform parent, string[] title, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            for (int i = 0; i < title.Length; i++)
            {
                SpawnText(parent, title[i], position, rotation, localScale);
            }
        }

        private void SetupButtons()
        {
            Log("SetupButtons Started", Preferences.debugging.Value);
            GameObject bet1Button = LoadMenuButton("1 Row",
                /*position*/ new Vector3(0.31f, 0.87f, 0.3f),
                /*rotation*/Quaternion.Euler(15, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                true,
                () => {
                    this.photonView.RPC("RPC_BetButtonPressed", RpcTarget.Others, new Il2CppSystem.Object[] { 0 });
                    BetButtonPressed(0);
                });
            GameObject bet2Button = LoadMenuButton("3 Row",
                /*position*/ new Vector3(0.09f, 0.87f, 0.3f),
                /*rotation*/Quaternion.Euler(15, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                true,
                () => {
                    this.photonView.RPC("RPC_BetButtonPressed", RpcTarget.Others, new Il2CppSystem.Object[] { 1 });
                    BetButtonPressed(1);
                });
            GameObject bet3Button = LoadMenuButton("5 Row",
                /*position*/ new Vector3(-0.14f, 0.87f, 0.3f),
                /*rotation*/Quaternion.Euler(15f, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                true,
                () => {
                    this.photonView.RPC("RPC_BetButtonPressed", RpcTarget.Others, new Il2CppSystem.Object[] { 2 });
                    BetButtonPressed(2);
                });
            freePlayButton = LoadMenuButton("FreePlay",
                /*position*/ new Vector3(0.4f, 0.7f, 0.4f),
                /*rotation*/Quaternion.Euler(90, 0, 0),
                /*scale*/ new Vector3(0.5f, 0.5f, 0.5f),
                false,
                () => {
                    if ((!spinning) && (!Preferences.useSeed.Value))
                    {
                        SetFreePlay(!freePlay);
                        this.photonView.RPC("RPC_FreePlayPressed", RpcTarget.Others, new Il2CppSystem.Object[] { freePlay });
                    }
                });
            slotMachineTitleText = SpawnText(
                /*parent*/ activeObjects.transform,
                /*title*/ "Title",
                /*position*/ new Vector3(0.19f, 1.47f, 0.21f),
                /*rotation*/ Quaternion.Euler(13f, 180f, 0f),
                /*scale*/ new Vector3(1, 1, 1));
            coinsIn = SpawnText(
                /*parent*/ activeObjects.transform,
                /*title*/ "Coins In",
                /*position*/ new Vector3(0.38f, 0.57f, 0.41f),
                /*rotation*/ Quaternion.Euler(0f, 180f, 0f),
                /*scale*/ new Vector3(1, 1, 1));
            coinsOut = SpawnText(
                /*parent*/ activeObjects.transform,
                /*title*/ "Coins Out",
                /*position*/ new Vector3(0.34f, 0.47f, 0.41f),
                /*rotation*/ Quaternion.Euler(0f, 180f, 0f),
                /*scale*/ new Vector3(1, 1, 1));
            SetFreePlay(freePlay);
            betAmount = 1;
            UpdateBetLines();
            activeObjects.transform.GetChild(0).GetChild(1).GetComponent<TextMeshPro>().color = Color.green;
            activeObjects.transform.GetChild(1).GetChild(1).GetComponent<TextMeshPro>().color = Color.black;
            activeObjects.transform.GetChild(2).GetChild(1).GetComponent<TextMeshPro>().color = Color.black;
            TextMeshPro titleTMP = slotMachineTitleText.GetComponent<TextMeshPro>();
            TextMeshPro coinsInTMP = coinsIn.GetComponent<TextMeshPro>();
            TextMeshPro coinsOutTMP = coinsOut.GetComponent<TextMeshPro>();
            titleTMP.text = "Ready To Play!";
            coinsInTMP.text = "In: 0";
            coinsOutTMP.text = "Out: 0";
            titleTMP.alignment = TextAlignmentOptions.Left;
            coinsInTMP.alignment = TextAlignmentOptions.Left;
            coinsOutTMP.alignment = TextAlignmentOptions.Left;
            titleTMP.enableWordWrapping = false;
            coinsInTMP.enableWordWrapping = false;
            coinsOutTMP.enableWordWrapping = false;
            Log("SetupButtons Completed", Preferences.debugging.Value);
        }

        [RumbleModdingAPI.RMAPI.PhotonRPCs.PunRPC]
        public void RPC_FreePlayPressed (bool isFreePlay)
        {
            SetFreePlay(isFreePlay);
        }

        private void BetButtonPressed(int button)
        {
            if (!spinning)
            {
                switch (button)
                {
                    case 0:
                        activeObjects.transform.GetChild(0).GetChild(2).GetComponent<TextMeshPro>().color = Color.green;
                        activeObjects.transform.GetChild(1).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        activeObjects.transform.GetChild(2).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        betAmount = 1;
                        break;
                    case 1:
                        activeObjects.transform.GetChild(0).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        activeObjects.transform.GetChild(1).GetChild(2).GetComponent<TextMeshPro>().color = Color.green;
                        activeObjects.transform.GetChild(2).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        betAmount = 3;
                        break;
                    case 2:
                        activeObjects.transform.GetChild(0).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        activeObjects.transform.GetChild(1).GetChild(2).GetComponent<TextMeshPro>().color = Color.black;
                        activeObjects.transform.GetChild(2).GetChild(2).GetComponent<TextMeshPro>().color = Color.green;
                        betAmount = 5;
                        break;
                    case 3:
                        SetFreePlay(!freePlay);
                        break;
                }
                UpdateBetLines();
            }
        }

        private void UpdateBetLines()
        {
            Log("UpdateBetLines Called", Preferences.debugging.Value);
            betLines[1].SetActive(betAmount >= 3);
            betLines[2].SetActive(betAmount >= 3);
            betLines[3].SetActive(betAmount == 5);
            betLines[4].SetActive(betAmount == 5);
        }

        private void SetupLever()
        {
            Log("SetupLever Started", Preferences.debugging.Value);
            lever = instance.transform.GetChild(0).GetChild(0).GetChild(2).gameObject.AddComponent<InteractionLever>();
            lever.LeverPulled += PulledLever;
            lever.OnLeverReleased += ToggleLeverRelease;
            lever.OnLeverReleasedComplete += ToggleLeverReleaseComplete;
            Log("SetupLever Completed", Preferences.debugging.Value);
        }

        PooledAudioSource playingAudio;
        private void ToggleLeverRelease()
        {
            Log("ToggleLeverRelease Called", Preferences.debugging.Value);
            MelonCoroutines.Start(PlayWheelSpinSoundTillStopped());
        }

        private void ToggleLeverReleaseComplete()
        {
            Log("ToggleLeverReleaseComplete Called", Preferences.debugging.Value);
            continueWheelSpinSound = false;
            if (!playingAudio.IsInPool)
            {
                playingAudio.ReturnToPool();
            }
        }

        private IEnumerator PlayWheelSpinSoundTillStopped()
        {
            Log("PlayWheelSpinSoundTillStopped Started", Preferences.debugging.Value);
            continueWheelSpinSound = true;
            while (continueWheelSpinSound)
            {
                playingAudio = AudioManager.instance.Play(Main.storedAudioCalls[0], soundsParent.transform.GetChild(0).position);
                yield return new WaitForSeconds(playingAudio.audioSource.clip.length);
            }
            Log("PlayWheelSpinSoundTillStopped Completed", Preferences.debugging.Value);
            yield break;
        }

        public void SetFreePlay(bool isFreePlay)
        {
            Log($"SetFreePlay({isFreePlay}) Called", Preferences.debugging.Value);
            freePlay = isFreePlay;
            freePlayButton.transform.GetChild(1).GetComponent<TextMeshPro>().color = freePlay ? Color.green : Color.red;
        }

        public void PulledLever()
        {
            Log("Pulled Lever Called", Preferences.debugging.Value);
            if (!IsSpinning() && (freePlay || PlayerHasEnoughCoins()))
            {
                try { if (lever.lastInteractedPlayer.Controller.controllerType != ControllerType.Local) { return; } } catch { return; }
                if (!freePlay)
                {
                    Main.Payout(-betAmount, 1f, this);
                    amountPutIn += betAmount;
                    coinsIn.GetComponent<TextMeshPro>().text = "In: " + amountPutIn;
                    PlayCoinInsertSound();
                }
                stopWinAnimation = true;
                object spinBabySpin = MelonCoroutines.Start(PlaySpin());
            }
        }

        private void PlayCoinInsertSound()
        {
            Log("PlayCoinInsertSound Started", Preferences.debugging.Value);
            AudioManager.instance.Play(Main.storedAudioCalls[1], soundsParent.transform.GetChild(1).position);
        }

        private bool PlayerHasEnoughCoins()
        {
            return (Main.GetPlayerCoinCount() >= betAmount);
        }

        [RumbleModdingAPI.RMAPI.PhotonRPCs.PunRPC]
        public void RPC_BetButtonPressed(short button)
        {
            Log($"RPC_BetButtonPressed({button}) Called", Preferences.debugging.Value);
            BetButtonPressed(button);
        }

        private IEnumerator PlaySpin()
        {
            Log("Spinning Wheels", Preferences.debugging.Value);
            spinning = true;
            yield return MelonCoroutines.Start(RandomizeSlots());
            MelonCoroutines.Start(StartWheelSpinSound());
            slotMachineTitleText.GetComponent<TextMeshPro>().text = "Spinning Wheels";
            for (int i = 0; i < 3; i++)
            {
                bool extraSpins = false;
                if ((i == 3) && (random.Next(0, 25) == 0))
                {
                    extraSpins = true;
                }
                spinCoroutines[i] = MelonCoroutines.Start(SpinNumber(rotatingWheels.transform.GetChild(i).GetComponent<RevolvingNumber>(), i, extraSpins));
            }
            yield return spinCoroutines[0];
            Log("Wheel 1 Done: " + SlotsSpotTypesString[slotsOrder[0][wheelsNumber[0]]], Preferences.debugging.Value);
            this.photonView.RPC("RPC_RotateToNumber", RpcTarget.Others, new Il2CppSystem.Object[] { 0, wheelsNumber[0] });
            yield return spinCoroutines[1];
            Log("Wheel 2 Done: " + SlotsSpotTypesString[slotsOrder[1][wheelsNumber[1]]], Preferences.debugging.Value);
            this.photonView.RPC("RPC_RotateToNumber", RpcTarget.Others, new Il2CppSystem.Object[] { 1, wheelsNumber[1] });
            yield return spinCoroutines[2];
            Log("Wheel 3 Done: " + SlotsSpotTypesString[slotsOrder[2][wheelsNumber[2]]], Preferences.debugging.Value);
            this.photonView.RPC("RPC_RotateToNumber", RpcTarget.Others, new Il2CppSystem.Object[] { 2, wheelsNumber[2] });
            yield return MelonCoroutines.Start(CheckWinPhase());
            yield break;
        }

        private IEnumerator StartWheelSpinSound()
        {
            playingWheelSpinSound = true;
            PooledAudioSource pooledAudio;
            while (playingWheelSpinSound)
            {
                pooledAudio = AudioManager.instance.Play(Main.storedAudioCalls[2], soundsParent.transform.GetChild(2).position);
                yield return new WaitForSeconds(pooledAudio.audioSource.clip.length * 2f);
            }
            yield break;
        }

        private IEnumerator CheckWinPhase(bool isPlaying = true)
        {
            Log("CheckWinPhase Started", Preferences.debugging.Value);
            playingWheelSpinSound = false;
            //create wheel face in Numbers
            int[][] wheelNumberLineup = new int[3][];
            int[] minusOneNumber = { wheelsNumber[0] - 1, wheelsNumber[1] - 1, wheelsNumber[2] - 1 };
            int[] plusOneNumber = { wheelsNumber[0] + 1, wheelsNumber[1] + 1, wheelsNumber[2] + 1 };
            if (minusOneNumber[0] < 0) { minusOneNumber[0] += 10; }
            if (minusOneNumber[1] < 0) { minusOneNumber[1] += 10; }
            if (minusOneNumber[2] < 0) { minusOneNumber[2] += 10; }
            if (plusOneNumber[0] > 9) { plusOneNumber[0] -= 10; }
            if (plusOneNumber[1] > 9) { plusOneNumber[1] -= 10; }
            if (plusOneNumber[2] > 9) { plusOneNumber[2] -= 10; }
            int[] line0 = { slotsOrder[0][minusOneNumber[0]], slotsOrder[1][minusOneNumber[1]], slotsOrder[2][minusOneNumber[2]]};
            int[] line1 = { slotsOrder[0][wheelsNumber[0]], slotsOrder[1][wheelsNumber[1]], slotsOrder[2][wheelsNumber[2]] };
            int[] line2 = { slotsOrder[0][plusOneNumber[0]], slotsOrder[1][plusOneNumber[1]], slotsOrder[2][plusOneNumber[2]] };
            wheelNumberLineup[0] = line0;
            wheelNumberLineup[1] = line1;
            wheelNumberLineup[2] = line2;
            for (int i = 0; i < 3; i++)
            {
                Log($"Slot {i}: {SlotsSpotTypesString[slotsOrder[i][0]]} {SlotsSpotTypesString[slotsOrder[i][1]]} {SlotsSpotTypesString[slotsOrder[i][2]]} {SlotsSpotTypesString[slotsOrder[i][3]]} {SlotsSpotTypesString[slotsOrder[i][4]]} {SlotsSpotTypesString[slotsOrder[i][5]]} {SlotsSpotTypesString[slotsOrder[i][6]]} {SlotsSpotTypesString[slotsOrder[i][7]]} {SlotsSpotTypesString[slotsOrder[i][8]]} {SlotsSpotTypesString[slotsOrder[i][9]]}", Preferences.debugging.Value);
            }
            Log($"Wheel : {SlotsSpotTypesString[wheelNumberLineup[2][0]]} {SlotsSpotTypesString[wheelNumberLineup[2][1]]} {SlotsSpotTypesString[wheelNumberLineup[2][2]]}", Preferences.debugging.Value);
            Log($"Wheel : {SlotsSpotTypesString[wheelNumberLineup[1][0]]} {SlotsSpotTypesString[wheelNumberLineup[1][1]]} {SlotsSpotTypesString[wheelNumberLineup[1][2]]}", Preferences.debugging.Value);
            Log($"Wheel : {SlotsSpotTypesString[wheelNumberLineup[0][0]]} {SlotsSpotTypesString[wheelNumberLineup[0][1]]} {SlotsSpotTypesString[wheelNumberLineup[0][2]]}", Preferences.debugging.Value);
            TextMeshPro titleTextMP = slotMachineTitleText.GetComponent<TextMeshPro>();
            titleTextMP.text = "Payout: ";
            List<object> showTopWinCoroutines = new List<object>();
            int payout = 0;
            bool hasWon = false;
            stopWinAnimation = false;
            //bet1
            Log("Checking Middle Line", Preferences.debugging.Value);
            int amount = CheckWinLine(wheelNumberLineup[1]);
            if (amount != 0)
            {
                if (hasWon) { titleTextMP.text += " + "; }
                else { hasWon = true; }
                titleTextMP.text += amount.ToString();
                MelonCoroutines.Start(FlashColors(betLines[0]));
                showTopWinCoroutines.Add(MelonCoroutines.Start(DisplayWinOnTop(wheelNumberLineup[1])));
                PlayPayoutSound();
                yield return new WaitForSeconds(1.75f);
            }
            Log("Payout: " + amount, Preferences.debugging.Value);
            payout += amount;
            //bet3
            if (betAmount >= 3)
            {
                Log("Checking Bottom Line", Preferences.debugging.Value);
                amount = CheckWinLine(wheelNumberLineup[0]);
                if (amount != 0)
                {
                    if (hasWon) { titleTextMP.text += " + "; }
                    else { hasWon = true; }
                    titleTextMP.text += amount.ToString();
                    MelonCoroutines.Start(FlashColors(betLines[1]));
                    showTopWinCoroutines.Add(MelonCoroutines.Start(DisplayWinOnTop(wheelNumberLineup[0])));
                    PlayPayoutSound();
                    yield return new WaitForSeconds(1.75f);
                }
                Log("Payout: " + amount, Preferences.debugging.Value);
                payout += amount;
                Log("Checking Top Line", Preferences.debugging.Value);
                amount = CheckWinLine(wheelNumberLineup[2]);
                if (amount != 0)
                {
                    if (hasWon) { titleTextMP.text += " + "; }
                    else { hasWon = true; }
                    slotMachineTitleText.GetComponent<TextMeshPro>().text += amount.ToString();
                    MelonCoroutines.Start(FlashColors(betLines[2]));
                    showTopWinCoroutines.Add(MelonCoroutines.Start(DisplayWinOnTop(wheelNumberLineup[2])));
                    PlayPayoutSound();
                    yield return new WaitForSeconds(1.75f);
                }
                Log("Payout: " + amount, Preferences.debugging.Value);
                payout += amount;
            }
            //bet5
            if (betAmount >= 5)
            {
                int[] crossline = { wheelNumberLineup[0][0], wheelNumberLineup[1][1], wheelNumberLineup[2][2] };
                int[] crossline2 = { wheelNumberLineup[0][2], wheelNumberLineup[1][1], wheelNumberLineup[2][0] };
                Log("Checking Top Left to Bottom Right Line", Preferences.debugging.Value);
                amount = CheckWinLine(crossline2);
                if (amount != 0)
                {
                    if (hasWon) { titleTextMP.text += " + "; }
                    else { hasWon = true; }
                    titleTextMP.text += amount.ToString();
                    MelonCoroutines.Start(FlashColors(betLines[3]));
                    showTopWinCoroutines.Add(MelonCoroutines.Start(DisplayWinOnTop(crossline2)));
                    PlayPayoutSound();
                    yield return new WaitForSeconds(1.75f);
                }
                Log("Payout: " + amount, Preferences.debugging.Value);
                payout += amount;
                Log("Checking Bottom Left to Top Right Line", Preferences.debugging.Value);
                amount = CheckWinLine(crossline);
                if (amount != 0)
                {
                    if (hasWon) { titleTextMP.text += " + "; }
                    else { hasWon = true; }
                    titleTextMP.text += amount.ToString();
                    MelonCoroutines.Start(FlashColors(betLines[4]));
                    showTopWinCoroutines.Add(MelonCoroutines.Start(DisplayWinOnTop(crossline)));
                    PlayPayoutSound();
                    yield return new WaitForSeconds(1.75f);
                }
                Log("Payout: " + amount, Preferences.debugging.Value);
                payout += amount;
            }
            if (payout == 0)
            {
                titleTextMP.text += "0";
            }
            yield return new WaitForSeconds(0.25f);
            //play payout text animation
            string payoutPart = "Payout: ";
            string numbersPart = titleTextMP.text.Substring(8, titleTextMP.text.Length - 8);
            if (numbersPart.Contains("+"))
            {
                Log("Animating Payout Text", Preferences.debugging.Value);
                while (numbersPart.Length > 0)
                {
                    numbersPart = numbersPart.Remove(random.Next(0, numbersPart.Length), 1);
                    titleTextMP.text = payoutPart + numbersPart;
                    yield return new WaitForSeconds(0.1f);
                }
                numbersPart = "";
                titleTextMP.text = payoutPart + numbersPart;
                string payoutString = payout.ToString();
                for (int i = payoutString.Length - 1; i >= 0; i--)
                {
                    numbersPart = payoutString.Substring(i, payoutString.Length - i);
                    titleTextMP.text = payoutPart + numbersPart;
                    yield return new WaitForSeconds(0.1f);
                }
            }
            if (payout > 0) { Log($"Paying Out: (FreePlay: {(freePlay ? "Yes" : "No")}) " + payout, true); }
            if (!freePlay && isPlaying)
            {
                amountGotOut += payout;
                Main.Payout(payout, 1, this);
                coinsOut.GetComponent<TextMeshPro>().text = "Out: " + amountGotOut;
            }
            if (payout == 0)
            {
                PlayNoPayoutSound();
            }
            else if (payout >= 100)
            {
                MelonCoroutines.Start(AnimateTopLight(payout));
                yield return MelonCoroutines.Start(PlayBigPayoutSound(payout));
            }
            foreach(object coroutine in showTopWinCoroutines) { yield return coroutine; }
            spinning = false;
            Log("CheckWinPhase Completed", Preferences.debugging.Value);
            yield break;
        }

        private void PlayNoPayoutSound()
        {
            Log("PlayNoPayoutSound Called", Preferences.debugging.Value);
            AudioManager.instance.Play(Main.storedAudioCalls[5], soundsParent.transform.GetChild(3).position);
        }

        private IEnumerator AnimateTopLight(int payout)
        {
            Log("AnimateTopLight Started", Preferences.debugging.Value);
            AudioSource sound = soundsParent.transform.GetChild(4).gameObject.GetComponent<AudioSource>();
            topLight.transform.localPosition = new Vector3(4.15874f, 74.5f, -6.97464f);
            topLight.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            topLight.SetActive(true);
            int ticks = 25;
            float currentRotation = 0;
            float currentHeight = 74.5f;
            float heightPerTick = 7.692f / ticks;
            for (int i = 0; i < ticks; i++)
            {
                currentRotation += 5f;
                currentHeight += heightPerTick;
                topLight.transform.localPosition = new Vector3(4.15874f, currentHeight, -6.97464f);
                topLight.transform.localRotation = Quaternion.Euler(-90f, 0f, currentRotation);
                yield return new WaitForFixedUpdate();
            }
            topLight.transform.localPosition = new Vector3(4.15874f, 82.192f, -6.97464f);
            DateTime soundDoneTime = DateTime.Now.AddSeconds(((Main.storedAudioCalls[4].clips[0].Clip.length * 0.7f) * ((int)(payout / 100))) - 0.5f);
            while (DateTime.Now < soundDoneTime)
            {
                currentRotation += 5f;
                topLight.transform.localRotation = Quaternion.Euler(-90f, 0f, currentRotation);
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < ticks; i++)
            {
                currentRotation += 5f;
                currentHeight -= heightPerTick;
                topLight.transform.localPosition = new Vector3(4.15874f, currentHeight, -6.97464f);
                topLight.transform.localRotation = Quaternion.Euler(-90f, 0f, currentRotation);
                yield return new WaitForFixedUpdate();
            }
            topLight.SetActive(false);
            Log("AnimateTopLight Completed", Preferences.debugging.Value);
            yield break;
        }

        private void PlayPayoutSound()
        {
            Log("PlayPayoutSound Called", Preferences.debugging.Value);
            AudioManager.instance.Play(Main.storedAudioCalls[3], soundsParent.transform.GetChild(3).position);
        }

        private IEnumerator PlayBigPayoutSound(int payout)
        {
            Log($"PlayBigPayoutSound({payout}) Called", Preferences.debugging.Value);
            PooledAudioSource pooledAudio;
            for (int i = 0; i < ((int)(payout / 100)); i++)
            {
                pooledAudio = AudioManager.instance.Play(Main.storedAudioCalls[4], soundsParent.transform.GetChild(4).position);
                yield return new WaitForSeconds(pooledAudio.audioSource.clip.length * 0.7f);
            }
            yield break;
        }

        private IEnumerator DisplayWinOnTop(int[] line)
        {
            Log($"DisplayWinOnTop({line[0]} {line[1]} {line[2]}) Started", Preferences.debugging.Value);
            GameObject[] slotObjects = new GameObject[line.Length];
            GameObject[] slotObjectsParents = new GameObject[line.Length];
            float[] positions = { 0.33f, 0.12f, -0.14f };
            for (int i = 0; i < line.Length; i++)
            {
                slotObjectsParents[i] = new GameObject("SlotObjectPosition" + i);
                slotObjectsParents[i].transform.SetParent(activeObjects.transform);
                slotObjectsParents[i].transform.localPosition = new Vector3(positions[i], 1.5f, 0.1f);
                slotObjectsParents[i].transform.localRotation = Quaternion.identity;
                slotObjectsParents[i].transform.localScale = Vector3.one;
                slotObjects[i] = GameObject.Instantiate(slotObjectOriginals[line[i]]);
                slotObjects[i].name = SlotsSpotTypesString[line[i]];
                slotObjects[i].transform.SetParent(slotObjectsParents[i].transform);
                slotObjects[i].transform.localPosition = Vector3.zero;
                if (line[i] == 0)
                {
                    slotObjects[i].transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
                    slotObjects[i].transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
                }
                else if (line[i] == 9)
                {
                    slotObjects[i].transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    slotObjects[i].transform.localScale = new Vector3(0.09f, 0.08f, 0.09f);
                    slotObjects[i].transform.GetChild(0).localPosition = new Vector3(-0.021f, -0.905f, 0f);
                }
                else
                {
                    slotObjects[i].transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    slotObjects[i].transform.localScale = new Vector3(2f, 2f, 2f);
                    if (line[i] == 1)
                    {
                        slotObjects[i].transform.localPosition = new Vector3(0, 0.03f, -0.05f);
                    }
                    else if (line[i] == 2)
                    {
                        slotObjects[i].transform.localPosition = new Vector3(0, 0.04f, 0f);
                        slotObjects[i].transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    else
                    {
                        slotObjects[i].transform.localPosition = new Vector3(0, 0, -0.05f);
                    }
                }
                slotObjects[i].transform.GetChild(0).localScale = new Vector3(1f, 1f, 1f);
            }
            float distancePerTick = 0.006f;
            float rotationPerTick = 10f;
            float currentScale = 1;
            for (int i = 0; i < 125; i++)
            {
                for (int j = 0; j < slotObjectsParents.Length; j++)
                {
                    //move up first 25 ticks
                    if ((i < 25) || (i >= 100))
                    {
                        slotObjectsParents[j].transform.localPosition = new Vector3(slotObjectsParents[j].transform.localPosition.x, slotObjectsParents[j].transform.localPosition.y + distancePerTick, slotObjectsParents[j].transform.localPosition.z);
                    }
                    //start rotating till the end
                    if (i >= 25)
                    {
                        slotObjectsParents[j].transform.localRotation = Quaternion.Euler(slotObjectsParents[j].transform.localRotation.eulerAngles.x, slotObjectsParents[j].transform.localRotation.eulerAngles.y - rotationPerTick, slotObjectsParents[j].transform.localRotation.eulerAngles.z);
                    }
                    ///move up and scale to 0 for remaining for 25 ticks (starting on tick 100)
                    if (i >= 100)
                    {
                        slotObjectsParents[j].transform.localPosition = new Vector3(slotObjectsParents[j].transform.localPosition.x, slotObjectsParents[j].transform.localPosition.y + distancePerTick, slotObjectsParents[j].transform.localPosition.z);
                        currentScale = (float)(124 - i) / 25;
                        slotObjectsParents[j].transform.localScale = new Vector3(currentScale, currentScale, currentScale);
                    }
                }
                yield return new WaitForFixedUpdate();
            }
            foreach (GameObject slotObject in slotObjectsParents) { GameObject.Destroy(slotObject); }
            Log($"DisplayWinOnTop({line.ToString()}) Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator FlashColors(GameObject betLine)
        {
            MeshRenderer[] meshes = new MeshRenderer[betLine.transform.GetChildCount()];
            for (int i = 0; i < betLine.transform.GetChildCount(); i++)
            {
                meshes[i] = betLine.transform.GetChild(i).GetComponent<MeshRenderer>();
            }
            float r = 0f, g = 0f, b = 1f;
            int currentDirectionR = 1, currentDirectionG = 1, currentDirectionB = -1;
            bool[] directionIsMoving = { false, true, false };
            while (!stopWinAnimation)
            {
                for (int j = 0; j < 5; j++)
                {
                    try
                    {
                        if (directionIsMoving[0])
                        {
                            r += (float)currentDirectionR * 0.2f;
                        }
                        else if (directionIsMoving[1])
                        {
                            g += (float)currentDirectionG * 0.2f;
                        }
                        else if (directionIsMoving[2])
                        {
                            b += (float)currentDirectionB * 0.2f;
                        }
                        for (int i = 0; i < meshes.Length; i++)
                        {
                            meshes[i].material.color = new Color(r, g, b);
                        }
                    }
                    catch { yield break; }
                    yield return new WaitForSeconds(0.1f);
                    if (stopWinAnimation) { break; }
                }
                if (directionIsMoving[0])
                {
                    directionIsMoving = new bool[] { false, true, false };
                    currentDirectionR *= -1;
                }
                else if (directionIsMoving[1])
                {
                    directionIsMoving = new bool[] { false, false, true };
                    currentDirectionG *= -1;
                }
                else if (directionIsMoving[2])
                {
                    directionIsMoving = new bool[] { true, false, false };
                    currentDirectionB *= -1;
                }
            }
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].material.color = new Color(0.6667f, 0.6667f, 0.6314f);
            }
            yield break;
        }

        private int CheckWinLine(int[] line)
        {
            Log($"CheckWinLine: {line[0]} {line[1]} {line[2]}", Preferences.debugging.Value);
            int payout = 0;
            int howards = 0;
            //count howards
            for(int i = 0; i < 3; i++) { if (line[i] == 9) {  howards++; } }
            //if 1 Howard in line
            if (howards == 1)
            {
                //if 1 Howard + 2 matching
                if (((line[0] == line[1]) && (line[2] == 9)) || ((line[0] == line[2]) && (line[1] == 9)) || ((line[1] == line[2]) && (line[0] == 9)))
                {
                    int payoutAmount = setsPayouts[(line[0] == 9) ? (int)line[1] : (int)line[0]] / 10;
                    Log($"Line: {SlotsSpotTypesString[line[0]]} {SlotsSpotTypesString[line[1]]} {SlotsSpotTypesString[line[2]]} is a Winner, Paying {payoutAmount} Coins (1 Howard + 2 Matching)", true);
                    payout += payoutAmount;
                }
                //otherwise if just 1 Howard
                else
                {
                    Log($"Line: {SlotsSpotTypesString[line[0]]} {SlotsSpotTypesString[line[1]]} {SlotsSpotTypesString[line[2]]} is a Winner, Paying 1 Coin (1 Howard in a Row)", true);
                    payout += 1;
                }
            }
            //if 2 Howards in line
            else if (howards == 2)
            {
                Log($"Line: {SlotsSpotTypesString[line[0]]} {SlotsSpotTypesString[line[1]]} {SlotsSpotTypesString[line[2]]} is a Winner, Paying 5 Coins (2 Howards in a Row)", true);
                payout += 5;
            }
            //if all 3 match
            else if ((line[0] == line[1]) && (line[1] == line[2]))
            {
                if ((1 < line[0]) && (line[0] < 9))
                {
                    MelonCoroutines.Start(PlayShiftStoneSound());
                }
                int payoutAmount = setsPayouts[(int)line[0]];
                Log($"Line: {SlotsSpotTypesString[line[0]]} {SlotsSpotTypesString[line[1]]} {SlotsSpotTypesString[line[2]]} is a Winner, Paying {payoutAmount} Coins (All 3 Match)", true);
                payout += payoutAmount;
            }
            return payout;
        }

        private IEnumerator PlayShiftStoneSound()
        {
            Log("PlayShiftStoneSound Called", Preferences.debugging.Value);
            PooledAudioSource pooledAudio;
            pooledAudio = AudioManager.instance.Play(Main.storedAudioCalls[6], soundsParent.transform.GetChild(3).position);
            yield return new WaitForSeconds(0.25f);
            pooledAudio = AudioManager.instance.Play(Main.storedAudioCalls[6], soundsParent.transform.GetChild(3).position);
            yield return new WaitForSeconds(0.25f);
            pooledAudio = AudioManager.instance.Play(Main.storedAudioCalls[6], soundsParent.transform.GetChild(3).position);
            yield break;
        }

        private IEnumerator SpinNumber(RevolvingNumber revolvingNumber, int wheelSpot, bool extraSpins)
        {
            DateTime minTime = DateTime.Now.AddSeconds(2 + (wheelSpot * (wheelSpot == 1 ? 3f : 5f)) + (extraSpins ? 5f : 0f));
            while (DateTime.Now < minTime)
            {
                int wheelNumber = random.Next(0, 10);
                wheelsNumber[wheelSpot] = wheelNumber;
                revolvingNumber.RotateToNumber(wheelsNumber[wheelSpot]);
                yield return revolvingNumber.rotateCoroutine;
            }
            yield break;
        }

        [RumbleModdingAPI.RMAPI.PhotonRPCs.PunRPC]
        public void RPC_RotateToNumber(short wheel, short num)
        {
            Log("RPC_RotateToNumber Called", Preferences.debugging.Value);
            MelonCoroutines.Start(WaitForWheelSpinsToFinishThenSetFinalSpot(wheel, num));
        }

        private IEnumerator WaitForWheelSpinsToFinishThenSetFinalSpot(int wheel, int num)
        {
            Log($"WaitForWheelSpinsToFinishThenSetFinalSpot({wheel} {num}) Started", Preferences.debugging.Value);
            if (clientSpinningCoroutines[wheel] != null)
            {
                clientSpinning[wheel] = false;
                yield return clientSpinningCoroutines[wheel];
            }
            RevolvingNumber revolvingNumber = rotatingWheels.transform.GetChild(wheel).GetComponent<RevolvingNumber>();
            yield return revolvingNumber.rotateCoroutine;
            revolvingNumber.RotateToNumber(num);
            wheelsNumber[wheel] = num;
            yield return revolvingNumber.rotateCoroutine;
            MelonCoroutines.Start(CheckWinPhase(false));
            Log($"WaitForWheelSpinsToFinishThenSetFinalSpot({wheel} {num}) Completed", Preferences.debugging.Value);
            yield break;
        }

        public bool IsSpinning() { return spinning; }

        private IEnumerator RandomizeSlots()
        {
            Log("RandomizeSlots Started", Preferences.debugging.Value);
            for (int i = 0; i < 3; i++)
            {
                slotsOrder[i] = new int[10];
                for (int j = 0; j < 10; j++)
                {
                    slotsOrder[i][j] = j;
                }
                int temp;
                for (int j = 0; j < 10; j++)
                {
                    int randomSpot = random.Next(0, 10);
                    while (randomSpot == j) { randomSpot = random.Next(0, 10); }
                    temp = slotsOrder[i][j];
                    slotsOrder[i][j] = slotsOrder[i][randomSpot];
                    slotsOrder[i][randomSpot] = temp;
                    Log($"Slot {i}: Spots Flipped: {j} <-> {randomSpot}", Preferences.debugging.Value);
                }
            }
            this.photonView.RPC("RPC_RandomizeSlots", RpcTarget.Others, new Il2CppSystem.Object[] { freePlay, betAmount, slotsOrder[0][0], slotsOrder[0][1], slotsOrder[0][2], slotsOrder[0][3], slotsOrder[0][4], slotsOrder[0][5], slotsOrder[0][6], slotsOrder[0][7], slotsOrder[0][8], slotsOrder[0][9], slotsOrder[1][0], slotsOrder[1][1], slotsOrder[1][2], slotsOrder[1][3], slotsOrder[1][4], slotsOrder[1][5], slotsOrder[1][6], slotsOrder[1][7], slotsOrder[1][8], slotsOrder[1][9], slotsOrder[2][0], slotsOrder[2][1], slotsOrder[2][2], slotsOrder[2][3], slotsOrder[2][4], slotsOrder[2][5], slotsOrder[2][6], slotsOrder[2][7], slotsOrder[2][8], slotsOrder[2][9]});
            slotMachineTitleText.GetComponent<TextMeshPro>().text = "Randomizing";
            Transform screen = instance.transform.GetChild(0).GetChild(3);
            float movementPerTick = 0.04f / 75f;
            float currentRotation = screen.localRotation.eulerAngles.z;
            for (int i = 0; i < 75; i++)
            {
                rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, rotatingWheels.transform.localPosition.z - movementPerTick);
                if (i >= 25)
                {
                    currentRotation += 3.6f;
                    screen.transform.localRotation = Quaternion.Euler(0, 90, currentRotation);
                }
                yield return new WaitForFixedUpdate();
            }
            rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, -0.023f);
            screen.transform.localRotation = Quaternion.Euler(0, 90, 90);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    rotatingWheels.transform.GetChild(i).GetChild(0).GetChild(slotsOrder[i][j]).localRotation = Quaternion.Euler(0, 0, 270f - (j * 36));
                }
                Log($"Finished Slot {i}: {SlotsSpotTypesString[slotsOrder[i][0]]} {SlotsSpotTypesString[slotsOrder[i][1]]} {SlotsSpotTypesString[slotsOrder[i][2]]} {SlotsSpotTypesString[slotsOrder[i][3]]} {SlotsSpotTypesString[slotsOrder[i][4]]} {SlotsSpotTypesString[slotsOrder[i][5]]} {SlotsSpotTypesString[slotsOrder[i][6]]} {SlotsSpotTypesString[slotsOrder[i][7]]} {SlotsSpotTypesString[slotsOrder[i][8]]} {SlotsSpotTypesString[slotsOrder[i][9]]}", Preferences.debugging.Value);
            }
            currentRotation = screen.localRotation.eulerAngles.z;
            for (int i = 0; i < 75; i++)
            {
                if (i < 50)
                {
                    currentRotation += 3.6f;
                    screen.transform.localRotation = Quaternion.Euler(0, 90, currentRotation);
                }
                rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, rotatingWheels.transform.localPosition.z + movementPerTick);
                yield return new WaitForFixedUpdate();
            }
            rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, 0.017f);
            screen.transform.localRotation = Quaternion.Euler(0, 90, 270);
            Log("RandomizeSlots Completed", Preferences.debugging.Value);
            yield break;
        }

        [RumbleModdingAPI.RMAPI.PhotonRPCs.PunRPC]
        public void RPC_RandomizeSlots(bool isFreePlay, short thisBetAmount, short num0, short num1, short num2, short num3, short num4, short num5, short num6, short num7, short num8, short num9, short num10, short num11, short num12, short num13, short num14, short num15, short num16, short num17, short num18, short num19, short num20, short num21, short num22, short num23, short num24, short num25, short num26, short num27, short num28, short num29)
        {
            Log("RPC_RandomizeSlots Called", Preferences.debugging.Value);
            SetFreePlay(isFreePlay);
            betAmount = thisBetAmount;
            UpdateBetLines();
            stopWinAnimation = true;
            spinning = true;
            int[] newSlotsOrder = { num0, num1, num2, num3, num4, num5, num6, num7, num8, num9, num10, num11, num12, num13, num14, num15, num16, num17, num18, num19, num20, num21, num22, num23, num24, num25, num26, num27, num28, num29 };
            MelonCoroutines.Start(RandomizeSlots(newSlotsOrder));
        }

        private IEnumerator RandomizeSlots(int[] newSlotsOrder)
        {
            Log("RandomizeSlots(int[]) Started", Preferences.debugging.Value);
            slotMachineTitleText.GetComponent<TextMeshPro>().text = "Randomizing";
            Transform screen = instance.transform.GetChild(0).GetChild(3);
            float movementPerTick = 0.04f / 75f;
            float currentRotation = screen.localRotation.eulerAngles.z;
            Log("RandomizeSlots(int[]) - Rotating Cover and moving wheel backwards", Preferences.debugging.Value);
            for (int i = 0; i < 75; i++)
            {
                rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, rotatingWheels.transform.localPosition.z - movementPerTick);
                if (i >= 25)
                {
                    currentRotation += 3.6f;
                    screen.transform.localRotation = Quaternion.Euler(0, 90, currentRotation);
                }
                yield return new WaitForFixedUpdate();
            }
            rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, -0.023f);
            screen.transform.localRotation = Quaternion.Euler(0, 90, 90);
            Log("RandomizeSlots(int[]) - Setting Slots to Passed Data", Preferences.debugging.Value);
            for (int i = 0; i < 3; i++)
            {
                slotsOrder[i] = new int[10];
                for (int j = 0; j < 10; j++)
                {
                    slotsOrder[i][j] = newSlotsOrder[(10 * i) + j];
                    Log($"Slot {i}: {slotsOrder[i][j]}", Preferences.debugging.Value);
                }
                for (int j = 0; j < 10; j++)
                {
                    rotatingWheels.transform.GetChild(i).GetChild(0).GetChild(slotsOrder[i][j]).localRotation = Quaternion.Euler(0, 0, 270f - (j * 36));
                }
                Log($"Finished Slot {i}: {SlotsSpotTypesString[slotsOrder[i][0]]} {SlotsSpotTypesString[slotsOrder[i][1]]} {SlotsSpotTypesString[slotsOrder[i][2]]} {SlotsSpotTypesString[slotsOrder[i][3]]} {SlotsSpotTypesString[slotsOrder[i][4]]} {SlotsSpotTypesString[slotsOrder[i][5]]} {SlotsSpotTypesString[slotsOrder[i][6]]} {SlotsSpotTypesString[slotsOrder[i][7]]} {SlotsSpotTypesString[slotsOrder[i][8]]} {SlotsSpotTypesString[slotsOrder[i][9]]}", Preferences.debugging.Value);
            }
            Log("RandomizeSlots(int[]) - Rotating Cover and moving wheel forwards", Preferences.debugging.Value);
            currentRotation = screen.localRotation.eulerAngles.z;
            for (int i = 0; i < 75; i++)
            {
                if (i < 50)
                {
                    currentRotation += 3.6f;
                    screen.transform.localRotation = Quaternion.Euler(0, 90, currentRotation);
                }
                rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, rotatingWheels.transform.localPosition.z + movementPerTick);
                yield return new WaitForFixedUpdate();
            }
            rotatingWheels.transform.localPosition = new Vector3(-0.543f, 1.115f, 0.017f);
            screen.transform.localRotation = Quaternion.Euler(0, 90, 270);
            SpinWheelsTillStopped();
            Log("RandomizeSlots Completed", Preferences.debugging.Value);
            yield break;
        }

        private void SpinWheelsTillStopped()
        {
            Log("SpinWheelsTillStopped Called", Preferences.debugging.Value);
            MelonCoroutines.Start(StartWheelSpinSound());
            slotMachineTitleText.GetComponent<TextMeshPro>().text = "Spinning Wheels";
            clientSpinningCoroutines[0] = MelonCoroutines.Start(SpinWheelTillStoppedCoroutine(0));
            clientSpinningCoroutines[1] = MelonCoroutines.Start(SpinWheelTillStoppedCoroutine(1));
            clientSpinningCoroutines[2] = MelonCoroutines.Start(SpinWheelTillStoppedCoroutine(2));
        }

        private bool[] clientSpinning = new bool[3];
        private object[] clientSpinningCoroutines = new object[3];
        private IEnumerator SpinWheelTillStoppedCoroutine(int wheel)
        {
            clientSpinning[wheel] = true;
            RevolvingNumber revolvingNumber = rotatingWheels.transform.GetChild(wheel).GetComponent<RevolvingNumber>();
            while (revolvingNumber != null && clientSpinning[wheel])
            {
                int wheelNumber = random.Next(0, 10);
                wheelsNumber[wheel] = wheelNumber;
                revolvingNumber.RotateToNumber(wheelsNumber[wheel]);
                yield return revolvingNumber.rotateCoroutine;
            }
            yield break;
        }

        public GameObject LoadMenuButton(string title, Vector3 position, Quaternion rotation, Vector3 localScale, bool onButtonTop, Action listener = null)
        {
            Log("Loading Menu Button: " + title, Preferences.debugging.Value);
            GameObject button = (listener != null ? Create.NewButton(listener) : Create.NewButton());
            button.name = title + " Button";
            button.transform.SetParent(activeObjects.transform);
            button.transform.localPosition = new Vector3(position.x, position.y, position.z);
            button.transform.localRotation = rotation;
            button.transform.localScale = localScale;
            LoadText(button, title, onButtonTop);
            Log("Done Loading Menu Button: " + title, Preferences.debugging.Value);
            return button;
        }

        public GameObject SpawnText(Transform parent, string title, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Log("Loading Text: " + title, Preferences.debugging.Value);
            GameObject text = Create.NewText();
            text.name = title + " Text";
            text.transform.SetParent(parent);
            text.transform.localPosition = position;
            text.transform.localRotation = rotation;
            text.transform.localScale = localScale;
            TextMeshPro textTMP = text.GetComponent<TextMeshPro>();
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.enableWordWrapping = false;
            textTMP.text = title;
            Log("Done Loading Text: " + title, Preferences.debugging.Value);
            return text;
        }

        public void LoadText(GameObject button, string title, bool onButtonTop)
        {
            Log("Loading Menu Button Text: " + title, Preferences.debugging.Value);
            GameObject text = Create.NewText();
            text.name = title + "Text";
            text.transform.SetParent(button.transform);
            text.transform.localPosition = onButtonTop ? new Vector3(0f, 0.01f, -0.22f) : new Vector3(-0.3f, 0f, 0f);
            text.transform.localRotation = Quaternion.Euler(90, 180, 0);
            text.transform.localScale = Vector3.one;
            TextMeshPro textTMP = text.GetComponent<TextMeshPro>();
            textTMP.alignment = TextAlignmentOptions.Center;
            textTMP.enableWordWrapping = false;
            textTMP.text = title;
            Log("Done Loading Menu Button Text: " + title, Preferences.debugging.Value);
        }
    }
}
