# FlatOut 2 Baseline

Date: 2026-08-22

Game directory: `A:\SteamLibrary\steamapps\common\FlatOut 2`

## Existing components

- `FlatOut2.exe`
- `fo2_zoom.dll`
- `zoom_platform.bfs`
- `dxwrapper.dll` version `1.4.7900.25`
- `d3dx9_30.dll`
- `SDL3.dll`
- `mods\FlatOut 2 Mod Manager.exe`
- `dxwrapper.ini`
- `zoom_fo2.cfg`
- `mods` directory
- `Savegame` directory

This is already a modified installation. Do not add another widescreen DLL or another DirectX wrapper without first removing or proving compatibility with the existing Zoom platform.

## Existing configuration

- `dxwrapper.ini` enables the D3D9 and DirectSound wrappers.
- `dxwrapper.ini` enables windowed mode.
- `zoom_fo2.cfg` leaves FOV and LOD at defaults and disables logging.

## Verification

- Direct launch of `FlatOut2.exe` held a live process for at least 12 seconds without an immediate exit.
- Full backup created at `A:\SteamLibrary\steamapps\common\FlatOut 2.backup-2026-08-22`.
- Backup size: approximately 3.34 GB.

## Baseline hashes

- `FlatOut2.exe`: `B7F691D098A167AD0C6C6FF4604DE6F653EFDCF93B6ACBDD0396097...`
- `data.bfs`: `ECEF95A985C83813A5C57B81A6346663977269EEED1B7B35F7887D8...`
- `zoom_platform.bfs`: `C8F67507E11EE138AF88C4065B04EB30B97FD9CAA48E225184A12F7...`
- `fo2_zoom.dll`: `003E0BDB22F879DBD3F38E92A90E273F7DC5346A58181FB1EA6EE63...`
- `dxwrapper.dll`: `CA76BC2989FE07F1BFE65ECB8FC057766DE5FA99563039171CD3C0C...`

The complete SHA-256 values can be regenerated with PowerShell before rollback.
