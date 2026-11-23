// AboutAndLicense.cs - the About box, the License Agreement window, and the first-run "accept the license" check. // file purpose
using System.Diagnostics;                                                                                  // Process.Start for the data-folder link
using System.Reflection;                                                                                   // reading the exe's version number

namespace IconKeeper;                                                                                      // project namespace

/// <summary>Things packed inside the exe: the icon and the license text.</summary>                       // doc comment
internal static class AppResources                                                                         // shared by every window
{                                                                                                          // start of AppResources
    /// <summary>Loads the checkmark icon at the size asked for.</summary>                                 // doc comment
    public static Icon LoadIcon(Size size)                                                                 // size = which image in app.ico to use
    {                                                                                                      // start of LoadIcon
        using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("IconKeeper.app.ico");  // the embedded app.ico
        return stream != null ? new Icon(stream, size) : SystemIcons.Application;                          // fall back to the plain Windows icon if missing
    }                                                                                                      // end of LoadIcon

    /// <summary>The full license agreement text (LICENSE.txt, embedded at build time).</summary>          // doc comment
    public static string LicenseText()                                                                     // returns the whole text
    {                                                                                                      // start of LicenseText
        using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("IconKeeper.LICENSE.txt"); // the embedded LICENSE.txt
        if (stream == null) return "License text missing from this build.";                                // should never happen
        using var reader = new StreamReader(stream);                                                       // read it as text
        return reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");                             // Windows line endings for the text box
    }                                                                                                      // end of LicenseText

    /// <summary>The help text (docs\HELP.md, embedded at build time).</summary>                           // doc comment
    public static string HelpText()                                                                        // returns the whole text
    {                                                                                                      // start of HelpText
        using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("IconKeeper.HELP.md");  // the embedded HELP.md
        if (stream == null) return "Help text missing from this build.";                                   // should never happen
        using var reader = new StreamReader(stream);                                                       // read it as text
        return reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");                             // Windows line endings for the text box
    }                                                                                                      // end of HelpText

    /// <summary>The exe's file version, e.g. 1.3.0.</summary>                                             // doc comment
    public static string FileVersion() =>                                                                  // read from the assembly
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";                             // major.minor.build
}                                                                                                          // end of AppResources

/// <summary>Checks and records that the user has accepted the license.</summary>                         // doc comment
internal static class LicenseGate                                                                          // used at startup
{                                                                                                          // start of LicenseGate
    public const string LicenseVersion = "1.0";                                                            // bump this when LICENSE.txt changes, to ask again

    /// <summary>True if this version of the license has been accepted.</summary>                          // doc comment
    public static bool IsAccepted() => ProfileStore.LoadSettings().AcceptedLicense == LicenseVersion;      // compared with settings.json

    /// <summary>Shows the license to accept if needed. False = the user declined.</summary>               // doc comment
    public static bool AskIfNeeded()                                                                       // called before the main window opens
    {                                                                                                      // start of AskIfNeeded
        if (IsAccepted()) return true;                                                                     // already agreed: nothing to do
        using var form = new LicenseForm(acceptMode: true);                                                // license window with I Agree / I Do Not Agree
        if (form.ShowDialog() != DialogResult.OK) { Log.Write("License declined; exiting."); return false; } // declined (or closed): stop
        var s = ProfileStore.LoadSettings();                                                               // settings
        s.AcceptedLicense = LicenseVersion;                                                                // remember the acceptance
        ProfileStore.SaveSettings(s);                                                                      // save it
        Log.Write($"License version {LicenseVersion} accepted.");                                          // log it
        return true;                                                                                       // carry on
    }                                                                                                      // end of AskIfNeeded
}                                                                                                          // end of LicenseGate

