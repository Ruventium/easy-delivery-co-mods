# FlatOut 2 Wheel Control Profile

## Devices

- Wheel: `ARDOR Le Mans`
- Pedals: `PD HM`
- Shifter: PXN A7 exposed through `ARDOR Le Mans`

## Verified shifter buttons

| Gear | Button |
|---|---:|
| 1 | 17 |
| 2 | 19 |
| 3 | 21 |
| 4 | 18 |
| 5 | 20 |
| 6 | 22 |
| Reverse | 23 |

## Initial in-game bindings

- Steering: assign the `ARDOR Le Mans` left/right steering axis in the game's control menu.
- Throttle: assign the `PD HM` axis that moves when the accelerator is pressed.
- Brake: assign the `PD HM` axis that moves when the brake is pressed.
- Gears: assign buttons 17, 19, 21, 18, 20, 22, and 23 to 1, 2, 3, 4, 5, 6, and reverse.
- Clutch: leave unassigned for the first pass.
- Preserve keyboard bindings as fallback.

## Tuning order

1. Verify center and full-lock steering.
2. Verify throttle and brake direction.
3. Add only a small dead zone if the wheel drifts at rest.
4. Adjust steering sensitivity after a test race.
5. Do not change multiple tuning values at once.
