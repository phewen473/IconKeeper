// MainForm.cs - the IconKeeper window: list of profiles plus buttons to save, restore and manage them.   // file purpose
// The layout is built in code (no designer file) so everything is in one readable place.                // why no .Designer.cs
using System.Diagnostics;                                                                                  // Process.Start to open the data folder

namespace IconKeeper;                                                                                      // project namespace

internal sealed class MainForm : Form                                                                      // the main window
{                                                                                                          // start of MainForm
    readonly ListView list = new()                                                                         // the table of profiles
    {                                                                                                      // start of initializer
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,                                  // fills the window, shows columns, whole-row selection
        MultiSelect = false, HideSelection = false, GridLines = true,                                      // one at a time, keep highlight when focus moves, grid lines
    };                                                                                                     // end of initializer
    readonly CheckBox runAtLogin = new() { Text = "Restore the default profile when I log in to Windows", AutoSize = true }; // login option
    readonly CheckBox scale = new() { Text = "Stretch positions to fit if the screen size has changed", AutoSize = true };   // scaling option
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.Fixed3D }; // status line
    readonly List<Button> needSelection = new();                                                           // buttons that only work with a profile selected
    bool loading;                                                                                          // true while we fill controls (so events do not save)

    public MainForm()                                                                                      // builds the window
    {                                                                                                      // start of constructor
        Text = $"IconKeeper {Program.VersionName} - Desktop Icon Layouts";                                 // title bar
        AutoScaleDimensions = new SizeF(96F, 96F);                                                         // sizes below are written for 96 DPI (100%)
        AutoScaleMode = AutoScaleMode.Dpi;                                                                 // scale them up on high-DPI screens
        ClientSize = new Size(820, 420);                                                                   // starting size
        MinimumSize = new Size(640, 340);                                                                  // do not let it get too small
        StartPosition = FormStartPosition.CenterScreen;                                                    // open in the middle of the screen
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }                   // use the exe's icon if there is one

        list.Columns.Add("Profile", 240);                                                                  // column: profile name
        list.Columns.Add("Default", 70);                                                                   // column: Yes if restored at login
        list.Columns.Add("Icons", 60, HorizontalAlignment.Right);                                          // column: number of icons saved
        list.Columns.Add("Screen", 110);                                                                   // column: screen size when saved
        list.Columns.Add("Saved", 150);                                                                    // column: when it was saved
        list.SelectedIndexChanged += (_, _) => UpdateButtons();                                            // enable/disable buttons as selection changes
        list.DoubleClick += (_, _) => RestoreSelected();                                                   // double-click a profile = restore it

        var buttons = new FlowLayoutPanel                                                                  // column of buttons on the right
        { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, Width = 200, Padding = new Padding(6), WrapContents = false }; // layout settings
        buttons.Controls.Add(MakeButton("Save current layout as...", SaveNew, false));                     // new profile from the desktop
        buttons.Controls.Add(MakeButton("Update selected", UpdateSelected, true));                         // overwrite a profile with the desktop
        buttons.Controls.Add(MakeButton("Restore selected", RestoreSelected, true));                       // move icons back
        buttons.Controls.Add(MakeButton("Make selected the default", SetDefault, true));                   // choose the login profile
        buttons.Controls.Add(MakeButton("Rename...", RenameSelected, true));                               // rename a profile
        buttons.Controls.Add(MakeButton("Delete", DeleteSelected, true));                                  // delete a profile
        buttons.Controls.Add(MakeButton("Open data folder", OpenFolder, false));                           // show %APPDATA%\IconKeeper in Explorer
        buttons.Controls.Add(MakeButton("Refresh", ReloadList, false));                                    // re-read the profiles

        var options = new FlowLayoutPanel                                                                  // the two check boxes under the list
        { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(6) }; // layout settings
        options.Controls.Add(runAtLogin);                                                                  // login check box
        options.Controls.Add(scale);                                                                       // scaling check box
        runAtLogin.CheckedChanged += (_, _) => RunAtLoginChanged();                                        // save the login choice when clicked
        scale.CheckedChanged += (_, _) => ScaleChanged();                                                  // save the scaling choice when clicked

        Controls.Add(list);                                                                                // added first = laid out last (fills what is left)
        Controls.Add(buttons);                                                                             // right edge
        Controls.Add(options);                                                                             // above the status line
        Controls.Add(status);                                                                              // added last = very bottom edge

        loading = true;                                                                                    // do not react to our own check box changes
        var settings = ProfileStore.LoadSettings();                                                        // current settings
        scale.Checked = settings.ScaleWhenScreenChanges;                                                   // show the scaling choice
        runAtLogin.Checked = ProfileStore.IsRunAtLogin();                                                  // show whether the login entry exists
        if (runAtLogin.Checked) ProfileStore.SetRunAtLogin(true);                                          // rewrite it in case the exe has moved since
        loading = false;                                                                                   // back to normal
        ReloadList();                                                                                      // fill the table
    }                                                                                                      // end of constructor

    /// <summary>Makes one of the right-hand buttons.</summary>                                            // doc comment
    Button MakeButton(string text, Action action, bool requiresSelection)                                  // text, what it does, needs a selection?
    {                                                                                                      // start of MakeButton
        var b = new Button { Text = text, Width = 184, Height = 32, Margin = new Padding(0, 0, 0, 6) };    // same size for every button
        b.Click += (_, _) => Guard(action);                                                                // run the action with error handling
        if (requiresSelection) needSelection.Add(b);                                                       // remember it for enabling/disabling
        return b;                                                                                          // give it to the caller
    }                                                                                                      // end of MakeButton

    /// <summary>Runs an action and shows any error in a message box instead of crashing.</summary>       // doc comment
    void Guard(Action action)                                                                              // wrapper for button clicks
    {                                                                                                      // start of Guard
        try { Cursor = Cursors.WaitCursor; action(); }                                                     // hourglass while working, then do it
        catch (Exception ex)                                                                               // something failed
        {                                                                                                  // start of catch
            Log.Write("Error: " + ex);                                                                     // full details in the log
            MessageBox.Show(this, ex.Message, "IconKeeper", MessageBoxButtons.OK, MessageBoxIcon.Error);   // short message on screen
        }                                                                                                  // end of catch
        finally { Cursor = Cursors.Default; }                                                              // normal pointer again
    }                                                                                                      // end of Guard

    /// <summary>Re-reads all profiles into the table.</summary>                                           // doc comment
    void ReloadList()                                                                                      // called after every change
    {                                                                                                      // start of ReloadList
        string? keep = Selected()?.Name;                                                                   // remember what was selected
        var def = ProfileStore.LoadSettings().DefaultProfile;                                              // which one is the default
        list.BeginUpdate();                                                                                // no flicker while filling
        list.Items.Clear();                                                                                // empty the table
        foreach (var p in ProfileStore.LoadAll())                                                          // every profile on disk
        {                                                                                                  // start of loop body
            bool isDefault = p.Name.Equals(def, StringComparison.OrdinalIgnoreCase);                       // is this the default?
            var row = new ListViewItem(p.Name) { Tag = p };                                                // first column + keep the profile object
            row.SubItems.Add(isDefault ? "Yes" : "");                                                      // Default column
            row.SubItems.Add(p.Icons.Count.ToString());                                                    // Icons column
            row.SubItems.Add(p.ScreenWidth > 0 ? $"{p.ScreenWidth} x {p.ScreenHeight}" : "?");             // Screen column
            row.SubItems.Add(p.SavedAt.ToString("yyyy-MM-dd HH:mm"));                                      // Saved column
            if (isDefault) row.Font = new Font(list.Font, FontStyle.Bold);                                 // default shown in bold
            list.Items.Add(row);                                                                           // add the row
            if (keep != null && p.Name.Equals(keep, StringComparison.OrdinalIgnoreCase)) row.Selected = true; // restore the selection
        }                                                                                                  // end of loop body
        list.EndUpdate();                                                                                  // draw it
        var screen = ProfileStore.CurrentScreen();                                                         // for the status line
        SetStatus($"{list.Items.Count} profile(s). Default: {(def.Length > 0 ? def : "(none)")}. Screen now {screen.Width} x {screen.Height}."); // summary
        UpdateButtons();                                                                                   // enable/disable buttons
    }                                                                                                      // end of ReloadList

    /// <summary>The selected profile, or null.</summary>                                                  // doc comment
    Profile? Selected() => list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as Profile : null;    // read from the row's Tag

    /// <summary>Enables the selection-only buttons when a row is selected.</summary>                     // doc comment
    void UpdateButtons()                                                                                   // called when the selection changes
    {                                                                                                      // start of UpdateButtons
        bool on = Selected() != null;                                                                      // anything selected?
        foreach (var b in needSelection) b.Enabled = on;                                                   // turn the buttons on or off
    }                                                                                                      // end of UpdateButtons

    /// <summary>Writes a message on the status line.</summary>                                            // doc comment
    void SetStatus(string text) => status.Text = "  " + text;                                              // small indent looks tidier

    /// <summary>Button: save the current desktop as a new profile.</summary>                              // doc comment
    void SaveNew()                                                                                         // "Save current layout as..."
    {                                                                                                      // start of SaveNew
        string? name = NamePrompt.Ask(this, "Save layout", "Name for this layout:", $"Layout {DateTime.Now:yyyy-MM-dd}"); // ask for a name
        if (name == null) return;                                                                          // cancelled
        if (ProfileStore.Exists(name) &&                                                                   // already a profile by that name?
            MessageBox.Show(this, $"A profile named '{name}' already exists. Replace it?", "IconKeeper",    // ask before overwriting
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;            // anything but Yes = stop
        var p = ProfileStore.Capture(name);                                                                // read the desktop
        ProfileStore.Save(p);                                                                              // write it
        var s = ProfileStore.LoadSettings();                                                               // settings
        if (s.DefaultProfile.Length == 0) { s.DefaultProfile = name; ProfileStore.SaveSettings(s); }       // first profile becomes the default
        Log.Write($"Saved '{name}' ({p.Icons.Count} icons).");                                             // log it
        ReloadList();                                                                                      // refresh the table
        SelectByName(name);                                                                                // highlight the new one
        SetStatus($"Saved {p.Icons.Count} icon positions as '{name}'.");                                   // report
    }                                                                                                      // end of SaveNew

    /// <summary>Button: overwrite the selected profile with the current desktop.</summary>                // doc comment
    void UpdateSelected()                                                                                  // "Update selected"
    {                                                                                                      // start of UpdateSelected
        var sel = Selected(); if (sel == null) return;                                                     // need a selection
        if (MessageBox.Show(this, $"Replace '{sel.Name}' with the icon positions on the desktop now?", "IconKeeper", // confirm
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;                // anything but Yes = stop
        var p = ProfileStore.Capture(sel.Name);                                                            // read the desktop under the same name
        ProfileStore.Save(p);                                                                              // overwrite
        Log.Write($"Updated '{sel.Name}' ({p.Icons.Count} icons).");                                       // log it
        ReloadList();                                                                                      // refresh
        SetStatus($"Updated '{sel.Name}' with {p.Icons.Count} icon positions.");                           // report
    }                                                                                                      // end of UpdateSelected

    /// <summary>Button: move the desktop icons back to the selected profile.</summary>                    // doc comment
    void RestoreSelected()                                                                                 // "Restore selected" / double-click
    {                                                                                                      // start of RestoreSelected
        var sel = Selected(); if (sel == null) return;                                                     // need a selection
        var fresh = ProfileStore.Load(sel.Name) ?? sel;                                                    // re-read in case the file changed
        var r = ProfileStore.Restore(fresh, scale.Checked);                                                // move the icons
        SetStatus($"Restored '{fresh.Name}': {r}.");                                                       // report
    }                                                                                                      // end of RestoreSelected

    /// <summary>Button: make the selected profile the one restored at login.</summary>                    // doc comment
    void SetDefault()                                                                                      // "Make selected the default"
    {                                                                                                      // start of SetDefault
        var sel = Selected(); if (sel == null) return;                                                     // need a selection
        var s = ProfileStore.LoadSettings();                                                               // settings
        s.DefaultProfile = sel.Name;                                                                       // new default
        ProfileStore.SaveSettings(s);                                                                      // save
        ReloadList();                                                                                      // refresh (bold row moves)
        if (!runAtLogin.Checked) SetStatus($"'{sel.Name}' is now the default. Tick the login box to restore it automatically."); // hint
    }                                                                                                      // end of SetDefault

    /// <summary>Button: rename the selected profile.</summary>                                            // doc comment
    void RenameSelected()                                                                                  // "Rename..."
    {                                                                                                      // start of RenameSelected
        var sel = Selected(); if (sel == null) return;                                                     // need a selection
        string? name = NamePrompt.Ask(this, "Rename layout", "New name:", sel.Name);                       // ask for the new name
        if (name == null || name == sel.Name) return;                                                      // cancelled or unchanged
        if (ProfileStore.Exists(name) && !name.Equals(sel.Name, StringComparison.OrdinalIgnoreCase))       // clashes with another profile?
        { MessageBox.Show(this, $"There is already a profile named '{name}'.", "IconKeeper"); return; }    // refuse
        ProfileStore.Rename(sel.Name, name);                                                               // do it (keeps default status)
        Log.Write($"Renamed '{sel.Name}' to '{name}'.");                                                   // log it
        ReloadList();                                                                                      // refresh
        SelectByName(name);                                                                                // keep it highlighted
    }                                                                                                      // end of RenameSelected

    /// <summary>Button: delete the selected profile.</summary>                                            // doc comment
    void DeleteSelected()                                                                                  // "Delete"
    {                                                                                                      // start of DeleteSelected
        var sel = Selected(); if (sel == null) return;                                                     // need a selection
        if (MessageBox.Show(this, $"Delete profile '{sel.Name}'? This cannot be undone.", "IconKeeper",    // confirm
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;                 // anything but Yes = stop
        ProfileStore.Delete(sel.Name);                                                                     // remove it (clears default if needed)
        Log.Write($"Deleted '{sel.Name}'.");                                                               // log it
        ReloadList();                                                                                      // refresh
    }                                                                                                      // end of DeleteSelected

    /// <summary>Button: open %APPDATA%\IconKeeper in Explorer.</summary>                                  // doc comment
    void OpenFolder()                                                                                      // "Open data folder"
    {                                                                                                      // start of OpenFolder
        Directory.CreateDirectory(ProfileStore.DataFolder);                                                // make sure it exists
        Process.Start("explorer.exe", ProfileStore.DataFolder);                                            // show it
    }                                                                                                      // end of OpenFolder

    /// <summary>Selects the row with this profile name.</summary>                                         // doc comment
    void SelectByName(string name)                                                                         // used after save/rename
    {                                                                                                      // start of SelectByName
        foreach (ListViewItem row in list.Items)                                                           // every row
            row.Selected = row.Text.Equals(name, StringComparison.OrdinalIgnoreCase);                      // select only the match
    }                                                                                                      // end of SelectByName

    /// <summary>Login check box clicked: add or remove the Run entry.</summary>                           // doc comment
    void RunAtLoginChanged()                                                                               // event handler
    {                                                                                                      // start of RunAtLoginChanged
        if (loading) return;                                                                               // ignore while filling the form
        Guard(() =>                                                                                        // with error handling
        {                                                                                                  // start of action
            ProfileStore.SetRunAtLogin(runAtLogin.Checked);                                                // write/remove the registry entry
            bool noDefault = ProfileStore.LoadSettings().DefaultProfile.Length == 0;                       // is there a default to restore?
            SetStatus(runAtLogin.Checked                                                                   // pick the message
                ? (noDefault ? "Login restore is on, but no default profile is chosen yet." : "The default profile will be restored each time you log in.") // on
                : "Login restore is off.");                                                                // off
        });                                                                                                // end of action
    }                                                                                                      // end of RunAtLoginChanged

    /// <summary>Scaling check box clicked: save the setting.</summary>                                    // doc comment
    void ScaleChanged()                                                                                    // event handler
    {                                                                                                      // start of ScaleChanged
        if (loading) return;                                                                               // ignore while filling the form
        var s = ProfileStore.LoadSettings();                                                               // settings
        s.ScaleWhenScreenChanges = scale.Checked;                                                          // new value
        ProfileStore.SaveSettings(s);                                                                      // save (also used by the login restore)
    }                                                                                                      // end of ScaleChanged
}                                                                                                          // end of MainForm

/// <summary>Small "type a name" dialog.</summary>                                                         // doc comment
internal static class NamePrompt                                                                           // helper
{                                                                                                          // start of NamePrompt
    /// <summary>Shows the dialog; returns the trimmed text, or null if cancelled/empty.</summary>        // doc comment
    public static string? Ask(IWin32Window owner, string title, string label, string value)                // owner window, captions, starting text
    {                                                                                                      // start of Ask
        using var f = new Form                                                                             // the dialog window
        {                                                                                                  // start of initializer
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, // fixed size, centred
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(360, 104), // no min/max, not in taskbar
            AutoScaleDimensions = new SizeF(96F, 96F), AutoScaleMode = AutoScaleMode.Dpi,                  // scale for high-DPI screens
        };                                                                                                 // end of initializer
        var caption = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };                    // the prompt text
        var box = new TextBox { Left = 12, Top = 34, Width = 336, Text = value };                          // where the name is typed
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 192, Top = 68, Width = 75 }; // OK button
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 273, Top = 68, Width = 75 }; // Cancel button
        f.Controls.AddRange(new Control[] { caption, box, ok, cancel });                                   // put them on the dialog
        f.AcceptButton = ok; f.CancelButton = cancel;                                                      // Enter = OK, Esc = Cancel
        box.SelectAll();                                                                                   // starting text is selected so typing replaces it
        if (f.ShowDialog(owner) != DialogResult.OK) return null;                                           // cancelled
        string text = box.Text.Trim();                                                                     // tidy the name
        return text.Length > 0 ? text : null;                                                              // empty counts as cancelled
    }                                                                                                      // end of Ask
}                                                                                                          // end of NamePrompt
