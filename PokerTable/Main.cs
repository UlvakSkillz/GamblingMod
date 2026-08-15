using HarmonyLib;
using Il2CppPhoton.Pun;
using Il2CppRUMBLE.Audio;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Economy.Interactables;
using Il2CppRUMBLE.Environment.Howard;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Poses;
using Il2CppTMPro;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using System.Security.Cryptography;
using UIFramework;
using UnityEngine;

namespace GamblingMod
{
    public static class BuildInfo
    {
        public const string ModName = "GamblingMod";
        public const string ModVersion = "2.3.1";
        public const string Author = "UlvakSkillz";
    }

    [HarmonyPatch(typeof(PlayerPoseSystem), "OnPoseSetCompleted", new Type[] { typeof(PoseSet) })]
    public static class PosePatch
    {
        private static void Postfix(PoseSet set)
        {
            Main.posesCompleted++;
            if (Main.posesCompleted >= 100)
            {
                Main.posesCompleted -= 100;
                Main.Payout(1, 1f);
                Main.Log("Player Earned 1 Coin from Completing 100 Poses", true);
            }
        }
    }

    public class Main : MelonMod
    {
        private static MelonLogger.Instance logger;
        public static MelonMod melonMod;
        private static int playerCoins = -1;
        public static int posesCompleted = -1;
        private static bool flatLandFound = false, voidLandFound = false;
        public static List<Table> tableList = new List<Table>();
        public static List<SlotMachine> slotsList = new List<SlotMachine>();
        public GameObject rumbleTextObject;
        public static string currentScene = "Loader";
        private static GameObject storedTable, storedSlots;
        private bool finishedGymSetup;
        private Material spinnerMaterial;
        private static Shader URPLit;

        public static void Log(string msg, bool sendMsg = true)
        {
            if (sendMsg) { logger.Msg(msg); }
        }

        public static void Warn(string msg, bool sendMsg = true)
        {
            if (sendMsg) { logger.Warning(msg); }
        }

        public static void Error(string msg) { logger.Error(msg); }

        public override void OnInitializeMelon()
        {
            Preferences.InitPrefs();
            UI.RegisterMelon(this, Preferences.GamblingModCategory).OnModSaved += Save;
        }

        public override void OnLateInitializeMelon()
        {
            URPLit = Shader.Find("Universal Render Pipeline/Lit");
            logger = LoggerInstance;
            melonMod = this;
            Lighting.UseBounceLighting(true);
            Lighting.SetHorizonLightColor(new Color(0.5f, 0.5f, 0.55f));
            Lighting.SetGroundLightColor(new Color(0.35f, 0.35f, 0.42f));
            Log("OnLateInitializeMelon Started", Preferences.debugging.Value);
            LoadSaveFile();
            LoadAssetBundle();
            MelonCoroutines.Start(GrabRumbleTextObject());
            Log("OnLateInitializeMelon Completed", Preferences.debugging.Value);
            Actions.onMapInitialized += MapLoaded;
        }

