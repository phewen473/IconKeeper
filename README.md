# IconKeeper

IconKeeper saves where your desktop icons are and puts them back later. You can keep as many
saved layouts ("profiles") as you like. One of them can be the **default**, and Windows can
restore it automatically each time you log in.

Current version: **V1R2** (2026-11-22): green checkmark icon, and the window minimizes to the system tray.

## Where things live

| What | Where |
|---|---|
| Project folder | the folder that holds `build.bat` (wherever you put IconKeeper) |
| The program | `IconKeeper.exe` in the project folder |
| Source code | `src\` in the project folder |
| Version snapshots | `versions\IconKeeper-V1R1\`, `...\IconKeeper-V1R2\` (exe + source; older versions are never overwritten) |
| Icon | `src\app.ico`, drawn by `tools\make_icon.ps1` (re-run that script if you change the design) |
| Your saved profiles | `%APPDATA%\IconKeeper\profiles\<name>.json` (plain JSON, readable in Notepad) |
| Settings (default profile, scaling) | `%APPDATA%\IconKeeper\settings.json` |
| Log | `%APPDATA%\IconKeeper\iconkeeper.log` (rolls over at 1 MB) |
| Login entry (only when ticked) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `IconKeeper` = `"...\IconKeeper.exe" /restore-default` |
| Source code online | https://github.com/phewen473/IconKeeper |

## How to use it

Double-click `IconKeeper.exe`. The window lists your profiles. The default profile is shown in bold.

- **Save current layout as...**: saves where every icon is right now under a name you choose.
  The first profile you save becomes the default automatically.
- **Update selected**: overwrites the selected profile with the icon positions on the desktop now.
- **Restore selected** (or double-click a row): moves the icons back to where that profile says.
- **Make selected the default**: picks which profile is restored at login.
- **Rename... / Delete**: manage profiles.
- **Restore the default profile when I log in to Windows**: adds or removes the login entry.
- **Stretch positions to fit if the screen size has changed**: if a profile was saved at a different
  total screen size (for example with a monitor unplugged), positions are scaled to fit. Untick it to
  place icons at the exact saved pixels.

### System tray

Minimizing the window hides it from the taskbar and puts a green checkmark in the system tray
(by the clock; on Windows 11 it may be under the ^ arrow until you drag it out). The first time,
a balloon reminds you it is still running.

- **Left-click** the checkmark: bring the window back.
- **Right-click** it: Open IconKeeper, Restore default, a Restore submenu with every profile
  (default in bold), Save current layout as..., and Exit. A balloon reports each restore.
- Closing the window with the **X** exits the program. Only minimizing goes to the tray.

### Command line

```
IconKeeper /save <name>        save the current layout as <name>
IconKeeper /restore <name>     restore layout <name>
IconKeeper /restore-default    wait for the desktop to load, then restore the default (used at login)
IconKeeper /list               list profiles (* marks the default)
```

Exit codes: 0 = OK, 1 = error, 2 = profile not found, 3 = desktop never became ready (login mode).
Names may contain spaces. Text output appears when it is run from a Command Prompt.

## How it works (plain English)

- Explorer shows the desktop icons in a "folder view". Windows provides an official programming
  interface for that view (`IFolderView2`). IconKeeper asks Explorer for the desktop's view and then
  uses it to read each icon's position (`GetItemPosition`) and to move icons (`SelectAndPositionItems`).
  It does not reach into Explorer's memory the way some older tools do.
- Each icon is matched by its full path (for example `%USERPROFILE%\Desktop\Notes.txt`), or by
  its shell id for special icons like the Recycle Bin. So a renamed or deleted file is reported as
  "in profile but not on desktop", and a new file is left wherever it is.
- If "Auto arrange icons" is on, Windows would put the icons straight back in rows, so IconKeeper
  turns it off when restoring. "Align icons to grid" is left alone, so icons may land a few pixels
  from the saved spot.
- At login, `/restore-default` waits (up to 2 minutes) until Explorer's desktop is up and the icon
  count has stopped changing, then restores. It runs silently and writes what it did to the log.
- Explorer remembers the new positions itself, the same as if you had dragged the icons by hand.

## Building

Needs the .NET SDK (8 or later). Run `build.bat`. It compiles `src\` into a
single `IconKeeper.exe` in this folder. The exe needs the .NET 8 Desktop Runtime, which is installed.

Source files:

- `src\Program.cs`: startup, command-line commands, login restore
- `src\MainForm.cs`: the window, the system tray icon and menu, and the name dialog
- `src\app.ico`: the checkmark icon (on the exe, and embedded so the window and tray load crisp sizes)
- `tools\make_icon.ps1`: draws `app.ico` (green rounded square, white check, sizes 16 to 256)
- `src\ProfileStore.cs`: profile and settings files, the login registry entry, the log
- `src\DesktopIcons.cs`: talking to Explorer (reading and moving icons) and the COM declarations

Every line of code carries a comment explaining what it does.

## Where things were left (2026-11-22)

- V1R1 built and tested on Windows 11 (multi-monitor desktop): save, restore,
  and a real move test (moved one icon one row down, then back) all worked. The window opens.
- V1R2 (same evening): added the checkmark icon and minimize-to-tray. Tested: the exe shows the
  checkmark, and minimizing hides the window while the program keeps running.
- Ideas for later: an option to start minimized in the tray, and automatically picking a profile
  based on which monitors are connected.
