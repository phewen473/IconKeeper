# IconKeeper - Build Plan

This document is the plan for building IconKeeper: what it has to do, how it is put together,
how to build and release it, how it was tested, and what comes next.

## 1. Goal

A small Windows program that saves the positions of the desktop icons and restores them later.

Requirements (as asked for):

| # | Requirement | Status |
|---|---|---|
| R1 | Save desktop icon locations | Done (V1R1) |
| R2 | Multiple named profiles | Done (V1R1) |
| R3 | A default profile that is restored at login | Done (V1R1) |
| R4 | Checkmark application icon | Done (V1R2) |
| R5 | Minimize to the system tray | Done (V1R2) |
| R6 | About box with a license agreement | Done (V1R3) |
| R7 | Help documentation and a build plan | Done (V1R3) |
| R8 | Published on GitHub | Done (V1R3) |

House rules for this project: every line of code carries a comment; a plain-English README is kept
up to date; every version is kept as a snapshot under `versions\`; every change is committed to the
Git repository and pushed to GitHub.

## 2. Design decisions

| Decision | Choice | Why |
|---|---|---|
| Language / UI | C# WinForms on .NET 8 | Small, native-looking window; easy COM access. |
| Reading/moving icons | Explorer's `IFolderView2` via ShellWindows → desktop browser → active view | The supported shell interface. Older tools read Explorer's list-view memory across processes, which is fragile. |
| Matching icons | Full parsing name (path or `::{CLSID}`) | Unique and stable. Display names can repeat. |
| Storage | One JSON file per profile in `%APPDATA%\IconKeeper\profiles` | Readable, easy to back up or copy, no database needed. |
| Login restore | `HKCU\...\Run` entry running `IconKeeper.exe /restore-default` | No admin rights needed; no service; the program exits after restoring. |
| Login timing | Poll every 2 s until the desktop exists and the icon count is steady (max 2 min) | Explorer and OneDrive fill the desktop late in the login. |
| Screen changes | Store total screen size; scale positions if it differs (option) | Monitor setups change; scaling keeps the layout close to right. |
| Auto arrange | Turned off when restoring | Otherwise Windows immediately rearranges the icons. |
| Icon | Drawn by script (`tools\make_icon.ps1`) into `src\app.ico` | Reproducible; no image editor needed. |
| License | Freeware EULA, accepted once on first run (stored as `AcceptedLicense` in settings.json) | Owner's choice. Bumping `LicenseGate.LicenseVersion` asks again. |
| Help | `docs\HELP.md`, embedded in the exe, shown by Help (F1) | One file serves both the program and GitHub readers. |

## 3. Structure

```
IconKeeper\
  IconKeeper.exe        built program
  build.bat             build script
  LICENSE.txt           license agreement (embedded in the exe)
  README.md             overview, where things live, status
  docs\HELP.md          user help (embedded in the exe)
  docs\BUILD-PLAN.md    this plan
  src\Program.cs        startup, license check, command line, login restore
  src\MainForm.cs       main window, tray icon and menu, name dialog
  src\AboutAndLicense.cs  About box, License window, Help window, embedded resources
  src\ProfileStore.cs   profiles, settings, login registry entry, log
  src\DesktopIcons.cs   Explorer COM interop: read and move icons
  src\IconKeeper.csproj project settings
  src\app.ico           checkmark icon
  tools\make_icon.ps1   draws app.ico
  versions\IconKeeper-V1Rn\  snapshot of every released version
```

## 4. How to build

Prerequisites: Windows 10/11, .NET SDK 8 or later, Git.

1. Open a Command Prompt in the project folder.
2. Run `build.bat`. It runs
   `dotnet publish src\IconKeeper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`
   and copies the result to `IconKeeper.exe` in the project folder.
3. The exe is about 0.3 MB and needs the .NET 8 Desktop Runtime on the PC that runs it
   (free from Microsoft; Windows prompts for it if it is missing).

To change the icon: edit the colours/shape in `tools\make_icon.ps1`, run
`powershell -ExecutionPolicy Bypass -File tools\make_icon.ps1`, then `build.bat`.

## 5. Release steps (each new version)

1. Bump `VersionName` in `src\Program.cs` (V1Rn) and `<Version>` in `src\IconKeeper.csproj` (1.n.0).
2. Run `build.bat`; fix any warnings.
3. Run the tests in section 6.
4. Update `README.md` ("Current version" and "Where things were left") and `docs\HELP.md` if the
   program's behaviour changed.
5. Copy the exe, build.bat, README, LICENSE, docs, src and tools into `versions\IconKeeper-V1Rn\`.
6. Commit and push (`git push`).

## 6. Test plan

| Test | How | Expected |
|---|---|---|
| Save | `IconKeeper /save _Test` | Exit 0; `_Test.json` lists every desktop icon with X/Y. |
| Restore (no change) | `IconKeeper /restore _Test` | Exit 0; log says all icons moved, 0 missing. |
| Real move | Edit one icon's Y in a copy of the profile, restore it, save again, compare; then restore the original | Icon moved to the edited spot (snapped to grid), then back. |
| Window | Start with no arguments | Window opens with the checkmark icon. |
| Tray | Minimize | Window hidden, process still running, tray checkmark shown. |
| License first run | Remove `AcceptedLicense` from settings.json and start | License window appears; "I Do Not Agree" exits; "I Agree" opens the main window and is not asked again. |
| License on command line | With no acceptance, run `/restore x` | Exit 4 and a log line. |
| About / Help | Click About... and Help (F1) | About shows version, copyright, link and License button; Help shows HELP.md. |
| Login | Tick the login box, sign out and back in | Icons restored; log shows "Login: waiting..." then "Restored ...". |

Results for V1R1 to V1R3 are recorded in the README under "Where things were left".

## 7. Future ideas

- Option to start minimized in the tray (and to keep running in the tray after login).
- Pick a profile automatically from the connected monitor setup.
- Watch for icons being scrambled (for example after a resolution change) and offer to restore.
- Installer (MSI or NSIS) with Start-menu shortcut and uninstaller.
- Export/import of profiles between PCs from inside the program.