        private void LoadSaveFile()
        {
            Log("LoadSaveFile Started", Preferences.debugging.Value);
            if (!Directory.Exists(@"UserData\" + BuildInfo.ModName))
            {
                Directory.CreateDirectory(@"UserData\" + BuildInfo.ModName);
            }
            if (!File.Exists(@"UserData\" + BuildInfo.ModName + @"\Save.save"))
            {
                EncryptStringToFile("0|0", @"UserData\" + BuildInfo.ModName + @"\Save.save", "UlvakSkillz");
            }
            try
            {
                string decodedText = DecryptStringFromFile(@"UserData\" + BuildInfo.ModName + @"\Save.save", "UlvakSkillz");
                Log("Save.save File Text: " + decodedText, Preferences.debugging.Value);
                string[] fileText = decodedText.Split("|");
                playerCoins = int.Parse(fileText[0]);
                Log("Loaded Player Coins: " + playerCoins, true);
                posesCompleted = int.Parse(fileText[1]);
                Log("Loaded Poses Completed: " + posesCompleted, true);
            }
            catch (Exception e)
            {
                Error("Error Loading Save File:");
                Error(e.Message);
                if (playerCoins == -1) { Log("Failed to Load Player Coins, Setting to 0", true); playerCoins = 0; }
                if (posesCompleted == -1) { Log("Failed to Load Poses Completed Count, Setting to 0", true); posesCompleted = 0; }
            }
            Log("LoadSaveFile Completed", Preferences.debugging.Value);
        }

        private void LoadAssetBundle()
        {
            Log("LoadAssetBundle Started", Preferences.debugging.Value);
            GameObject materialGO = GameObject.Instantiate(AssetBundles.LoadAssetFromStream<GameObject>(this, BuildInfo.ModName + ".gambling", "Spinner"));
            spinnerMaterial = (materialGO.GetComponent<MeshRenderer>().material);
            GameObject.DontDestroyOnLoad(materialGO);
            materialGO.name = "Gambling Mod Matierial Storage";
            materialGO.SetActive(false);
            GameObject bundle = GameObject.Instantiate(AssetBundles.LoadAssetFromStream<GameObject>(this, BuildInfo.ModName + ".gambling", "Poker"));
            storedTable = bundle.transform.GetChild(0).gameObject;
            storedSlots = bundle.transform.GetChild(1).gameObject;
            storedTable.name = "PokerTable";
            storedSlots.name = "SlotMachine";
            storedTable.transform.SetParent(null);
            storedSlots.transform.SetParent(null);
            storedTable.SetActive(false);
            storedSlots.SetActive(false);
            GameObject.DontDestroyOnLoad(storedTable);
            GameObject.DontDestroyOnLoad(storedSlots);
            finishedGymSetup = false;
            GameObject.Destroy(bundle);
            Log("LoadAssetBundle Completed", Preferences.debugging.Value);
        }

        private static void ChangeShaderLitToUnlit(GameObject asset)
        {
            UnityEngine.Renderer parentRendderer = asset.GetComponent<Renderer>();
            if (parentRendderer != null)
            {
                for (int i = 0; i < parentRendderer.materials.Length; i++)
                {
                    if (parentRendderer.materials[i].shader.name == "Universal Render Pipeline/Lit")
                    {
                        parentRendderer.materials[i].shader = URPLit;
                    }
                }
            }
            for (int i = 0; i < asset.transform.GetChildCount(); i++)
            {
                ChangeShaderLitToUnlit(asset.transform.GetChild(i).gameObject);
            }
        }

        private void Save()
        {
            if (Preferences.IsPrefChanged(Preferences.volume))
            {
                SetAudioLevels(Preferences.volume.Value);
            }
            if (Preferences.IsPrefChanged(Preferences.useSeed))
            {
                foreach (Table table in tableList)
                {
                    if (Preferences.useSeed.Value)
                    {
                        if (table.freePlayButton != null) { GameObject.Destroy(table.freePlayButton); }
                        table.freePlay = true;
                    }
                    if (table.blackJackInstance != null) { MelonCoroutines.Stop(table.blackJackInstance); }
                    if (table.jacksOrBetterInstance != null) { MelonCoroutines.Stop(table.jacksOrBetterInstance); }
                    table.ClearActiveObjects();
                    table.SetupRandom();
                    table.SetupStart();
                }
                foreach (SlotMachine slots in slotsList)
                {
                    if (Preferences.useSeed.Value)
                    {
                        slots.SetFreePlay(true);
                    }
                    slots.SetupRandom();
                }
            }
            Preferences.StoreLastSavedPrefs();
        }

        private void SetAudioLevels(int volumeLevel)
        {
            Log($"SetAudioLevels({volumeLevel}) Running", Preferences.debugging.Value);
            foreach (AudioCall audioCall in storedAudioCalls)
            {
                audioCall.generalSettings.SetVolume(volumeLevel / 100);
                audioCall.spatialSettings.SpatialBlend = 1f;
            }
        }

        private void MapLoaded(string map)
        {
            currentScene = map;
            if (currentScene == "Loader") { return; }
            if (logger == null)
            {
                logger = LoggerInstance;
            }
            if ((map != "Gym") && (map != "Park")) { return; }
            MelonCoroutines.Start(MapLoadCoroutine());
        }

        private IEnumerator MapLoadCoroutine()
        {
            Log("MapLoadCoroutine Started", Preferences.debugging.Value);
            if ((currentScene == "Gym") && (!finishedGymSetup))
            {
                yield return new WaitForFixedUpdate();
                FinishStoredTableSetup();
                FinishStoredSlotsSetup();
                if (GameObject.Find("/FlatLand/") != null)
                {
                    flatLandFound = true;
                }
                if (GameObject.Find("/VoidLand/") != null)
                {
                    voidLandFound = true;
                }
                SetupAudio();
                finishedGymSetup = true;
            }
            if (currentScene == "Gym")
            {
                yield return new WaitForFixedUpdate();
                if (flatLandFound)
                {
                    //moves active objects when going to FlatLand
                    GameObject.Find("/FlatLand/FlatLandButton/Button").GetComponent<InteractionButton>().onPressed
                        .AddListener(new Action(() =>
                        {
                            MelonCoroutines.Start(ControlOtherLandsTransition());
                        }));
                }
                if (voidLandFound)
                {
                    GameObject.Find("/VoidLand/VoidLandButton/Button").GetComponent<InteractionButton>().onPressed
                        .AddListener(new Action(() =>
                        {
                            MelonCoroutines.Start(ControlOtherLandsTransition());
                        }));
                }
            }
            if (Preferences.tableEnabled.Value)
            {
                LoadTable();
            }
            if (Preferences.slotsEnabled.Value)
            {
                LoadSlots();
            }
            Log("MapLoadCoroutine Completed", Preferences.debugging.Value);
            yield break;
        }

        public static List<AudioCall> storedAudioCalls = new List<AudioCall>();
        private void SetupAudio()
        {
            Log("SetupAudio Started", Preferences.debugging.Value);
            storedAudioCalls.Clear();
            Transform playerController = PlayerManager.instance.localPlayer.Controller.transform;
            Transform visuals = playerController.GetChild(1);
            Transform howard = GameObjects.Gym.INTERACTABLES.Howard.DummyRoot.Howard.GetGameObject().transform;
            Transform progressTracker = GameObjects.Gym.INTERACTABLES.ProgressTracker.GetGameObject().transform;
            Transform gearMarket = GameObjects.Gym.INTERACTABLES.Gearmarket.GetGameObject().transform;
            storedAudioCalls.Add(AudioCall.Instantiate(howard.GetComponent<HowardFX>().movementAudioCall)); //Howard movement (chain sound)  /////// lever return
            storedAudioCalls[0].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(gearMarket.GetComponent<GearMarket>().unlockClaimSFX)); //Gear Market Claim  ///// coin insert sound
            storedAudioCalls[1].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(progressTracker.GetComponent<ProgressTracker>().itemPanelFlipAudioCall)); //progress tracker rotate (4 clips) ////////// wheel spinning?
            storedAudioCalls[2].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(gearMarket.GetComponent<GearMarket>().unlockPurchaseSFX)); //Gear Market Purchase ///// payout sound
            storedAudioCalls[3].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(gearMarket.GetComponent<GearMarket>().alertAudioCall)); //Gear Market Alert //////////big payout?
            storedAudioCalls[4].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(visuals.GetComponent<PlayerScaling>().onMeasureFailedAudioCall)); //measure fail ////////// no payout
            storedAudioCalls[5].hideFlags = HideFlags.HideAndDontSave;
            storedAudioCalls.Add(AudioCall.Instantiate(playerController.GetComponent<PlayerShiftstoneSystem>().onShiftstoneUseSFX)); //shiftstone use (4 internal) //////// x3 in a row stones
            storedAudioCalls[6].hideFlags = HideFlags.HideAndDontSave;
            SetAudioLevels(Preferences.volume.Value);
            Log("SetupAudio Completed", Preferences.debugging.Value);
        }

        private IEnumerator ControlOtherLandsTransition()
        {
            yield return new WaitForSeconds(1f);
            foreach (Table loadedTable in tableList)
            {
                loadedTable.gameObject.SetActive(true);
                loadedTable.transform.position = new Vector3(loadedTable.transform.position.x, 0, loadedTable.transform.position.z);
            }
            foreach (SlotMachine loadedSlot in slotsList)
            {
                loadedSlot.gameObject.SetActive(true);
                loadedSlot.transform.position = new Vector3(loadedSlot.transform.position.x, 0, loadedSlot.transform.position.z);
            }
            Log("ControlOtherLandsTransition Completed", Preferences.debugging.Value);
            yield break;
        }

        private IEnumerator GrabRumbleTextObject()
        {
            Log("GrabRumbleTextObject Started", Preferences.debugging.Value);
            GameObject originalObject = null;
            while (originalObject == null)
            {
                originalObject = GameObject.Find("/SCENE/Text");
                yield return new WaitForFixedUpdate();
            }
            rumbleTextObject = new GameObject("RumbleText");
            GameObject newText = GameObject.Instantiate(originalObject.transform.GetChild(4).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, -5.3182f);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            newText = GameObject.Instantiate(originalObject.transform.GetChild(5).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, -2.7164f);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            newText = GameObject.Instantiate(originalObject.transform.GetChild(3).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, 0);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            newText = GameObject.Instantiate(originalObject.transform.GetChild(0).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, 2.7673f);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            newText = GameObject.Instantiate(originalObject.transform.GetChild(2).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, 4.6371f);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            newText = GameObject.Instantiate(originalObject.transform.GetChild(1).gameObject);
            newText.transform.SetParent(rumbleTextObject.transform);
            newText.transform.localPosition = new Vector3(0, 0, 6.605f);
            newText.transform.localRotation = Quaternion.identity;
            newText.transform.localScale = originalObject.transform.localScale;
            rumbleTextObject.transform.localScale = new Vector3(0.0001f, 0.001f, 0.001f);
            rumbleTextObject.SetActive(false);
            GameObject.DontDestroyOnLoad(rumbleTextObject);
            Log("Grabbed RUMBLE GameObject for Later", Preferences.debugging.Value);
            yield break;
        }

        private void FinishStoredSlotsSetup()
        {
            Log("FinishStoredSlotsSetup Started", Preferences.debugging.Value);
            //setup stored Slots Wheels
            //duplicate Revolving Numbers Collection
            GameObject radialDials = GameObject.Instantiate(GameObjects.Gym.INTERACTABLES.RegionSelector.Model.Pin.Ping.RevolvingNumberCollection.GetGameObject());
            radialDials.name = "RadialDials";
            radialDials.transform.SetParent(storedSlots.transform);
            radialDials.transform.localPosition = new Vector3(-0.543f, 1.115f, 0.017f);
            radialDials.transform.localRotation = Quaternion.Euler(270f, 180f, 0f);
            radialDials.transform.localScale = new Vector3(10f, 10f, 10f);

            //move left and right wheels into place
            radialDials.transform.GetChild(0).localPosition = new Vector3(-0.086f, 0f, 0f);
            radialDials.transform.GetChild(2).localPosition = new Vector3(-0.04f, 0f, 0f);

            //change material so numbers arent shown.
            radialDials.transform.GetChild(0).GetComponent<MeshRenderer>().material = spinnerMaterial;
            radialDials.transform.GetChild(1).GetComponent<MeshRenderer>().material = spinnerMaterial;
            radialDials.transform.GetChild(2).GetComponent<MeshRenderer>().material = spinnerMaterial;

            Transform newSpinner = new GameObject("Spinner Spot Parent 0").transform;
            newSpinner.SetParent(radialDials.transform.GetChild(0));
            newSpinner.localPosition = Vector3.zero;
            newSpinner.localRotation = Quaternion.Euler(0f, 90f, 0);
            newSpinner.localScale = Vector3.one;
            SetupStoredSlotsSpinner(newSpinner);

            newSpinner = new GameObject("Spinner Spot Parent 1").transform;
            newSpinner.SetParent(radialDials.transform.GetChild(1));
            newSpinner.localPosition = Vector3.zero;
            newSpinner.localRotation = Quaternion.Euler(0f, 90f, 0);
            newSpinner.localScale = Vector3.one;
            SetupStoredSlotsSpinner(newSpinner);

            newSpinner = new GameObject("Spinner Spot Parent 2").transform;
            newSpinner.SetParent(radialDials.transform.GetChild(2));
            newSpinner.localPosition = Vector3.zero;
            newSpinner.localRotation = Quaternion.Euler(0f, 90f, 0);
            newSpinner.localScale = Vector3.one;
            SetupStoredSlotsSpinner(newSpinner);

            ChangeShaderLitToUnlit(storedSlots);

            Log("FinishStoredSlotsSetup Completed", Preferences.debugging.Value);
        }

        private void FinishStoredTableSetup()
        {
            Log("FinishStoredTableSetup Started", Preferences.debugging.Value);

            ChangeShaderLitToUnlit(storedTable);
            storedTable.transform.GetChild(0).GetChild(0).GetComponent<Renderer>().material.color = new Color(0.0969f, 0.9072f, 0.4112f);
            storedTable.transform.GetChild(0).GetChild(2).localPosition = new Vector3(0f, -0.01f, 0f);
            storedTable.transform.GetChild(0).GetChild(3).localPosition = new Vector3(0f, -0.01f, 0f);
            storedTable.transform.GetChild(0).GetChild(14).localPosition = new Vector3(0f, -0.01f, 0f);
            storedTable.transform.GetChild(0).GetChild(1).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(2).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(3).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(4).GetComponent<Renderer>().material.color = new Color(0.0969f, 0.9072f, 0.4112f);
            storedTable.transform.GetChild(0).GetChild(5).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(6).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(7).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(8).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(9).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(10).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(11).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(12).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(13).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);
            storedTable.transform.GetChild(0).GetChild(14).GetComponent<Renderer>().material.color = new Color(0.2785f, 0.4784f, 0.4823f);

            Log("FinishStoredTableSetup Completed", Preferences.debugging.Value);
        }

        public GameObject SpawnText(Transform parent, string title, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            GameObject text = Create.NewText();
            text.name = title + " Text";
            text.transform.SetParent(parent);
            text.transform.localPosition = position;
            text.transform.localRotation = rotation;
            text.transform.localScale = localScale;
            TextMeshPro tmp = text.GetComponent<TextMeshPro>();
            tmp.text = title;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            return text;
        }

        private void SetupStoredSlotsSpinner(Transform spinner)
        {
            Log("SetupStoredSlotsSpinner Started", Preferences.debugging.Value);
            Vector3[] scales = {
                new Vector3(0.0001f, 0.001f, -0.001f),
                new Vector3(0.25f, 0f, 0.25f),
                new Vector3(0.25f, 0f, 0.25f),
                new Vector3(0.18f, 0f, 0.18f),
                new Vector3(0.25f, 0f, 0.25f),
                new Vector3(0.2f, 0f, 0.2f),
                new Vector3(0.2f, 0f, 0.2f),
                new Vector3(0.2f, 0f, 0.2f),
                new Vector3(0.2f, 0f, 0.2f),
                new Vector3(0.009f, 0.006f, 0.001f) };
            Quaternion[] rotations = {
                Quaternion.Euler(0f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(-90f, 90f, 0f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 0f, 270f),
                Quaternion.Euler(0f, 270f, 0f) };
            Vector3?[] positions = {
                new Vector3(-0.021f, 0f, 0.0005f),
                new Vector3(-0.021f, 0.006f, 0f),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new Vector3(-0.021f, -0.005f, 0f) };
            //output these calls below to check if valid
            GameObject[] slotObjectOriginals = {
                rumbleTextObject,
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox.AdamantStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox_.ChargeStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox__.FlowStone.Gem101.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox___.GuardStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox____.StubbornStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox_____.SurgeStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox______.VigorStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Shiftstones.ShiftstoneCabinet.Cabinet.ShiftstoneBox_______.VolatileStone.Mesh.GetGameObject(),
                GameObjects.Gym.INTERACTABLES.Howard.DummyRoot.Howard.GetGameObject() };
            for (int i = 0; i < 10; i++)
            {
                //to point it towards the wheel edge correctly
                GameObject objectSpot = new GameObject("SpinnerSpot" + i);
                objectSpot.transform.SetParent(spinner);
                objectSpot.transform.localPosition = Vector3.zero;
                objectSpot.transform.localRotation = Quaternion.Euler(0, 0, 270f - (i * 36));
                objectSpot.transform.localScale = Vector3.one;
                //the actual object. position is placed in here so it reflects uniformly
                GameObject slotObject = GameObject.Instantiate(slotObjectOriginals[i]);
                slotObject.transform.SetParent(objectSpot.transform);
                if (positions[i] != null) { slotObject.transform.localPosition = (Vector3)positions[i]; }
                else { slotObject.transform.localPosition = new Vector3(-0.0204f, 0f, 0f); }
                slotObject.transform.localRotation = Quaternion.Euler(rotations[i].eulerAngles.x, rotations[i].eulerAngles.y, rotations[i].eulerAngles.z);
                slotObject.transform.localScale = scales[i];
                if (i == 9)
                {
                    GameObject.DestroyImmediate(slotObject.transform.GetChild(3).gameObject);
                    GameObject.DestroyImmediate(slotObject.transform.GetChild(2).gameObject);
                    Component.DestroyImmediate(slotObject.GetComponent<HowardFX>());
                    Component.DestroyImmediate(slotObject.GetComponent<HowardAnimator>());
                    Component.DestroyImmediate(slotObject.GetComponent<Animator>());
                    //turn off Howard Light
                    slotObject.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetChild(0).gameObject.SetActive(false);
                }
                else if (i == 0) { slotObject.SetActive(true); } //this is needed for the RUMBLE text
            }
            Log("SetupStoredSlotsSpinner Completed", Preferences.debugging.Value);
        }

        private void LoadTable()
        {
            Log("LoadTable Started", Preferences.debugging.Value);
            switch (currentScene)
            {
                case "Gym":
                    GameObject table = GameObject.Instantiate(storedTable);
                    table.name = "PokerTable";
                    table.transform.position = new Vector3(4.1736f, -3.5255f, -10.3336f);
                    table.transform.rotation = Quaternion.Euler(0, 170.9995f, 0);
                    table.SetActive(true);
                    table.gameObject.AddComponent<PhotonView>().ViewID = 8008135;
                    tableList.Add(table.AddComponent<Table>());
                    if (flatLandFound)
                    {
                        GameObject.Find("/FlatLand/FlatLandButton/").transform.GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
                        {
                            foreach (Table loadedTable in tableList)
                            {
                                loadedTable.transform.position = new Vector3(loadedTable.transform.position.x, 0, loadedTable.transform.position.z);
                            }
                        }));
                    }
                    if (voidLandFound)
                    {
                        GameObject.Find("/VoidLand/VoidLandButton/").transform.GetChild(0).GetComponent<InteractionButton>().onPressed.AddListener(new System.Action(() =>
                        {
                            foreach (Table loadedTable in tableList)
                            {
                                loadedTable.transform.position = new Vector3(loadedTable.transform.position.x, 0, loadedTable.transform.position.z);
                            }
                        }));
                    }
                    break;
                case "Park":
                    GameObject table1 = GameObject.Instantiate(storedTable);
                    table1.name = "PokerTable";
                    table1.transform.position = new Vector3(16.5305f, -20.9391f, -20.8574f);
                    table1.transform.rotation = Quaternion.Euler(0, 315.5508f, 0);
                    table1.SetActive(true);
                    table1.gameObject.AddComponent<PhotonView>().ViewID = 8008135;
                    tableList.Add(table1.AddComponent<Table>());
                    break;
                default:
                    return;
            }
            Log("LoadTable Completed", Preferences.debugging.Value);
        }

