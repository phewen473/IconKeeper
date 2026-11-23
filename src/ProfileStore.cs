// ProfileStore.cs - saves/loads icon-layout profiles, the settings file, and the "run at login" entry.   // file purpose
// Everything lives in %APPDATA%\IconKeeper:  profiles\<name>.json, settings.json, iconkeeper.log          // where data is kept
using System.Text.Json;                                                                                    // JSON reading/writing
using Microsoft.Win32;                                                                                     // registry access for the login entry

namespace IconKeeper;                                                                                      // project namespace

/// <summary>A saved desktop layout.</summary>                                                             // doc comment
public sealed class Profile                                                                                // one JSON file per profile
{                                                                                                          // start of Profile
    public string Name { get; set; } = "";                                                                 // profile name as the user typed it
    public DateTime SavedAt { get; set; }                                                                  // when the layout was captured
    public int ScreenWidth { get; set; }                                                                   // total desktop width when saved (all monitors)
    public int ScreenHeight { get; set; }                                                                  // total desktop height when saved (all monitors)
    public List<IconPosition> Icons { get; set; } = new();                                                 // every icon and where it was
}                                                                                                          // end of Profile

/// <summary>Program settings kept between runs.</summary>                                                 // doc comment
public sealed class Settings                                                                               // stored in settings.json
{                                                                                                          // start of Settings
    public string DefaultProfile { get; set; } = "";                                                       // profile restored at login ("" = none)
    public bool ScaleWhenScreenChanges { get; set; } = true;                                               // stretch/shrink positions if the screen size differs
    public string AcceptedLicense { get; set; } = "";                                                      // license version the user agreed to ("" = not yet)
}                                                                                                          // end of Settings

