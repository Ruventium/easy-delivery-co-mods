# FlatOut 2 Remaster-like Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare and validate a reversible FlatOut 2 Steam enhancement build with original content, compatibility fixes, safe visual improvements, and wheel/pedal/shifter controls.

**Architecture:** Treat the game directory as an immutable baseline and install changes in isolated stages. Maintain a manifest and backup so each stage can be removed independently. Configure input only after the unmodified game and compatibility layer have been validated.

**Tech Stack:** Windows 10/11, Steam FlatOut 2, native game configuration, verified HID DirectInput devices, mod archives selected after compatibility inspection.

**Spec:** `docs/superpowers/specs/2026-08-22-flatout-2-remaster-design.md`

## Global Constraints

- Preserve original campaign, cars, tracks, progression, saves, and arcade identity.
- Do not install multiple physics overhauls or unverified total conversions.
- Create a complete backup before modifying the game.
- Install and validate one component at a time.
- Steering uses `ARDOR Le Mans`; pedals use `PD HM`.
- Shifter mapping through `ARDOR Le Mans`: `1=17`, `2=19`, `3=21`, `4=18`, `5=20`, `6=22`, `R=23`.
- Clutch emulation is excluded from the first build.

---

### Task 1: Inventory The Baseline

**Files:**
- Read: `A:\SteamLibrary\steamapps\common\FlatOut 2\*`
- Create: `docs/superpowers/plans/flatout-2-baseline.md`

- [ ] Record the executable names, configuration files, save locations, existing DLLs, and current modification timestamps.
- [ ] Confirm the Steam build launches before any change.
- [ ] Record current resolution, refresh rate, input behavior, and whether saves load.
- [ ] Stop and resolve any pre-existing launch or save failure before installing mods.

### Task 2: Create A Reversible Backup

**Files:**
- Create: `A:\SteamLibrary\steamapps\common\FlatOut 2.backup-2026-08-22\`
- Create: `docs/superpowers/plans/flatout-2-manifest.md`

- [ ] Verify the destination drive has sufficient free space.
- [ ] Copy the complete game directory and relevant save/configuration data.
- [ ] Record hashes for the original executable and all existing DLL files.
- [ ] Write the backup location and rollback command into the manifest.

### Task 3: Select Compatibility Components

**Files:**
- Create: `docs/superpowers/plans/flatout-2-mod-selection.md`

- [ ] Check each candidate mod's supported game version, installation files, dependencies, and known conflicts.
- [ ] Select the smallest compatible set covering widescreen, modern display modes, input stability, and crash prevention.
- [ ] Reject any package that replaces campaign content, requires a conflicting physics overhaul, or lacks a rollback path.
- [ ] Record source URLs, archive hashes, destination files, and removal instructions before installation.

### Task 4: Install And Validate Compatibility Layer

**Files:**
- Modify: `A:\SteamLibrary\steamapps\common\FlatOut 2\` only with the selected compatibility files.
- Modify: `docs/superpowers/plans/flatout-2-manifest.md`

- [ ] Install the selected compatibility component without adding unrelated visual or physics changes.
- [ ] Launch through Steam and test menu navigation, resolution, career load, race load, pause, completion, and exit.
- [ ] If a test fails, remove only this stage using the manifest and restore the baseline before investigating further.
- [ ] Record the validated configuration and file hashes.

### Task 5: Add Visual And Audio Improvements

**Files:**
- Modify: `A:\SteamLibrary\steamapps\common\FlatOut 2\` only with approved texture/effect/audio files.
- Modify: `docs/superpowers/plans/flatout-2-manifest.md`

- [ ] Install the visual package after compatibility validation, preserving original models, tracks, and career data.
- [ ] Test menus, HUD, garage, race start, vehicle damage, destruction event, and night/indoor scenes.
- [ ] Compare frame pacing and texture loading against the baseline.
- [ ] Install audio changes separately and repeat the same crash and save/load checks.
- [ ] Roll back the individual stage if it causes missing assets, UI breakage, or unacceptable performance loss.

### Task 6: Configure Wheel, Pedals, And Shifter

**Files:**
- Modify: FlatOut 2 control configuration discovered in Task 1.
- Modify: `docs/superpowers/plans/flatout-2-controls.md`

- [ ] Bind steering to the `ARDOR Le Mans` steering axis.
- [ ] Bind throttle and brake to the `PD HM` axes and verify that neither axis is inverted unexpectedly.
- [ ] Bind shifter buttons 17, 19, 21, 18, 20, 22, and 23 to gears 1, 2, 3, 4, 5, 6, and reverse respectively.
- [ ] Set a conservative steering dead zone and sensitivity, then tune only one setting at a time during a test race.
- [ ] Keep keyboard fallback bindings and verify they work after the wheel profile is active.

### Task 7: Full Acceptance Test

**Files:**
- Modify: `docs/superpowers/plans/flatout-2-acceptance.md`

- [ ] Cold-launch from Steam three times.
- [ ] Complete a short race using wheel, pedals, and at least four forward gears.
- [ ] Test reverse, pause, restart, race completion, career save, and career reload.
- [ ] Test one destruction event and confirm no missing textures, broken HUD, or input lock.
- [ ] Test keyboard fallback after disconnecting or disabling the wheel profile.
- [ ] Record installed versions, hashes, known limitations, and the final rollback procedure.