        private void LoadSlots()
        {
            Log("LoadSlots Started", Preferences.debugging.Value);
            slotsList.Clear();
            if ((currentScene != "Gym") && (currentScene != "Park")) { return; }
            GameObject slots = GameObject.Instantiate(storedSlots);
            switch (currentScene)
            {
                case "Gym":
                    slots.name = "SlotMachine";
                    slots.transform.position = new Vector3(1.7463f, -3.4255f, -11.8445f);
                    slots.transform.rotation = Quaternion.Euler(0, 133.8f, 0);
                    slots.SetActive(true);
                    slots.gameObject.AddComponent<PhotonView>().ViewID = 8008136;
                    slotsList.Add(slots.AddComponent<SlotMachine>());
                    break;
                case "Park":
                    slots.name = "SlotMachine1";
                    slots.transform.position = new Vector3(19.4876f, -20.9472f, -17.587f);
                    slots.transform.rotation = Quaternion.Euler(0, 320.9668f, 0);
                    slots.SetActive(true);
                    int photonID = 8008136;
                    PhotonView photonView = slots.gameObject.AddComponent<PhotonView>();
                    photonView.ViewID = photonID;
                    photonID++;
                    slotsList.Add(slots.AddComponent<SlotMachine>());
                    slots = GameObject.Instantiate(storedSlots);
                    slots.name = "SlotMachine2";
                    slots.transform.position = new Vector3(18.4876f, -20.9472f, -18.387f);
                    slots.transform.rotation = Quaternion.Euler(0, 320.9668f, 0);
                    slots.SetActive(true);
                    photonView = slots.gameObject.AddComponent<PhotonView>();
                    photonView.ViewID = photonID;
                    photonID++;
                    slotsList.Add(slots.AddComponent<SlotMachine>());
                    slots = GameObject.Instantiate(storedSlots);
                    slots.name = "SlotMachine3";
                    slots.transform.position = new Vector3(17.4876f, -20.9472f, -19.187f);
                    slots.transform.rotation = Quaternion.Euler(0, 320.9668f, 0);
                    slots.SetActive(true);
                    photonView = slots.gameObject.AddComponent<PhotonView>();
                    photonView.ViewID = photonID;
                    photonID++;
                    slotsList.Add(slots.AddComponent<SlotMachine>());
                    break;
            }
            Log("LoadSlots Completed", Preferences.debugging.Value);
        }

