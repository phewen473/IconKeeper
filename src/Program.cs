// Program.cs - entry point. No arguments = open the window. With arguments = do one job and exit.       // file purpose
//   IconKeeper.exe /save <name>        save the current desktop as profile <name>                       // command 1
//   IconKeeper.exe /restore <name>     put the icons back from profile <name>                            // command 2
//   IconKeeper.exe /restore-default    wait for the desktop, then restore the default profile (login)    // command 3
//   IconKeeper.exe /list               list the saved profiles                                           // command 4
using System.Runtime.InteropServices;                                                                      // AttachConsole

namespace IconKeeper;                                                                                      // project namespace

internal static class Program                                                                              // startup class
{                                                                                                          // start of Program
    public const string VersionName = "V1R2";                                                              // shown in the title bar; matches the versions\ folder

    [DllImport("kernel32.dll")]                                                                            // Windows API
    static extern bool AttachConsole(int processId);                                                       // lets this GUI exe print to the Command Prompt that started it

    [STAThread]                                                                                            // COM (Explorer) and WinForms both need a single-threaded apartment
    static int Main(string[] args)                                                                         // program starts here; returns an exit code
    {                                                                                                      // start of Main
        ApplicationConfiguration.Initialize();                                                             // DPI awareness and visual styles (from the .csproj)
        if (args.Length == 0)                                                                              // no arguments: interactive window
        {                                                                                                  // start of if
            using var mutex = new Mutex(true, "IconKeeper-window-" + Environment.UserName, out bool first); // allow only one window per user
            if (!first)                                                                                    // another copy already has the window open
            {                                                                                              // start of if
                MessageBox.Show("IconKeeper is already open.", "IconKeeper", MessageBoxButtons.OK, MessageBoxIcon.Information); // say so
                return 0;                                                                                  // and quit
            }                                                                                              // end of if
            Application.Run(new MainForm());                                                               // show the window until it is closed
            return 0;                                                                                      // normal exit
        }                                                                                                  // end of if
        AttachConsole(-1);                                                                                 // -1 = the parent console, so Console.WriteLine shows up
        try { return RunCommand(args); }                                                                   // do the requested job
        catch (Exception ex)                                                                               // anything went wrong
        {                                                                                                  // start of catch
            Say("Error: " + ex.Message);                                                                   // tell the user
            Log.Write("Error running '" + string.Join(" ", args) + "': " + ex);                            // and log full details
            return 1;                                                                                      // exit code 1 = failed
        }                                                                                                  // end of catch
    }                                                                                                      // end of Main

    /// <summary>Handles one command-line command. Returns the exit code.</summary>                        // doc comment
    static int RunCommand(string[] args)                                                                   // args[0] is the command
    {                                                                                                      // start of RunCommand
        string command = args[0].TrimStart('/', '-').ToLowerInvariant();                                   // accept /save, -save or --save
        string name = string.Join(" ", args.Skip(1)).Trim().Trim('"');                                     // the rest is the profile name (spaces allowed)
        var settings = ProfileStore.LoadSettings();                                                        // needed for scaling and the default
        switch (command)                                                                                   // pick the job
        {                                                                                                  // start of switch
            case "save":                                                                                   // save the current layout
                if (name.Length == 0) { Say("Usage: IconKeeper /save <profile name>"); return 1; }        // a name is required
                var captured = ProfileStore.Capture(name);                                                 // read the desktop
                ProfileStore.Save(captured);                                                               // write the profile
                if (settings.DefaultProfile.Length == 0)                                                   // first profile ever saved?
                { settings.DefaultProfile = name; ProfileStore.SaveSettings(settings); }                   // make it the default
                Say($"Saved {captured.Icons.Count} icon positions to profile '{name}'.");                  // report
                Log.Write($"Saved '{name}' from the command line ({captured.Icons.Count} icons).");        // log it
                return 0;                                                                                  // success
            case "restore":                                                                                // restore a named profile
                if (name.Length == 0) { Say("Usage: IconKeeper /restore <profile name>"); return 1; }     // a name is required
                var profile = ProfileStore.Load(name);                                                     // read it
                if (profile == null) { Say($"No profile named '{name}'."); return 2; }                     // exit code 2 = not found
                Say("Restored: " + ProfileStore.Restore(profile, settings.ScaleWhenScreenChanges));        // move the icons and report
                return 0;                                                                                  // success
            case "restore-default":                                                                        // what the login entry runs
                return RestoreDefaultAtLogin(settings);                                                    // separate method below
            case "list":                                                                                   // list profiles
                foreach (var p in ProfileStore.LoadAll())                                                  // every saved profile
                    Say($"{(p.Name.Equals(settings.DefaultProfile, StringComparison.OrdinalIgnoreCase) ? "*" : " ")} {p.Name}  ({p.Icons.Count} icons, {p.ScreenWidth}x{p.ScreenHeight}, saved {p.SavedAt:g})"); // * marks the default
                return 0;                                                                                  // success
            default:                                                                                       // unknown command or /?
                Say("IconKeeper " + VersionName + " - saves and restores desktop icon positions");         // title line
                Say("  IconKeeper                      open the window");                                  // help line
                Say("  IconKeeper /save <name>         save current layout as <name>");                    // help line
                Say("  IconKeeper /restore <name>      restore layout <name>");                            // help line
                Say("  IconKeeper /restore-default     restore the default profile (used at login)");      // help line
                Say("  IconKeeper /list                list profiles (* = default)");                      // help line
                return command is "?" or "help" or "h" ? 0 : 1;                                            // asking for help is not an error
        }                                                                                                  // end of switch
    }                                                                                                      // end of RunCommand

    /// <summary>Login mode: wait for Explorer's desktop to finish loading, then restore the default.</summary> // doc comment
    static int RestoreDefaultAtLogin(Settings settings)                                                    // runs silently from the registry Run key
    {                                                                                                      // start of RestoreDefaultAtLogin
        if (settings.DefaultProfile.Length == 0) { Log.Write("Login: no default profile set, nothing to do."); return 0; } // nothing chosen yet
        var profile = ProfileStore.Load(settings.DefaultProfile);                                          // read the default profile
        if (profile == null) { Log.Write($"Login: default profile '{settings.DefaultProfile}' is missing."); return 2; } // file gone
        Log.Write($"Login: waiting for the desktop before restoring '{profile.Name}'...");                  // note the start
        if (!DesktopIcons.WaitUntilReady(TimeSpan.FromMinutes(2)))                                         // give Explorer up to 2 minutes
        { Log.Write("Login: desktop never became ready; gave up."); return 3; }                            // exit code 3 = timed out
        ProfileStore.Restore(profile, settings.ScaleWhenScreenChanges);                                    // move the icons (Restore logs the result)
        return 0;                                                                                          // success
    }                                                                                                      // end of RestoreDefaultAtLogin

    /// <summary>Prints a line to the console (only visible when started from a Command Prompt).</summary> // doc comment
    static void Say(string text) => Console.WriteLine(text);                                               // simple wrapper
}                                                                                                          // end of Program
