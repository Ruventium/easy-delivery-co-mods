# FlatOut 2 Steam: Remaster-like Build Specification

**Goal:** Улучшить Steam-версию FlatOut 2 до стабильной и визуально современной сборки с оригинальным контентом, без замены карьеры, машин, трасс и базовой физики.

## Scope

- Preserve the original campaign, cars, tracks, progression, and arcade identity.
- Add only compatibility fixes, widescreen and UI corrections, safe visual improvements, stability fixes, and compatible audio improvements.
- Do not combine multiple physics overhauls or install unverified total conversions.
- Create a restorable backup before every modification stage.
- Install and validate one component at a time.

## Verified Hardware

- Steering wheel: `ARDOR Le Mans`.
- Pedal unit: `PD HM`.
- Shifter: PXN A7 connected through the wheel and exposed as buttons on `ARDOR Le Mans`.
- Verified shifter mapping:
  - 1st gear: Button 17
  - 2nd gear: Button 19
  - 3rd gear: Button 21
  - 4th gear: Button 18
  - 5th gear: Button 20
  - 6th gear: Button 22
  - Reverse: Button 23

## Control Design

- Wheel steering uses the `ARDOR Le Mans` steering axis.
- Throttle and brake use the `PD HM` axes.
- Shifter positions are mapped as digital gear commands through the wheel buttons.
- FlatOut 2 does not need a clutch input for this build; clutch emulation is excluded unless a later, separately approved test proves it improves control.
- Preserve a keyboard fallback and document the original bindings before changing them.
- Tune steering sensitivity, dead zone, saturation, and centering only after a clean baseline run.

## Installation Stages

1. Inspect the current game directory and record executable, configuration, and save-file state.
2. Verify the Steam build launches unmodified and record baseline resolution, input behavior, and frame pacing.
3. Create a complete backup outside the game directory.
4. Install compatibility and widescreen/UI corrections.
5. Validate startup, menus, career loading, race loading, pause, exit, and controller input.
6. Install a compatible visual texture/effect package only if it preserves original content and passes the baseline scenes.
7. Install audio improvements only if they do not replace required game libraries or introduce crashes.
8. Configure and validate the wheel, pedals, and shifter mapping.
9. Test a short race, a destruction event, a career save/load cycle, and keyboard fallback.
10. Keep a manifest of installed files and a rollback procedure.

## Acceptance Criteria

- Game launches from Steam without manual repair.
- Original campaign, content, and saves remain available.
- No crash in menu, race start, race completion, pause, or exit during validation.
- Wheel steering, throttle, brake, and all seven verified shifter commands are detectable.
- Keyboard fallback remains usable.
- Visual improvements do not cause obvious UI overlap, broken aspect ratio, missing textures, or severe performance regression.
- Each installed component can be removed using the recorded manifest or backup.
