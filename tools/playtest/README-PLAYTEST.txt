FACTORY 1 (working title) - playtest build for Windows
======================================================

Thank you for playtesting! This is an early build: expect rough edges.

Running it
----------
1. Unzip the whole folder somewhere (for example your Desktop). Keep the files together:
   Factory.exe needs Factory.pck and the data_Factory.Game_windows_x86_64 folder next to it.
2. Double-click Factory.exe.
3. Windows may say "Windows protected your PC" because the build isn't signed yet.
   Click "More info", then "Run anyway".

You don't need to install .NET or anything else; it's all in the folder.
Factory.console.exe is the same game with a console window, useful if something crashes.

The game
--------
You are a robot commander sent ahead of human settlers. Build up from hand-mining to automated
factories, reach the other planets, and terraform three of the four habitable ones so the settlers
can be called. Press H in game for help.

New to the game? The GUIDE card (top right, under the minimap) gives you the next objective and how to do it, and ticks
it off when you've done it. G hides or shows it. If text is too small or too big, press Esc and use
"Interface size" (it's saved for next time).

Controls
--------
WASD                     Walk
Right-click (drag)       Mine ore or trees
1-7, then 1-0            Open a build category, then pick a building; left-click places, R rotates
X / Q                    Dismantle tool / pick the building under the cursor
Esc                      Step back out of a tool or window; then the game menu
E, I or Tab              Inventory      C  Replicator     T  Tech tree     M  Map     P  Star system     H  Help
G                        Show / hide the guide
Left-click a building    Open it; hover anything for quick info
F                        Unload pockets into the nearest lander or crate
Space / + / -            Pause / game speed
F5 / F9                  Quicksave / quickload

Saving and loading
------------------
Press Esc for the game menu, then "Save game": type a name and click Save (or click a save in the list to
save over it; it asks first). "Load game" lists your saves with the date, time played and planet; click Load.
F5 quicksaves and F9 loads the quicksave. The game also autosaves every 10 minutes of play into
"Autosave 1" to "Autosave 3", taking turns, and says so at the bottom left.
Loading replays your whole game from the start, so a long game takes a while; a progress bar shows how far
it has got and you can cancel it. Saves are kept in:
    %APPDATA%\Godot\app_userdata\Factory 1 (working title)\saves\
A save from a much older or newer build may not load; the Load list greys it out and says why.

Sending feedback
----------------
Tell us what was confusing, what felt slow or too fast, and anything that broke.
Please include the contents of VERSION.txt from this folder.
If something went wrong, a save and the log help a lot. They're in:
    %APPDATA%\Godot\app_userdata\Factory 1 (working title)\
(paste that into the File Explorer address bar). Send the save from the "saves" folder (the latest
Autosave is fine) and the "logs" folder.