/// <summary>Window showing the license text. Accept mode has I Agree / I Do Not Agree; view mode has Close.</summary> // doc comment
internal sealed class LicenseForm : Form                                                                   // license window
{                                                                                                          // start of LicenseForm
    public LicenseForm(bool acceptMode)                                                                    // acceptMode = first-run agreement
    {                                                                                                      // start of constructor
        Text = "IconKeeper - License Agreement";                                                           // title bar
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;                      // scale for high-DPI screens
        ClientSize = new Size(640, 520); MinimumSize = new Size(480, 360);                                 // size, and smallest allowed
        StartPosition = acceptMode ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;      // centred
        MinimizeBox = false; ShowInTaskbar = acceptMode;                                                   // no minimize; on the taskbar only at first run
        Icon = AppResources.LoadIcon(new Size(32, 32));                                                    // checkmark icon

        var intro = new Label                                                                              // line above the text
        {                                                                                                  // start of initializer
            Dock = DockStyle.Top, Height = 34, Padding = new Padding(8, 8, 8, 0),                          // across the top
            Text = acceptMode ? "Please read the license agreement. You must agree to it to use IconKeeper." // first-run wording
                              : "IconKeeper license agreement.",                                           // view-only wording
        };                                                                                                 // end of initializer
        var text = new TextBox                                                                             // the license itself
        {                                                                                                  // start of initializer
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,     // scrollable, not editable
            WordWrap = true, Font = new Font("Consolas", 9.5F), BackColor = SystemColors.Window,           // fixed-width font keeps the layout
            Text = AppResources.LicenseText(),                                                             // load the text
        };                                                                                                 // end of initializer
        var bar = new FlowLayoutPanel                                                                      // button row at the bottom
        { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) }; // buttons line up on the right
        if (acceptMode)                                                                                    // first run
        {                                                                                                  // start of if
            var no = new Button { Text = "I Do Not Agree", Width = 120, Height = 28, DialogResult = DialogResult.Cancel }; // decline = exit
            var yes = new Button { Text = "I Agree", Width = 100, Height = 28, DialogResult = DialogResult.OK }; // accept = carry on
            bar.Controls.Add(no); bar.Controls.Add(yes);                                                   // right-to-left: "I Agree" ends up left of "I Do Not Agree"
            CancelButton = no;                                                                             // Esc = decline (Enter does nothing, on purpose)
        }                                                                                                  // end of if
        else                                                                                               // just viewing
        {                                                                                                  // start of else
            var close = new Button { Text = "Close", Width = 100, Height = 28, DialogResult = DialogResult.OK }; // only button
            bar.Controls.Add(close); AcceptButton = close; CancelButton = close;                           // Enter or Esc closes
        }                                                                                                  // end of else
        Controls.Add(text); Controls.Add(intro); Controls.Add(bar);                                        // fill first, edges after (edges dock first)
        Shown += (_, _) => { text.SelectionLength = 0; text.SelectionStart = 0; };                         // start at the top, nothing highlighted
    }                                                                                                      // end of constructor
}                                                                                                          // end of LicenseForm

/// <summary>Help window: shows docs\HELP.md in a scrollable, read-only box.</summary>                    // doc comment
internal sealed class HelpForm : Form                                                                      // help window (not modal, can stay open)
{                                                                                                          // start of HelpForm
    public HelpForm()                                                                                      // builds it
    {                                                                                                      // start of constructor
        Text = "IconKeeper Help";                                                                          // title bar
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;                      // scale for high-DPI screens
        ClientSize = new Size(720, 600); MinimumSize = new Size(480, 360);                                 // size, and smallest allowed
        StartPosition = FormStartPosition.CenterScreen;                                                    // centred
        Icon = AppResources.LoadIcon(new Size(32, 32));                                                    // checkmark icon
        var text = new TextBox                                                                             // the help text
        {                                                                                                  // start of initializer
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,     // scrollable, not editable
            WordWrap = true, Font = new Font("Consolas", 10F), BackColor = SystemColors.Window,            // fixed-width font keeps tables lined up
            Text = AppResources.HelpText(),                                                                // load it
        };                                                                                                 // end of initializer
        var bar = new FlowLayoutPanel                                                                      // button row
        { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) }; // right-aligned
        var close = new Button { Text = "Close", Width = 100, Height = 28 };                               // close button
        close.Click += (_, _) => Close();                                                                  // closes the window
        bar.Controls.Add(close); CancelButton = close;                                                     // Esc closes too
        Controls.Add(text); Controls.Add(bar);                                                             // fill first, edge after
        Shown += (_, _) => { text.SelectionLength = 0; text.SelectionStart = 0; };                         // start at the top
    }                                                                                                      // end of constructor
}                                                                                                          // end of HelpForm