        public static int GetPlayerCoinCount() { return playerCoins; }

        public static int Payout(int bet, float multiplyer, Table table)
        {
            float betF = bet;
            if (!table.freePlay)
            {
                playerCoins += (int)(betF * multiplyer);
            }
            EncryptStringToFile(playerCoins.ToString() + "|" + posesCompleted, @"UserData\" + BuildInfo.ModName + @"\Save.save", "UlvakSkillz");
            return (int)(betF * multiplyer);
        }

        public static int Payout(int bet, float multiplyer, SlotMachine slotMachine)
        {
            float betF = bet;
            if (!slotMachine.freePlay)
            {
                playerCoins += (int)(betF * multiplyer);
            }
            EncryptStringToFile(playerCoins.ToString() + "|" + posesCompleted, @"UserData\" + BuildInfo.ModName + @"\Save.save", "UlvakSkillz");
            return (int)(betF * multiplyer);
        }

        public static int Payout(int bet, float multiplyer)
        {
            float betF = bet;
            playerCoins += (int)(betF * multiplyer);
            EncryptStringToFile(playerCoins.ToString() + "|" + posesCompleted, @"UserData\" + BuildInfo.ModName + @"\Save.save", "UlvakSkillz");
            return (int)(betF * multiplyer);
        }

        public static void EncryptStringToFile(string plainText, string filePath, string password)
        {
            // Generate a random salt
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using (var aesAlg = Aes.Create())
            {
                aesAlg.KeySize = 256;
                aesAlg.BlockSize = 128;
                aesAlg.Padding = PaddingMode.PKCS7;
                // Derive key and IV from password + salt
                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100000))
                {
                    aesAlg.Key = deriveBytes.GetBytes(32);
                    aesAlg.IV = deriveBytes.GetBytes(16);
                }
                using (FileStream fsOutput = new FileStream(filePath, FileMode.Create))
                {
                    // Write salt and IV to the file header
                    fsOutput.Write(salt, 0, salt.Length);
                    fsOutput.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
                    using (CryptoStream csEncrypt = new CryptoStream(fsOutput, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }
            }
        }

        public static string DecryptStringFromFile(string filePath, string password)
        {
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (FileStream fsInput = new FileStream(filePath, FileMode.Open))
            {
                // Read salt and IV from file
                fsInput.Read(salt, 0, salt.Length);
                fsInput.Read(iv, 0, iv.Length);
                using (var aesAlg = Aes.Create())
                {
                    aesAlg.KeySize = 256;
                    aesAlg.BlockSize = 128;
                    aesAlg.Padding = PaddingMode.PKCS7;
                    // Derive key from password + stored salt
                    using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100000))
                    {
                        aesAlg.Key = deriveBytes.GetBytes(32);
                    }
                    aesAlg.IV = iv;
                    using (ICryptoTransform decryptor = aesAlg.CreateDecryptor())
                    using (CryptoStream csDecrypt = new CryptoStream(fsInput, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        string plainText = srDecrypt.ReadToEnd();
                        return plainText;
                    }
                }
            }
        }
    }
}
