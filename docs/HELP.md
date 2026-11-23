ICONKEEPER HELP
===============

IconKeeper remembers where your desktop icons are and puts them back
when Windows (or a monitor change, a game, a remote session) scrambles
them. You can keep several saved layouts, called PROFILES. One of them
can be the DEFAULT, and it can be restored automatically when you log in.


QUICK START
-----------

1. Arrange your desktop icons the way you like them.
2. Open IconKeeper and click "Save current layout as...". Type a name.
   (Your first profile becomes the default automatically.)
3. Tick "Restore the default profile when I log in to Windows".

That's it. If the icons ever get moved, double-click your profile, or
right-click the tray checkmark and choose "Restore default".


THE MAIN WINDOW
---------------

The list shows every profile: its name, whether it is the default
(shown in bold with "Yes"), how many icons it holds, the screen size it
was saved at, and when it was saved.

  Save current layout as...   Save where every icon is right now as a
                              new profile. If the name already exists
                              you are asked before it is replaced.
  Update selected             Replace the selected profile with the
                              icon positions on the desktop now.
  Restore selected            Move the icons back to where the selected
                              profile says. Double-clicking a profile
                              does the same thing.
  Make selected the default   Choose the profile restored at login.
  Rename...                   Give a profile a new name. If it was the
                              default it stays the default.
  Delete                      Remove a profile for good.
  Open data folder            Show %APPDATA%\IconKeeper in Explorer.
  Refresh                     Re-read the profiles from disk.
  Help (F1)                   This help.
  About...                    Version, copyright and license agreement.

Check boxes:

  Restore the default profile when I log in to Windows
      Adds IconKeeper to your login startup list. At login it waits
      (up to 2 minutes) for the desktop to finish loading, restores the
      default profile, and quits. Nothing is shown on screen; results go
      in the log file.

  Stretch positions to fit if the screen size has changed
      If a profile was saved at a different total screen size (all
      monitors together), positions are scaled to fit the screen you
      have now. Untick it to put icons at the exact saved pixels.


THE SYSTEM TRAY
---------------

Minimizing the window hides it from the taskbar and shows a green
checkmark in the system tray by the clock. On Windows 11 the checkmark
may be hidden under the ^ arrow; drag it next to the clock to keep it
in view.

  Left-click      Open the IconKeeper window.
  Right-click     Quick menu: Open, Restore default, Restore (any
                  profile), Save current layout as..., Help, About,
                  Exit.

Clicking the X on the window closes IconKeeper completely. Only
minimizing sends it to the tray.


COMMAND LINE
------------

IconKeeper can also be run from a Command Prompt, a shortcut or a
batch file:

  IconKeeper /save <name>        save the current layout as <name>
  IconKeeper /restore <name>     restore the layout <name>
  IconKeeper /restore-default    wait for the desktop, then restore the
                                 default (this is what runs at login)
  IconKeeper /list               list profiles (* marks the default)
  IconKeeper /?                  show these commands

Names may contain spaces. Exit codes: 0 OK, 1 error, 2 profile not
found, 3 desktop never became ready, 4 license not yet accepted.


GOOD TO KNOW
------------

* Icons are matched by their full path (for example
  %USERPROFILE%\Desktop\Notes.txt). A file you renamed or deleted since
  the save is skipped. A new file is left wherever it is. Save the
  profile again ("Update selected") to include new icons.

* "Auto arrange icons" would push the icons back into rows, so
  IconKeeper turns it off when restoring. "Align icons to grid" is left
  alone, so an icon may land a few pixels from the exact saved spot.

* Windows remembers the restored positions itself, the same as if you
  had dragged the icons there by hand.

* IconKeeper runs only on your computer and sends nothing anywhere.


WHERE THINGS ARE KEPT
---------------------

  Profiles   %APPDATA%\IconKeeper\profiles\<name>.json
             (plain text; can be copied to another PC)
  Settings   %APPDATA%\IconKeeper\settings.json
  Log        %APPDATA%\IconKeeper\iconkeeper.log
  Login      HKCU\Software\Microsoft\Windows\CurrentVersion\Run,
             value "IconKeeper" (only while the box is ticked)


TROUBLESHOOTING
---------------

Icons did not move at login
    Open the log (Open data folder > iconkeeper.log). "Desktop never
    became ready" means Explorer took over 2 minutes to start; try
    "Restore default" from the tray. Also check that a default profile
    is chosen (bold in the list).

Some icons were not restored
    The status line and log say how many were "in profile but not on
    desktop". Those files were renamed, moved or deleted since the save.

Icons ended up in the wrong place after changing monitors
    Make a separate profile for each monitor setup (for example
    "Docked" and "Laptop only") and restore the one that matches.

Uninstalling
    Untick the login box, close IconKeeper, then delete IconKeeper.exe
    and the %APPDATA%\IconKeeper folder.


Copyright (c) 2026 P. H. Ewen (KO4WSC). Freeware; see the License
Agreement (About... > License Agreement...).