/// <summary>The About box.</summary>                                                                      // doc comment
internal sealed class AboutForm : Form                                                                     // About window
{                                                                                                          // start of AboutForm
    public AboutForm()                                                                                     // builds it
    {                                                                                                      // start of constructor
        Text = "About IconKeeper";                                                                         // title bar
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;                      // scale for high-DPI screens
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;           // fixed-size dialog
        ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;                             // centred on the main window
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;                                        // the box grows to fit its contents
        Padding = new Padding(16);                                                                         // space around the edges
        Icon = AppResources.LoadIcon(new Size(32, 32));                                                    // checkmark icon

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 2 }; // icon | text, then buttons
        var picture = new PictureBox { Width = 64, Height = 64, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 4, 16, 0) }; // big icon on the left
        picture.Image = AppResources.LoadIcon(new Size(64, 64)).ToBitmap();                                // the 64 px checkmark
        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; // text lines stacked top to bottom
        Label Line(string s, Font? f = null, int gapAfter = 2) =>                                          // helper: one wrapped label
            new Label { AutoSize = true, MaximumSize = new Size(400, 0), Text = s, Font = f ?? Font, Margin = new Padding(0, 0, 0, gapAfter) }; // wraps at 400 px
        text.Controls.Add(Line("IconKeeper", new Font(Font.FontFamily, 16F, FontStyle.Bold), 6));           // program name
        text.Controls.Add(Line($"Version {Program.VersionName}  (file version {AppResources.FileVersion()})")); // version line
        text.Controls.Add(Line("Saves and restores Windows desktop icon layouts.", null, 12));            // what it is
        text.Controls.Add(Line("Copyright © 2026 P. H. Ewen (KO4WSC). All rights reserved."));         // copyright line
        text.Controls.Add(Line("Freeware. Use is subject to the License Agreement.", null, 12));          // license line
        var folder = new LinkLabel { AutoSize = true, MaximumSize = new Size(400, 0), Text = "Data folder: " + ProfileStore.DataFolder }; // where profiles live
        folder.LinkArea = new LinkArea(13, ProfileStore.DataFolder.Length);                                // only the path is clickable
        folder.LinkClicked += (_, _) => { Directory.CreateDirectory(ProfileStore.DataFolder); Process.Start("explorer.exe", ProfileStore.DataFolder); }; // open it
        text.Controls.Add(folder);                                                                         // add the link
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) }; // button row, right-aligned
        var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(90, 30), DialogResult = DialogResult.OK }; // close the box
        var license = new Button { Text = "License Agreement...", AutoSize = true, MinimumSize = new Size(150, 30) }; // view the license
        license.Click += (_, _) => { using var f = new LicenseForm(acceptMode: false); f.ShowDialog(this); }; // open it read-only
        buttons.Controls.Add(ok); buttons.Controls.Add(license);                                           // right-to-left: OK ends up on the far right
        grid.Controls.Add(picture, 0, 0); grid.Controls.Add(text, 1, 0);                                   // first row: icon and text
        grid.Controls.Add(buttons, 0, 1); grid.SetColumnSpan(buttons, 2);                                  // second row: buttons across both columns
        Controls.Add(grid);                                                                                // put it all on the form
        AcceptButton = ok; CancelButton = ok;                                                              // Enter or Esc closes
    }                                                                                                      // end of constructor
}                                                                                                          // end of AboutForm
