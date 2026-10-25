using Microsoft.Maui.Storage;

namespace GroupAnnouncementApp.Helpers;

// Phase 3: when THIS phone's member last pressed the panic button on an announcement, kept on the phone.
// The card needs it to show the 5-minute wait. Before, every Home card read the press documents from the server
// each time it was built; now that is done once per announcement (and "never pressed" is remembered too).
// The server still checks the wait when the button is pressed, so a second phone cannot skip it.
public static class PanicMemory
{
    private const string Key = "panic.memory.v1";
    private const int MaxEntries = 120;

    private static readonly object Gate = new object();
    private static Dictionary<string, long>? _entries;

    // Returns true when this phone knows the answer. lastPressMs 0 = known, never pressed.
    public static bool TryGet(string uid, string groupId, string announcementId, out long lastPressMs)
    {
        lock (Gate)
        {
            Load();
            return _entries!.TryGetValue(EntryKey(uid, groupId, announcementId), out lastPressMs);
        }
    }

    public static void Set(string uid, string groupId, string announcementId, long lastPressMs)
    {
        lock (Gate)
        {
            Load();
            _entries![EntryKey(uid, groupId, announcementId)] = lastPressMs;
            Trim();
            Save();
        }
    }

    private static string EntryKey(string uid, string groupId, string announcementId)
    {
        return uid + "|" + groupId + "|" + announcementId;
    }

    private static void Load()
    {
        if (_entries != null)
        {
            return;
        }

        _entries = new Dictionary<string, long>();
        try
        {
            string text = Preferences.Default.Get(Key, string.Empty);
            string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                int cut = lines[i].LastIndexOf('=');
                if (cut > 0 && long.TryParse(lines[i].Substring(cut + 1), out long ms))
                {
                    _entries[lines[i].Substring(0, cut)] = ms;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC MEMORY LOAD ERROR: " + ex);
        }
    }

    // Keeps the newest presses; entries that never pressed (0) go first.
    private static void Trim()
    {
        if (_entries!.Count <= MaxEntries)
        {
            return;
        }

        List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>(_entries);
        rows.Sort((a, b) => a.Value.CompareTo(b.Value));
        int remove = rows.Count - MaxEntries;
        for (int i = 0; i < remove; i++)
        {
            _entries.Remove(rows[i].Key);
        }
    }

    private static void Save()
    {
        try
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, long> pair in _entries!)
            {
                text.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            }

            Preferences.Default.Set(Key, text.ToString());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC MEMORY SAVE ERROR: " + ex);
        }
    }
}
