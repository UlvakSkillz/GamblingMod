using MelonLoader;
using MelonLoader.Preferences;

namespace GamblingMod
{
	public class Preferences
	{
		private const string CONFIG_FILE = "config.cfg";
		private const string USER_DATA = "UserData/GamblingMod/";
        internal static Dictionary<MelonPreferences_Entry, object> LastSavedValues = new();

        internal static MelonPreferences_Category GamblingModCategory;
		internal static MelonPreferences_Entry<int> deckCount;
		internal static MelonPreferences_Entry<bool> tableEnabled;
		internal static MelonPreferences_Entry<bool> slotsEnabled;
		internal static MelonPreferences_Entry<int> tableSeed;
		internal static MelonPreferences_Entry<int> slotsSeed;
		internal static MelonPreferences_Entry<bool> useSeed;
		internal static MelonPreferences_Entry<int> volume;
		internal static MelonPreferences_Entry<bool> showHandCount;
		internal static MelonPreferences_Entry<bool> debugging;

        internal static void InitPrefs()
		{
			if (!Directory.Exists(USER_DATA)) { Directory.CreateDirectory(USER_DATA); }

            //General settings
            GamblingModCategory = MelonPreferences.CreateCategory("GamblingMod", "Settings");
            GamblingModCategory.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            deckCount = GamblingModCategory.CreateEntry("deckCount", 1, "Deck Count", $"Sets How Many Complete Decks should be in the Dealer Deck.", validator: new ValueRange<int>(0, int.MaxValue));
            tableEnabled = GamblingModCategory.CreateEntry("tableEnabled", true, "Table Enabled", "Enables the Poker Table.vv");
            slotsEnabled = GamblingModCategory.CreateEntry("slotsEnabled", true, "Slots Enabled", "Enables the Slot Machine.");
            tableSeed = GamblingModCategory.CreateEntry("tableSeed", 0, "Table Seed", $"Sets the Seed in the Randomizer if 'Use Seed' is Toggled On.", validator: new ValueRange<int>(0, int.MaxValue));
            slotsSeed = GamblingModCategory.CreateEntry("slotsSeed", 0, "Slots Seed", $"Sets the Seed in the Randomizer if 'Use Seed' is Toggled On.", validator: new ValueRange<int>(0, int.MaxValue));
            useSeed = GamblingModCategory.CreateEntry("useSeed", false, "Use Seed", "If Enabled, Sets the Table to FreePlay Mode and Uses the Seed.");
            volume = GamblingModCategory.CreateEntry("volume", 100, "Volume", $"Sets the Volume of Sounds. 0 - 100", validator: new ValueRange<int>(0, 100));
            showHandCount = GamblingModCategory.CreateEntry("showHandCount", true, "Show Hand Count", "BlackJack: If Enabled, Shows the Hand Counts.");
            debugging = GamblingModCategory.CreateEntry("debugging", true, "Debugging", "Enables Debugging Logs");

            tableSeed.ResetToDefault(); //Ignore saved setting to emulate ModUI DoNotSave tag;
            slotsSeed.ResetToDefault(); //Ignore saved setting to emulate ModUI DoNotSave tag;
            useSeed.ResetToDefault(); //Ignore saved setting to emulate ModUI DoNotSave tag;
            StoreLastSavedPrefs();
		}

		internal static void StoreLastSavedPrefs()
		{
			List<MelonPreferences_Entry> prefs = new();
			prefs.AddRange(GamblingModCategory.Entries);

			foreach (MelonPreferences_Entry entry in  prefs) { LastSavedValues[entry] = entry.BoxedValue; }
		}

		public static bool AnyPrefsChanged()
		{
			foreach (KeyValuePair<MelonPreferences_Entry, object> pair in LastSavedValues)
			{
				if (!pair.Key.BoxedValue.Equals(pair.Value)) { return true; }
			}
			return false;
		}

		public static bool IsPrefChanged(MelonPreferences_Entry entry)
		{
			if (LastSavedValues.TryGetValue(entry, out object? lastValue)) { return !entry.BoxedValue.Equals(lastValue); }
			return false;
		}
	}
}