/// <summary>File and registry handling for profiles and settings.</summary>                              // doc comment
public static class ProfileStore                                                                           // static helper, no state
{                                                                                                          // start of ProfileStore
    public static readonly string DataFolder =                                                             // %APPDATA%\IconKeeper
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IconKeeper");  // built from the roaming AppData folder
    public static readonly string ProfileFolder = Path.Combine(DataFolder, "profiles");                    // where the profile .json files go
    static readonly string SettingsFile = Path.Combine(DataFolder, "settings.json");                       // settings file path
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };                           // pretty JSON so it can be read in Notepad
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";                                // per-user "start at login" registry key
    const string RunValue = "IconKeeper";                                                                  // our value name under that key

    /// <summary>File path for a profile name (characters Windows forbids become _).</summary>            // doc comment
    static string FileFor(string name)                                                                     // maps name -> file
    {                                                                                                      // start of FileFor
        var bad = Path.GetInvalidFileNameChars();                                                          // characters not allowed in file names
        var safe = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray());                      // replace them with underscores
        return Path.Combine(ProfileFolder, safe + ".json");                                                // full path of the profile file
    }                                                                                                      // end of FileFor

    /// <summary>All profiles, sorted by name.</summary>                                                   // doc comment
    public static List<Profile> LoadAll()                                                                  // used to fill the list on screen
    {                                                                                                      // start of LoadAll
        Directory.CreateDirectory(ProfileFolder);                                                          // make sure the folder exists
        var list = new List<Profile>();                                                                    // results
        foreach (var file in Directory.GetFiles(ProfileFolder, "*.json"))                                  // every profile file
        {                                                                                                  // start of loop body
            try { var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Json); if (p != null) list.Add(p); } // read it
            catch (Exception ex) { Log.Write($"Could not read {file}: {ex.Message}"); }                    // a damaged file is logged and skipped
        }                                                                                                  // end of loop body
        return list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();                // alphabetical
    }                                                                                                      // end of LoadAll

    /// <summary>Loads one profile, or null if there is none by that name.</summary>                      // doc comment
    public static Profile? Load(string name)                                                               // name lookup is case-insensitive (Windows files)
    {                                                                                                      // start of Load
        var file = FileFor(name);                                                                          // where it would be
        if (!File.Exists(file)) return null;                                                               // not found
        return JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Json);                          // read and return it
    }                                                                                                      // end of Load

    /// <summary>True if a profile with this name exists.</summary>                                       // doc comment
    public static bool Exists(string name) => File.Exists(FileFor(name));                                  // just checks for the file

    /// <summary>Writes a profile to disk (safely: temp file, then replace).</summary>                     // doc comment
    public static void Save(Profile profile)                                                               // creates or overwrites
    {                                                                                                      // start of Save
        Directory.CreateDirectory(ProfileFolder);                                                          // make sure the folder exists
        var file = FileFor(profile.Name);                                                                  // final file name
        var temp = file + ".tmp";                                                                          // write here first so a crash cannot leave half a file
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, Json));                                  // write the JSON
        File.Move(temp, file, true);                                                                       // swap it into place
    }                                                                                                      // end of Save

    /// <summary>Deletes a profile; clears the default if it was the default.</summary>                    // doc comment
    public static void Delete(string name)                                                                 // removes the file
    {                                                                                                      // start of Delete
        File.Delete(FileFor(name));                                                                        // remove it from disk
        var s = LoadSettings();                                                                            // read settings
        if (string.Equals(s.DefaultProfile, name, StringComparison.OrdinalIgnoreCase))                     // was it the default?
        { s.DefaultProfile = ""; SaveSettings(s); }                                                        // then there is no default any more
    }                                                                                                      // end of Delete

    /// <summary>Renames a profile and keeps it as default if it was.</summary>                            // doc comment
    public static void Rename(string oldName, string newName)                                              // old -> new
    {                                                                                                      // start of Rename
        var p = Load(oldName) ?? throw new FileNotFoundException($"Profile '{oldName}' not found.");       // read the old one
        p.Name = newName;                                                                                  // give it the new name
        Save(p);                                                                                           // write under the new file name
        if (!string.Equals(FileFor(oldName), FileFor(newName), StringComparison.OrdinalIgnoreCase))        // only if the file name really changed
            File.Delete(FileFor(oldName));                                                                 // remove the old file
        var s = LoadSettings();                                                                            // read settings
        if (string.Equals(s.DefaultProfile, oldName, StringComparison.OrdinalIgnoreCase))                  // was the old name the default?
        { s.DefaultProfile = newName; SaveSettings(s); }                                                   // point the default at the new name
    }                                                                                                      // end of Rename

    /// <summary>Reads settings.json (defaults if missing or damaged).</summary>                           // doc comment
    public static Settings LoadSettings()                                                                  // always returns something usable
    {                                                                                                      // start of LoadSettings
        try                                                                                                // file may not exist yet
        {                                                                                                  // start of try
            if (File.Exists(SettingsFile))                                                                 // only read if present
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsFile), Json) ?? new(); // parse it
        }                                                                                                  // end of try
        catch (Exception ex) { Log.Write("settings.json unreadable, using defaults: " + ex.Message); }     // damaged file: log and carry on
        return new Settings();                                                                             // defaults
    }                                                                                                      // end of LoadSettings

    /// <summary>Writes settings.json.</summary>                                                           // doc comment
    public static void SaveSettings(Settings s)                                                            // overwrites the file
    {                                                                                                      // start of SaveSettings
        Directory.CreateDirectory(DataFolder);                                                             // make sure the folder exists
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, Json));                                // write it
    }                                                                                                      // end of SaveSettings

    /// <summary>Total size of the desktop right now (all monitors together).</summary>                    // doc comment
    public static Size CurrentScreen() => SystemInformation.VirtualScreen.Size;                            // physical pixels (the app is DPI aware)

    /// <summary>Captures the current desktop into a new profile object (not yet saved).</summary>        // doc comment
    public static Profile Capture(string name)                                                             // name for the new profile
    {                                                                                                      // start of Capture
        var screen = CurrentScreen();                                                                      // remember the screen size it was taken at
        return new Profile                                                                                 // build the profile
        {                                                                                                  // start of initializer
            Name = name,                                                                                   // its name
            SavedAt = DateTime.Now,                                                                        // time stamp
            ScreenWidth = screen.Width,                                                                    // desktop width
            ScreenHeight = screen.Height,                                                                  // desktop height
            Icons = DesktopIcons.Read(),                                                                   // every icon's position
        };                                                                                                 // end of initializer
    }                                                                                                      // end of Capture

    /// <summary>True if the profile was saved at a different screen size than now.</summary>             // doc comment
    public static bool ScreenDiffers(Profile p)                                                            // used to decide on scaling
    {                                                                                                      // start of ScreenDiffers
        var now = CurrentScreen();                                                                         // current size
        return p.ScreenWidth > 0 && p.ScreenHeight > 0                                                     // only if the profile recorded a size
            && (p.ScreenWidth != now.Width || p.ScreenHeight != now.Height);                               // and it is different
    }                                                                                                      // end of ScreenDiffers

    /// <summary>Puts the desktop icons back where the profile says.</summary>                             // doc comment
    public static RestoreResult Restore(Profile p, bool scaleIfScreenChanged)                              // scale = stretch to the new screen size
    {                                                                                                      // start of Restore
        double sx = 1, sy = 1;                                                                             // no scaling by default
        if (scaleIfScreenChanged && ScreenDiffers(p))                                                      // screen changed and scaling allowed
        {                                                                                                  // start of if
            var now = CurrentScreen();                                                                     // current size
            sx = (double)now.Width / p.ScreenWidth;                                                        // horizontal stretch factor
            sy = (double)now.Height / p.ScreenHeight;                                                      // vertical stretch factor
        }                                                                                                  // end of if
        var result = DesktopIcons.Apply(p.Icons, sx, sy);                                                  // move the icons
        Log.Write($"Restored '{p.Name}' (scale {sx:0.###} x {sy:0.###}): {result}");                       // keep a record
        return result;                                                                                     // tell the caller
    }                                                                                                      // end of Restore

    /// <summary>True if Windows is set to run IconKeeper at login.</summary>                              // doc comment
    public static bool IsRunAtLogin()                                                                      // reads HKCU\...\Run
    {                                                                                                      // start of IsRunAtLogin
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);                                           // open the Run key read-only
        return key?.GetValue(RunValue) != null;                                                            // present = on
    }                                                                                                      // end of IsRunAtLogin

    /// <summary>Turns the login restore on or off (on = points at this exe with /restore-default).</summary> // doc comment
    public static void SetRunAtLogin(bool on)                                                              // writes HKCU\...\Run
    {                                                                                                      // start of SetRunAtLogin
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);                                         // open (or create) the Run key for writing
        if (on) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\" /restore-default");                 // run this exe at login in restore mode
        else key.DeleteValue(RunValue, false);                                                             // remove the entry (no error if already gone)
    }                                                                                                      // end of SetRunAtLogin
}                                                                                                          // end of ProfileStore

/// <summary>Tiny text log in %APPDATA%\IconKeeper\iconkeeper.log (mainly for the silent login restore).</summary> // doc comment
public static class Log                                                                                    // static logger
{                                                                                                          // start of Log
    static readonly string LogFile = Path.Combine(ProfileStore.DataFolder, "iconkeeper.log");              // log file path

    /// <summary>Appends one time-stamped line. Never throws.</summary>                                    // doc comment
    public static void Write(string message)                                                               // the only method
    {                                                                                                      // start of Write
        try                                                                                                // logging must never crash the program
        {                                                                                                  // start of try
            Directory.CreateDirectory(ProfileStore.DataFolder);                                            // make sure the folder exists
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 1_000_000)                          // keep the log under about 1 MB
                File.Move(LogFile, LogFile + ".old", true);                                                // roll it over to iconkeeper.log.old
            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}"); // add the line
        }                                                                                                  // end of try
        catch { }                                                                                          // ignore logging failures on purpose
    }                                                                                                      // end of Write
}                                                                                                          // end of Log
