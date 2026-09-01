# FlatOut 2 Installed Manifest

## Baseline preserved

- Backup: `A:\SteamLibrary\steamapps\common\FlatOut 2.backup-2026-08-22`
- Backup copy result: `robocopy` exit code `1`, with no failed files.
- No new game files have been installed by this session.

## Existing files before this session

- Zoom platform: `fo2_zoom.dll`, `zoom_platform.bfs`, `zoom_fo2.cfg`
- Compatibility wrapper: `dxwrapper.dll`, `dxwrapper.ini`, `d3dx9_30.dll`, `SDL3.dll`
- Mod management: `mods\FlatOut 2 Mod Manager.exe`, `mods\bfstool.exe`, `mods\steam_api64.dll`

## Rollback

Close Steam and all FlatOut 2 processes. To restore the pre-session game directory, copy the backup over the game directory with a verified file copy tool. Do not delete the backup until the final build has passed acceptance testing.

## Rejected component: FlatOut 2 Steering Wheel Fix

- Source: `https://www.moddb.com/mods/flatout-2-steering-wheel-fix/downloads/flatout-2-steering-wheel-fix`
- Archive MD5 verified before inspection: `f68801ac7a3f937f7f30c1c25eb1ed08`
- Rejected because the installed `bfstool.exe` could not list or parse the archive: `File not found in BFS file database` and `Please provide an appropriate format to use`.
- The mod file and its `filesystem` entry were removed after the test.
- The original `filesystem` backup remains at `filesystem.before-wheel-fix-2026-08-22`.

## Current validation

- Direct `FlatOut2.exe` launch without the rejected mod remains alive after the test interval; no file-loading error was reported.
- The legacy Mod Manager was disabled as `mods\FlatOut 2 Mod Manager.disabled.exe`; it cannot query the Zoom Platform BFS format.
- The `filesystem` file was restored byte-for-byte from `filesystem.before-wheel-fix-2026-08-22` after a line-ending corruption caused `Fatal error` on launch.
- Verified current `filesystem` SHA-256: `DF6F71E39CEBB9FAA487497C5CBC0C83FAAC0B233EEE12BA43A0B41B9404533C`.
- Verified direct launch after restoration shows the normal `FlatOut 2` window and remains responsive.
- Steam `-applaunch 2990` did not leave a FlatOut 2 process running during the automated test. Input assignment therefore remains pending manual in-game verification.
